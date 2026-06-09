"""
=============================================================================
  6_universal_dataset_builder.py — Bộ Xây dựng Dataset Thống nhất Vạn năng
=============================================================================
  Dự án  : Dynamic Spatial Hand Gesture Recognition
  Mục đích: Pipeline tự động phát hiện, xử lý, và hợp nhất NHIỀU dataset
            cử chỉ tay từ nhiều nguồn/format khác nhau thành 1 file CSV
            chuẩn duy nhất: mega_raw_data.csv

  ╔══════════════════════════════════════════════════════════════════════╗
  ║  HƯỚNG DẪN CÀI ĐẶT & CHẠY                                        ║
  ║                                                                    ║
  ║  Bước 1 — Kích hoạt môi trường ảo:                                 ║
  ║    cd D:\WORKSAPCE\Monitor_AR\1_AI_Model_Training         ║
  ║    .\venv\Scripts\activate                                         ║
  ║                                                                    ║
  ║  Bước 2 — Cài đặt thư viện:                                       ║
  ║    pip install torch opencv-python mediapipe numpy pandas scipy     ║
  ║    pip install tqdm                                                ║
  ║                                                                    ║
  ║  Bước 3 — Đặt dataset vào thư mục Downloads/:                     ║
  ║    Dataset/Downloads/DHGdataset/    ← DHG-14/28 skeleton files     ║
  ║    Dataset/Downloads/SHREC2017/     ← SHREC mirror (cùng format)   ║
  ║    Dataset/Downloads/FPHA/          ← First-Person Hand Action     ║
  ║    Dataset/Downloads/Videos/        ← Raw video files (.mp4/.avi)  ║
  ║    Dataset/Downloads/MediaPipeCSV/  ← Pre-extracted CSV/JSON       ║
  ║                                                                    ║
  ║  Bước 4 — Chạy:                                                   ║
  ║    python Scripts/6_universal_dataset_builder.py                    ║
  ║                                                                    ║
  ║  Output: Dataset/mega_raw_data.csv                                 ║
  ╚══════════════════════════════════════════════════════════════════════╝

  Kiến trúc:
    DatasetParser (Abstract Base Class)
      ├── DHGParser           — DHG-14/28 & SHREC 2017 (22 joints → 21)
      ├── FPHAParser          — First-Person Hand Action (21 joints)
      ├── BriareoParser       — Briareo dataset (22 joints → 21)
      ├── MediaPipeCSVParser  — CSV chứa 21 landmarks MediaPipe
      ├── MediaPipeJSONParser — JSON chứa landmarks MediaPipe
      ├── RawVideoParser      — Video .mp4/.avi → MediaPipe extraction
      └── ExistingCSVParser   — raw_data.csv format hiện tại
=============================================================================
"""

import os
import sys
import json
import glob
import math
import time
import logging
import traceback
from abc import ABC, abstractmethod
from pathlib import Path
from typing import List, Dict, Tuple, Optional

import cv2
import numpy as np
import pandas as pd
from scipy.interpolate import interp1d

try:
    from tqdm import tqdm
    HAS_TQDM = True
except ImportError:
    HAS_TQDM = False
    print("[WARN] tqdm not installed. Install via: pip install tqdm")

try:
    import mediapipe as mp
    HAS_MEDIAPIPE = True
except ImportError:
    HAS_MEDIAPIPE = False
    print("[WARN] mediapipe not installed. Video parsing disabled.")


# ═══════════════════════════════════════════════════════════════════════════════
# CẤU HÌNH
# ═══════════════════════════════════════════════════════════════════════════════
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOWNLOAD_DIR = os.path.join(PROJECT_ROOT, "Dataset", "Downloads")
OUTPUT_CSV = os.path.join(PROJECT_ROOT, "Dataset", "mega_raw_data.csv")
EXISTING_CSV = os.path.join(PROJECT_ROOT, "Dataset", "raw_data.csv")

TARGET_SEQ_LEN = 15
TARGET_JOINTS = 21
TARGET_COORDS = 3
TARGET_FEATURES = TARGET_JOINTS * TARGET_COORDS  # 63

# Cấu hình logging
LOG_FILE = os.path.join(PROJECT_ROOT, "Dataset", "build_log.txt")

# Nhãn cử chỉ chuẩn — ánh xạ từ nhiều nguồn
GESTURE_LABEL_MAP = {
    # DHG-14/28 & SHREC
    1: "grab", 2: "tap", 3: "expand", 4: "pinch",
    5: "rotation_cw", 6: "rotation_ccw",
    7: "swipe_right", 8: "swipe_left",
    9: "swipe_up", 10: "swipe_down",
    11: "swipe_x", 12: "swipe_v", 13: "swipe_plus", 14: "shake",
}

# Landmark columns cho output
LANDMARK_COLS = []
for i in range(TARGET_JOINTS):
    LANDMARK_COLS.extend([f"x{i}", f"y{i}", f"z{i}"])

OUTPUT_COLUMNS = ["sample_id", "frame_index", "label", "source"] + LANDMARK_COLS


# ═══════════════════════════════════════════════════════════════════════════════
# TIỆN ÍCH CHUNG
# ═══════════════════════════════════════════════════════════════════════════════

def setup_logging():
    """Thiết lập logging ra file + console."""
    os.makedirs(os.path.dirname(LOG_FILE), exist_ok=True)
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(message)s",
        handlers=[
            logging.FileHandler(LOG_FILE, mode='w', encoding='utf-8'),
            logging.StreamHandler(sys.stdout),
        ]
    )


def temporal_resample(sequence, target_length=TARGET_SEQ_LEN):
    """
    Resample chuỗi bất kỳ về đúng target_length frames.
    Sử dụng linear interpolation (scipy.interpolate.interp1d).

    Input:  (T_original, 21, 3)  — T bất kỳ
    Output: (target_length, 21, 3)

    Chiến lược:
      - T < target  → upsample (nội suy frame trung gian)
      - T > target  → downsample (lấy mẫu đều)
      - T == target → giữ nguyên
      - T == 1      → repeat frame
    """
    T_orig = sequence.shape[0]

    if T_orig == target_length:
        return sequence

    if T_orig < 2:
        return np.tile(sequence, (target_length, 1, 1))

    # Flatten: (T, 21, 3) → (T, 63) cho interpolation
    flat = sequence.reshape(T_orig, -1)

    x_orig = np.linspace(0, 1, T_orig)
    x_target = np.linspace(0, 1, target_length)

    interpolator = interp1d(x_orig, flat, axis=0, kind='linear')
    resampled = interpolator(x_target)

    return resampled.reshape(target_length, TARGET_JOINTS, TARGET_COORDS)


def normalize_skeleton(sequence):
    """
    Chuẩn hóa tọa độ 3D:
      1. Center trên wrist (joint 0) tại mỗi frame
      2. Scale bằng max absolute value toàn chuỗi

    Input/Output: (T, 21, 3)
    """
    wrist = sequence[:, 0:1, :]  # (T, 1, 3)
    centered = sequence - wrist

    max_val = np.abs(centered).max()
    if max_val > 1e-8:
        centered = centered / max_val

    return centered


def remap_joints(skeleton, original_joints, joints_to_keep):
    """
    Chọn subset joints từ skeleton.
    Ví dụ: DHG 22 joints → drop joint 0 (palm center) → 21 joints.

    Input:  (T, original_joints, 3)
    Output: (T, len(joints_to_keep), 3)
    """
    return skeleton[:, joints_to_keep, :]


def progress_bar(iterable, total=None, desc="Processing"):
    """Wrapper cho tqdm hoặc fallback đơn giản."""
    if HAS_TQDM:
        return tqdm(iterable, total=total, desc=desc, ncols=80)
    else:
        return iterable


def validate_sequence(sequence, label):
    """
    Kiểm tra tính hợp lệ của 1 sample đã xử lý.
    Returns: (is_valid, error_message)
    """
    if sequence is None:
        return False, "sequence is None"
    if sequence.shape != (TARGET_SEQ_LEN, TARGET_JOINTS, TARGET_COORDS):
        return False, f"wrong shape {sequence.shape}, expected ({TARGET_SEQ_LEN}, {TARGET_JOINTS}, {TARGET_COORDS})"
    if np.isnan(sequence).any():
        return False, "contains NaN"
    if np.isinf(sequence).any():
        return False, "contains Inf"
    if not label or not isinstance(label, str):
        return False, f"invalid label: {label}"
    return True, ""


# ═══════════════════════════════════════════════════════════════════════════════
# ABSTRACT BASE CLASS
# ═══════════════════════════════════════════════════════════════════════════════

class DatasetParser(ABC):
    """
    Lớp cơ sở trừu tượng cho tất cả dataset parsers.
    Mỗi subclass chịu trách nhiệm parse 1 format dataset cụ thể.
    """

    def __init__(self, name: str, root_dir: str):
        self.name = name
        self.root_dir = root_dir
        self.logger = logging.getLogger(f"Parser.{name}")

    @abstractmethod
    def discover(self) -> List[Dict]:
        """
        Quét thư mục và trả về danh sách các mẫu cần xử lý.
        Mỗi mẫu là dict chứa thông tin path, label, metadata.
        """
        pass

    @abstractmethod
    def parse_sample(self, sample_info: Dict) -> Tuple[Optional[np.ndarray], str]:
        """
        Xử lý 1 mẫu → trả về (sequence, label).
        sequence: (T, 21, 3) — CHƯA resample/normalize.
        label: string tên cử chỉ.
        """
        pass

    def process_all(self) -> List[Dict]:
        """
        Xử lý toàn bộ dataset: discover → parse → resample → normalize.
        Returns: List of {sequence: (15, 21, 3), label: str}
        """
        samples = self.discover()
        if not samples:
            self.logger.warning(f"Không tìm thấy mẫu nào trong {self.root_dir}")
            return []

        self.logger.info(f"Phát hiện {len(samples)} mẫu trong {self.name}")

        results = []
        errors = 0

        for sample_info in progress_bar(samples, desc=f"[{self.name}]"):
            try:
                sequence, label = self.parse_sample(sample_info)

                if sequence is None:
                    errors += 1
                    continue

                # Resample → 15 frames
                resampled = temporal_resample(sequence, TARGET_SEQ_LEN)

                # Normalize
                normalized = normalize_skeleton(resampled)

                # Validate
                is_valid, err_msg = validate_sequence(normalized, label)
                if not is_valid:
                    self.logger.debug(f"Invalid sample: {err_msg}")
                    errors += 1
                    continue

                results.append({
                    "sequence": normalized,
                    "label": label,
                })

            except Exception as e:
                self.logger.debug(f"Error processing sample: {e}")
                errors += 1

        self.logger.info(
            f"{self.name}: {len(results)} thành công, {errors} lỗi "
            f"({len(results)/(len(results)+errors)*100:.1f}% success rate)"
        )

        return results


# ═══════════════════════════════════════════════════════════════════════════════
# PARSER 1: DHG-14/28 & SHREC 2017
# ═══════════════════════════════════════════════════════════════════════════════

class DHGParser(DatasetParser):
    """
    Parser cho DHG-14/28 và SHREC 2017 dataset.

    Format: skeleton_world.txt — mỗi dòng chứa 66 float (22 joints × 3 coords).
    Joint 0 = Palm Center → DROP để còn 21 joints (mapping sang MediaPipe).

    Cấu trúc thư mục:
      root/gesture_{1-14}/finger_{1,2}/subject_{1-20}/essai_{1-5}/skeleton_world.txt
    """

    # Indices cần giữ (drop joint 0 = palm center)
    JOINTS_TO_KEEP = list(range(1, 22))

    def __init__(self, root_dir: str, use_28_classes=False):
        super().__init__("DHG-14/28", root_dir)
        self.use_28_classes = use_28_classes

    def discover(self) -> List[Dict]:
        samples = []

        for gesture_id in range(1, 15):
            gesture_dir = os.path.join(self.root_dir, f"gesture_{gesture_id}")
            if not os.path.isdir(gesture_dir):
                continue

            for finger_type in ["finger_1", "finger_2"]:
                finger_dir = os.path.join(gesture_dir, finger_type)
                if not os.path.isdir(finger_dir):
                    continue

                for subject_id in range(1, 21):
                    subject_dir = os.path.join(finger_dir, f"subject_{subject_id}")
                    if not os.path.isdir(subject_dir):
                        continue

                    for essai_id in range(1, 6):
                        skel_path = os.path.join(
                            subject_dir, f"essai_{essai_id}", "skeleton_world.txt"
                        )
                        if os.path.isfile(skel_path):
                            samples.append({
                                "path": skel_path,
                                "gesture_id": gesture_id,
                                "finger_type": finger_type,
                            })

        return samples

    def parse_sample(self, info: Dict) -> Tuple[Optional[np.ndarray], str]:
        data = np.loadtxt(info["path"])
        if data.ndim == 1:
            data = data.reshape(1, -1)

        if data.shape[1] != 66:
            return None, ""

        # Reshape → (T, 22, 3) → select 21 joints
        skeleton = data.reshape(-1, 22, 3)
        skeleton = remap_joints(skeleton, 22, self.JOINTS_TO_KEEP)

        # Label
        label = GESTURE_LABEL_MAP.get(info["gesture_id"], f"gesture_{info['gesture_id']}")
        if self.use_28_classes:
            suffix = "_1f" if info["finger_type"] == "finger_1" else "_wh"
            label += suffix

        return skeleton, label


# ═══════════════════════════════════════════════════════════════════════════════
# PARSER 2: FPHA (First-Person Hand Action)
# ═══════════════════════════════════════════════════════════════════════════════

class FPHAParser(DatasetParser):
    """
    Parser cho FPHA dataset.

    Format: Mỗi file chứa 21 joints × 3 coords per frame.
    Cấu trúc thư mục:
      root/Hand_pose_annotation_v1/{action_name}/{video_id}/skeleton.txt
    """

    def __init__(self, root_dir: str):
        super().__init__("FPHA", root_dir)
        self.anno_dir = os.path.join(root_dir, "Hand_pose_annotation_v1")
        if not os.path.isdir(self.anno_dir):
            self.anno_dir = root_dir

    def discover(self) -> List[Dict]:
        samples = []

        if not os.path.isdir(self.anno_dir):
            return samples

        for action_name in sorted(os.listdir(self.anno_dir)):
            action_dir = os.path.join(self.anno_dir, action_name)
            if not os.path.isdir(action_dir):
                continue

            for seq_name in sorted(os.listdir(action_dir)):
                seq_dir = os.path.join(action_dir, seq_name)
                if not os.path.isdir(seq_dir):
                    continue

                skel_path = os.path.join(seq_dir, "skeleton.txt")
                if not os.path.isfile(skel_path):
                    # Thử các tên file khác
                    for alt_name in ["skeleton_world.txt", "joints.txt", "pose.txt"]:
                        alt_path = os.path.join(seq_dir, alt_name)
                        if os.path.isfile(alt_path):
                            skel_path = alt_path
                            break

                if os.path.isfile(skel_path):
                    # Map action name → gesture label
                    label = action_name.lower().replace(" ", "_").replace("-", "_")
                    samples.append({
                        "path": skel_path,
                        "label": label,
                    })

        return samples

    def parse_sample(self, info: Dict) -> Tuple[Optional[np.ndarray], str]:
        data = np.loadtxt(info["path"])
        if data.ndim == 1:
            data = data.reshape(1, -1)

        n_cols = data.shape[1]

        if n_cols == 63:
            # Đã là 21 joints × 3
            skeleton = data.reshape(-1, 21, 3)
        elif n_cols == 66:
            # 22 joints → drop joint 0
            skeleton = data.reshape(-1, 22, 3)[:, 1:, :]
        else:
            # Thử chia cho 3 để xác định số joints
            n_joints = n_cols // 3
            if n_joints >= 21:
                skeleton = data.reshape(-1, n_joints, 3)[:, :21, :]
            else:
                return None, ""

        return skeleton, info["label"]


# ═══════════════════════════════════════════════════════════════════════════════
# PARSER 3: Briareo
# ═══════════════════════════════════════════════════════════════════════════════

class BriareoParser(DatasetParser):
    """
    Parser cho Briareo dataset.

    Briareo cung cấp 3D hand joints (22 joints, tương tự DHG).
    Format thường là CSV hoặc TXT với 66 columns per frame.
    """

    JOINTS_TO_KEEP = list(range(1, 22))  # Drop palm center

    BRIAREO_LABELS = {
        0: "swipe_left", 1: "swipe_right", 2: "swipe_up", 3: "swipe_down",
        4: "grab", 5: "tap", 6: "expand", 7: "pinch",
        8: "rotation_cw", 9: "rotation_ccw", 10: "shake", 11: "swipe_x",
    }

    def __init__(self, root_dir: str):
        super().__init__("Briareo", root_dir)

    def discover(self) -> List[Dict]:
        samples = []
        # Quét tất cả file .txt và .csv
        for ext in ["*.txt", "*.csv"]:
            for filepath in glob.glob(os.path.join(self.root_dir, "**", ext), recursive=True):
                # Thử đọc header/first line để detect format
                try:
                    with open(filepath, 'r') as f:
                        first_line = f.readline().strip()
                    # Nếu dòng đầu có ~66 số → skeleton file
                    parts = first_line.replace(',', ' ').split()
                    if len(parts) >= 63:
                        # Infer label từ folder name
                        parent = Path(filepath).parent.name
                        label = parent.lower().replace(" ", "_")
                        samples.append({"path": filepath, "label": label})
                except Exception:
                    continue

        return samples

    def parse_sample(self, info: Dict) -> Tuple[Optional[np.ndarray], str]:
        try:
            data = np.loadtxt(info["path"], delimiter=None)
        except Exception:
            try:
                data = np.loadtxt(info["path"], delimiter=',')
            except Exception:
                return None, ""

        if data.ndim == 1:
            data = data.reshape(1, -1)

        n_cols = data.shape[1]

        if n_cols == 66:
            skeleton = data.reshape(-1, 22, 3)
            skeleton = remap_joints(skeleton, 22, self.JOINTS_TO_KEEP)
        elif n_cols == 63:
            skeleton = data.reshape(-1, 21, 3)
        else:
            return None, ""

        return skeleton, info["label"]


# ═══════════════════════════════════════════════════════════════════════════════
# PARSER 4: MediaPipe CSV (Pre-extracted)
# ═══════════════════════════════════════════════════════════════════════════════

class MediaPipeCSVParser(DatasetParser):
    """
    Parser cho CSV files chứa MediaPipe hand landmarks đã trích xuất sẵn.

    Hỗ trợ 2 format:
      A) Cùng format raw_data.csv: [sample_id, frame_index, label, x0..z20]
      B) Flat format: [label, x0, y0, z0, x1, ..., z20]  (1 row = 1 frame)
    """

    def __init__(self, root_dir: str):
        super().__init__("MediaPipeCSV", root_dir)

    def discover(self) -> List[Dict]:
        samples = []
        csv_files = glob.glob(os.path.join(self.root_dir, "**", "*.csv"), recursive=True)

        for csv_path in csv_files:
            try:
                df = pd.read_csv(csv_path, nrows=5)
                cols = list(df.columns)

                # Detect format
                if 'sample_id' in cols and 'frame_index' in cols and 'label' in cols:
                    samples.append({"path": csv_path, "format": "sequential"})
                elif 'label' in cols and len(cols) >= 64:
                    samples.append({"path": csv_path, "format": "flat"})
                elif len(cols) >= 63:
                    # Giả sử cột cuối hoặc đầu là label
                    samples.append({"path": csv_path, "format": "unknown"})
            except Exception:
                continue

        return samples

    def parse_sample(self, info: Dict) -> Tuple[Optional[np.ndarray], str]:
        # Hàm này không dùng — xử lý batch trong process_all
        return None, ""

    def process_all(self) -> List[Dict]:
        """Override process_all vì CSV cần xử lý batch."""
        csv_files = self.discover()
        if not csv_files:
            return []

        self.logger.info(f"Phát hiện {len(csv_files)} CSV files")
        results = []

        for file_info in progress_bar(csv_files, desc=f"[{self.name}]"):
            try:
                df = pd.read_csv(file_info["path"])
                fmt = file_info["format"]

                if fmt == "sequential":
                    results.extend(self._parse_sequential(df))
                elif fmt == "flat":
                    results.extend(self._parse_flat(df))
                else:
                    # Thử sequential trước, rồi flat
                    parsed = self._parse_sequential(df)
                    if not parsed:
                        parsed = self._parse_flat(df)
                    results.extend(parsed)

            except Exception as e:
                self.logger.debug(f"Error parsing {file_info['path']}: {e}")

        self.logger.info(f"{self.name}: {len(results)} samples parsed")
        return results

    def _parse_sequential(self, df: pd.DataFrame) -> List[Dict]:
        """Parse format [sample_id, frame_index, label, x0..z20]."""
        results = []

        if 'sample_id' not in df.columns:
            return results

        meta_cols = ['sample_id', 'frame_index', 'label']
        feature_cols = [c for c in df.columns if c not in meta_cols and c != 'source']

        if len(feature_cols) < 63:
            return results

        for sample_id, group in df.groupby('sample_id'):
            group_sorted = group.sort_values('frame_index')
            label = str(group_sorted['label'].iloc[0])
            features = group_sorted[feature_cols[:63]].values.astype(np.float32)

            if features.shape[0] < 2:
                continue

            skeleton = features.reshape(-1, TARGET_JOINTS, TARGET_COORDS)
            resampled = temporal_resample(skeleton, TARGET_SEQ_LEN)
            normalized = normalize_skeleton(resampled)

            is_valid, _ = validate_sequence(normalized, label)
            if is_valid:
                results.append({"sequence": normalized, "label": label})

        return results

    def _parse_flat(self, df: pd.DataFrame) -> List[Dict]:
        """Parse format [label, x0, y0, z0, ...]  (1 row = 1 static pose)."""
        results = []

        if 'label' not in df.columns:
            return results

        feature_cols = [c for c in df.columns if c != 'label']
        if len(feature_cols) < 63:
            return results

        labels = df['label'].values
        features = df[feature_cols[:63]].values.astype(np.float32)

        # Flat format = static poses → mỗi sample = 1 frame repeat 15 lần
        for i in range(len(features)):
            frame = features[i].reshape(1, TARGET_JOINTS, TARGET_COORDS)
            sequence = np.tile(frame, (TARGET_SEQ_LEN, 1, 1))
            normalized = normalize_skeleton(sequence)

            label = str(labels[i])
            is_valid, _ = validate_sequence(normalized, label)
            if is_valid:
                results.append({"sequence": normalized, "label": label})

        return results


# ═══════════════════════════════════════════════════════════════════════════════
# PARSER 5: MediaPipe JSON
# ═══════════════════════════════════════════════════════════════════════════════

class MediaPipeJSONParser(DatasetParser):
    """
    Parser cho JSON files chứa MediaPipe hand landmarks.

    Hỗ trợ format:
      {"label": "grab", "frames": [[x0,y0,z0,...], ...]}
    hoặc:
      [{"label": "grab", "landmarks": [{"x":..., "y":..., "z":...}, ...]}]
    """

    def __init__(self, root_dir: str):
        super().__init__("MediaPipeJSON", root_dir)

    def discover(self) -> List[Dict]:
        json_files = glob.glob(os.path.join(self.root_dir, "**", "*.json"), recursive=True)
        return [{"path": p} for p in json_files]

    def parse_sample(self, info: Dict) -> Tuple[Optional[np.ndarray], str]:
        with open(info["path"], 'r', encoding='utf-8') as f:
            data = json.load(f)

        if isinstance(data, dict):
            return self._parse_single_dict(data)
        elif isinstance(data, list):
            # Trả về sample đầu tiên (process_all sẽ loop)
            if data and isinstance(data[0], dict):
                return self._parse_single_dict(data[0])
        return None, ""

    def _parse_single_dict(self, d: Dict) -> Tuple[Optional[np.ndarray], str]:
        label = str(d.get("label", d.get("gesture", d.get("class", "unknown"))))

        # Try different key names for frames
        for key in ["frames", "landmarks", "keypoints", "skeleton", "data"]:
            if key in d:
                frames_data = d[key]
                break
        else:
            return None, ""

        try:
            arr = np.array(frames_data, dtype=np.float32)
            if arr.ndim == 2 and arr.shape[1] == 63:
                skeleton = arr.reshape(-1, 21, 3)
            elif arr.ndim == 3 and arr.shape[1:] == (21, 3):
                skeleton = arr
            else:
                return None, ""
            return skeleton, label
        except Exception:
            return None, ""

    def process_all(self) -> List[Dict]:
        """Override: mỗi JSON file có thể chứa nhiều samples."""
        json_files = self.discover()
        if not json_files:
            return []

        self.logger.info(f"Phát hiện {len(json_files)} JSON files")
        results = []

        for file_info in progress_bar(json_files, desc=f"[{self.name}]"):
            try:
                with open(file_info["path"], 'r', encoding='utf-8') as f:
                    data = json.load(f)

                items = data if isinstance(data, list) else [data]

                for item in items:
                    if not isinstance(item, dict):
                        continue
                    seq, label = self._parse_single_dict(item)
                    if seq is not None:
                        resampled = temporal_resample(seq, TARGET_SEQ_LEN)
                        normalized = normalize_skeleton(resampled)
                        is_valid, _ = validate_sequence(normalized, label)
                        if is_valid:
                            results.append({"sequence": normalized, "label": label})

            except Exception as e:
                self.logger.debug(f"Error parsing {file_info['path']}: {e}")

        self.logger.info(f"{self.name}: {len(results)} samples parsed")
        return results


# ═══════════════════════════════════════════════════════════════════════════════
# PARSER 6: Raw Video → MediaPipe Extraction
# ═══════════════════════════════════════════════════════════════════════════════

class RawVideoParser(DatasetParser):
    """
    Parser cho video files (.mp4, .avi, .mov, .mkv).

    Pipeline: Video → OpenCV → MediaPipe Hands → 21 landmarks → resample.
    Label được infer từ tên file hoặc thư mục cha.
    """

    VIDEO_EXTENSIONS = {'.mp4', '.avi', '.mov', '.mkv', '.webm'}

    def __init__(self, root_dir: str):
        super().__init__("RawVideo", root_dir)
        self.hands = None

    def _init_mediapipe(self):
        """Lazy init MediaPipe (tránh load nếu không cần)."""
        if not HAS_MEDIAPIPE:
            raise ImportError("mediapipe required for video processing")
        if self.hands is None:
            self.hands = mp.solutions.hands.Hands(
                static_image_mode=False,
                max_num_hands=1,
                min_detection_confidence=0.5,
                min_tracking_confidence=0.5,
                model_complexity=0,  # Nhanh hơn
            )

    def discover(self) -> List[Dict]:
        samples = []
        for ext in self.VIDEO_EXTENSIONS:
            pattern = os.path.join(self.root_dir, "**", f"*{ext}")
            for filepath in glob.glob(pattern, recursive=True):
                # Label = parent folder name hoặc filename
                parent_name = Path(filepath).parent.name
                file_stem = Path(filepath).stem

                # Ưu tiên folder name (thường = gesture class)
                label = parent_name.lower().replace(" ", "_").replace("-", "_")
                if label in ["videos", "data", "downloads", "raw"]:
                    label = file_stem.lower().replace(" ", "_").replace("-", "_")

                samples.append({"path": filepath, "label": label})

        return samples

    def parse_sample(self, info: Dict) -> Tuple[Optional[np.ndarray], str]:
        self._init_mediapipe()

        cap = cv2.VideoCapture(info["path"])
        if not cap.isOpened():
            return None, ""

        frames = []
        max_frames = 300  # Giới hạn tránh video quá dài
        frame_count = 0
        no_hand_count = 0

        try:
            while cap.isOpened() and frame_count < max_frames:
                ret, frame = cap.read()
                if not ret:
                    break

                frame_count += 1

                # Chỉ xử lý mỗi 2 frame (tăng tốc)
                if frame_count % 2 != 0:
                    continue

                rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                results = self.hands.process(rgb)

                if results.multi_hand_landmarks:
                    hand = results.multi_hand_landmarks[0]
                    coords = []
                    for lm in hand.landmark:
                        coords.extend([lm.x, lm.y, lm.z])
                    frames.append(coords)
                    no_hand_count = 0
                else:
                    no_hand_count += 1
                    # Nếu mất tay > 10 frame liên tiếp → dừng
                    if no_hand_count > 10 and frames:
                        break

        finally:
            cap.release()

        if len(frames) < 3:
            return None, ""

        skeleton = np.array(frames, dtype=np.float32).reshape(-1, 21, 3)
        return skeleton, info["label"]

    def cleanup(self):
        """Giải phóng MediaPipe resources."""
        if self.hands:
            self.hands.close()
            self.hands = None


# ═══════════════════════════════════════════════════════════════════════════════
# PARSER 7: Existing raw_data.csv
# ═══════════════════════════════════════════════════════════════════════════════

class ExistingCSVParser(DatasetParser):
    """
    Parser cho file raw_data.csv hiện tại (synthetic hoặc DHG đã convert).
    Đọc trực tiếp và thêm vào mega dataset.
    """

    def __init__(self, csv_path: str):
        super().__init__("ExistingCSV", os.path.dirname(csv_path))
        self.csv_path = csv_path

    def discover(self) -> List[Dict]:
        if os.path.isfile(self.csv_path):
            return [{"path": self.csv_path}]
        return []

    def parse_sample(self, info: Dict) -> Tuple[Optional[np.ndarray], str]:
        return None, ""

    def process_all(self) -> List[Dict]:
        """Đọc trực tiếp từ CSV đã chuẩn."""
        if not os.path.isfile(self.csv_path):
            return []

        df = pd.read_csv(self.csv_path)
        self.logger.info(f"Đọc {len(df)} rows từ {self.csv_path}")

        meta_cols = ['sample_id', 'frame_index', 'label']
        feature_cols = [c for c in df.columns if c not in meta_cols and c != 'source']

        if len(feature_cols) < 63:
            self.logger.warning(f"Not enough features: {len(feature_cols)}")
            return []

        results = []
        for sample_id, group in df.groupby('sample_id'):
            group_sorted = group.sort_values('frame_index')
            label = str(group_sorted['label'].iloc[0])
            features = group_sorted[feature_cols[:63]].values.astype(np.float32)

            if features.shape[0] != TARGET_SEQ_LEN:
                # Resample
                skeleton = features.reshape(-1, TARGET_JOINTS, TARGET_COORDS)
                features = temporal_resample(skeleton, TARGET_SEQ_LEN)
                features = features.reshape(TARGET_SEQ_LEN, -1)
            else:
                features = features.reshape(TARGET_SEQ_LEN, TARGET_JOINTS, TARGET_COORDS)
                features = features  # Đã đúng format

            if features.shape == (TARGET_SEQ_LEN, TARGET_JOINTS, TARGET_COORDS):
                results.append({"sequence": features, "label": label})
            else:
                results.append({
                    "sequence": features.reshape(TARGET_SEQ_LEN, TARGET_JOINTS, TARGET_COORDS),
                    "label": label
                })

        self.logger.info(f"ExistingCSV: {len(results)} samples loaded")
        return results


# ═══════════════════════════════════════════════════════════════════════════════
# AGGREGATOR — PIPELINE CHÍNH
# ═══════════════════════════════════════════════════════════════════════════════

class UniversalDatasetBuilder:
    """
    Bộ điều phối chính — tự động phát hiện và xử lý tất cả dataset.

    Pipeline:
      1. Quét Dataset/Downloads/ để phát hiện format
      2. Khởi tạo parser phù hợp cho từng thư mục con
      3. Xử lý song song (hoặc tuần tự) tất cả dataset
      4. Hợp nhất thành mega_raw_data.csv
      5. In báo cáo thống kê chi tiết
    """

    def __init__(self, download_dir: str, output_csv: str, existing_csv: str = None):
        self.download_dir = download_dir
        self.output_csv = output_csv
        self.existing_csv = existing_csv
        self.parsers: List[DatasetParser] = []
        self.all_results = []
        self.logger = logging.getLogger("Builder")

    def auto_detect_parsers(self):
        """
        Tự động phát hiện format dataset dựa trên cấu trúc thư mục.
        """
        self.logger.info(f"\n{'='*65}")
        self.logger.info(f"  AUTO-DETECT: Quét {self.download_dir}")
        self.logger.info(f"{'='*65}")

        os.makedirs(self.download_dir, exist_ok=True)

        # ── 1. Existing raw_data.csv ─────────────────────────────────────
        if self.existing_csv and os.path.isfile(self.existing_csv):
            self.parsers.append(ExistingCSVParser(self.existing_csv))
            self.logger.info(f"  [+] ExistingCSV: {self.existing_csv}")

        # ── 2. Quét thư mục Downloads ────────────────────────────────────
        if not os.path.isdir(self.download_dir):
            self.logger.info(f"  [--] Downloads directory empty or missing")
            return

        for item_name in sorted(os.listdir(self.download_dir)):
            item_path = os.path.join(self.download_dir, item_name)

            if not os.path.isdir(item_path) and not item_name.endswith('.csv'):
                continue

            item_lower = item_name.lower()

            # ── DHG-14/28 detection ──────────────────────────────────
            if any(x in item_lower for x in ["dhg", "gesture_1"]):
                # Kiểm tra có gesture_*/finger_*/subject_*/essai_*/skeleton_world.txt
                test_path = os.path.join(item_path, "gesture_1")
                if os.path.isdir(test_path) or os.path.isdir(item_path):
                    self.parsers.append(DHGParser(item_path))
                    self.logger.info(f"  [+] DHGParser: {item_name}")
                    continue

            # ── SHREC 2017 detection ─────────────────────────────────
            if "shrec" in item_lower:
                self.parsers.append(DHGParser(item_path))
                self.logger.info(f"  [+] DHGParser (SHREC): {item_name}")
                continue

            # ── FPHA detection ───────────────────────────────────────
            if "fpha" in item_lower or "first_person" in item_lower:
                self.parsers.append(FPHAParser(item_path))
                self.logger.info(f"  [+] FPHAParser: {item_name}")
                continue

            # ── Briareo detection ────────────────────────────────────
            if "briareo" in item_lower:
                self.parsers.append(BriareoParser(item_path))
                self.logger.info(f"  [+] BriareoParser: {item_name}")
                continue

            # ── Video detection ──────────────────────────────────────
            if "video" in item_lower or any(
                glob.glob(os.path.join(item_path, f"**/*{ext}"), recursive=True)
                for ext in ['.mp4', '.avi']
            ):
                if HAS_MEDIAPIPE:
                    self.parsers.append(RawVideoParser(item_path))
                    self.logger.info(f"  [+] RawVideoParser: {item_name}")
                else:
                    self.logger.warning(f"  [--] Skipping video dir (no mediapipe): {item_name}")
                continue

            # ── MediaPipe CSV detection ──────────────────────────────
            csv_files = glob.glob(os.path.join(item_path, "**", "*.csv"), recursive=True)
            if csv_files:
                self.parsers.append(MediaPipeCSVParser(item_path))
                self.logger.info(f"  [+] MediaPipeCSVParser: {item_name}")
                continue

            # ── JSON detection ───────────────────────────────────────
            json_files = glob.glob(os.path.join(item_path, "**", "*.json"), recursive=True)
            if json_files:
                self.parsers.append(MediaPipeJSONParser(item_path))
                self.logger.info(f"  [+] MediaPipeJSONParser: {item_name}")
                continue

            # ── Generic skeleton .txt detection ──────────────────────
            txt_files = glob.glob(os.path.join(item_path, "**", "*.txt"), recursive=True)
            if txt_files:
                self.parsers.append(BriareoParser(item_path))
                self.logger.info(f"  [+] BriareoParser (generic): {item_name}")

            # ── Standalone CSV file in Downloads ─────────────────────
            if item_name.endswith('.csv') and os.path.isfile(item_path):
                self.parsers.append(MediaPipeCSVParser(os.path.dirname(item_path)))
                self.logger.info(f"  [+] MediaPipeCSVParser (file): {item_name}")

        self.logger.info(f"\n  Tổng: {len(self.parsers)} parsers detected")

    def build(self):
        """
        Chạy toàn bộ pipeline:
          detect → parse all → merge → save → report.
        """
        start_time = time.time()

        self.logger.info(f"\n{'='*65}")
        self.logger.info(f"  UNIVERSAL DATASET BUILDER")
        self.logger.info(f"  Xay dung mega dataset thong nhat tu nhieu nguon")
        self.logger.info(f"{'='*65}")
        self.logger.info(f"  Downloads : {self.download_dir}")
        self.logger.info(f"  Output    : {self.output_csv}")
        self.logger.info(f"  Seq Length: {TARGET_SEQ_LEN}")
        self.logger.info(f"  Features  : {TARGET_FEATURES}")
        self.logger.info(f"{'='*65}")

        # ── Step 1: Auto-detect parsers ──────────────────────────────────
        self.auto_detect_parsers()

        if not self.parsers:
            self.logger.warning(
                "\n  [WARN] Khong tim thay dataset nao!\n"
                f"  Dat dataset vao: {self.download_dir}\n"
                "  Hoac dam bao raw_data.csv ton tai."
            )
            return None

        # ── Step 2: Process all parsers ──────────────────────────────────
        self.logger.info(f"\n{'─'*65}")
        self.logger.info(f"  PROCESSING {len(self.parsers)} DATASETS...")
        self.logger.info(f"{'─'*65}")

        source_stats = {}

        for parser in self.parsers:
            self.logger.info(f"\n  >>> {parser.name} ({parser.root_dir})")
            try:
                results = parser.process_all()
                for r in results:
                    r["source"] = parser.name
                self.all_results.extend(results)
                source_stats[parser.name] = len(results)
            except Exception as e:
                self.logger.error(f"  [ERROR] {parser.name}: {e}")
                self.logger.debug(traceback.format_exc())
                source_stats[parser.name] = 0

            # Cleanup nếu là video parser
            if hasattr(parser, 'cleanup'):
                parser.cleanup()

        # ── Step 3: Build DataFrame ──────────────────────────────────────
        if not self.all_results:
            self.logger.error("\n  [LOI] Khong xu ly duoc sample nao!")
            return None

        self.logger.info(f"\n{'─'*65}")
        self.logger.info(f"  MERGING {len(self.all_results)} SAMPLES...")
        self.logger.info(f"{'─'*65}")

        all_rows = []
        for sample_id, result in enumerate(self.all_results):
            seq = result["sequence"]       # (15, 21, 3)
            label = result["label"]
            source = result.get("source", "unknown")

            flat_frames = seq.reshape(TARGET_SEQ_LEN, -1)  # (15, 63)

            for frame_idx in range(TARGET_SEQ_LEN):
                row = [sample_id, frame_idx, label, source] + \
                      flat_frames[frame_idx].tolist()
                all_rows.append(row)

        df = pd.DataFrame(all_rows, columns=OUTPUT_COLUMNS)

        # ── Step 4: Save ─────────────────────────────────────────────────
        os.makedirs(os.path.dirname(self.output_csv), exist_ok=True)
        df.to_csv(self.output_csv, index=False)

        elapsed = time.time() - start_time

        # ── Step 5: Report ───────────────────────────────────────────────
        self.logger.info(f"\n{'='*65}")
        self.logger.info(f"  BUILD COMPLETE")
        self.logger.info(f"{'='*65}")
        self.logger.info(f"  Total samples   : {len(self.all_results)}")
        self.logger.info(f"  Total rows      : {len(df)}")
        self.logger.info(f"  Unique labels   : {df['label'].nunique()}")
        self.logger.info(f"  Unique sources  : {df['source'].nunique()}")
        self.logger.info(f"  CSV columns     : {len(df.columns)}")
        self.logger.info(f"  File size       : {os.path.getsize(self.output_csv)/(1024*1024):.1f} MB")
        self.logger.info(f"  Time elapsed    : {elapsed:.1f}s")
        self.logger.info(f"  Output          : {self.output_csv}")

        # Per-source breakdown
        self.logger.info(f"\n  Source Breakdown:")
        for source, count in sorted(source_stats.items()):
            bar = "█" * min(count // 20 + 1, 40)
            self.logger.info(f"    {source:25s}: {count:6d} samples  {bar}")

        # Per-label breakdown
        self.logger.info(f"\n  Label Distribution:")
        label_counts = df[df["frame_index"] == 0].groupby("label").size().sort_values(ascending=False)
        for label in label_counts.index:
            count = label_counts[label]
            bar = "█" * min(count // 20 + 1, 40)
            self.logger.info(f"    {label:25s}: {count:6d} samples  {bar}")

        # Validation
        self.logger.info(f"\n  Validation:")
        feature_cols = [c for c in df.columns if c not in ['sample_id', 'frame_index', 'label', 'source']]
        vals = df[feature_cols].values
        self.logger.info(f"    Feature range: [{vals.min():.4f}, {vals.max():.4f}]")
        self.logger.info(f"    NaN count    : {df.isna().sum().sum()}")
        self.logger.info(f"    Inf count    : {np.isinf(vals).sum()}")

        sample_0 = df[df["sample_id"] == 0]
        self.logger.info(f"    Sample 0     : {len(sample_0)} frames, "
                         f"label='{sample_0['label'].iloc[0]}', "
                         f"source='{sample_0['source'].iloc[0]}'")

        self.logger.info(f"\n{'='*65}")
        self.logger.info(f"  mega_raw_data.csv san sang!")
        self.logger.info(f"  Dung cho training: thay DATA_PATH trong notebooks")
        self.logger.info(f"{'='*65}\n")

        return df


# ═══════════════════════════════════════════════════════════════════════════════
# ENTRY POINT
# ═══════════════════════════════════════════════════════════════════════════════

def main():
    setup_logging()

    builder = UniversalDatasetBuilder(
        download_dir=DOWNLOAD_DIR,
        output_csv=OUTPUT_CSV,
        existing_csv=EXISTING_CSV,
    )

    df = builder.build()

    if df is not None:
        print(f"\n  ✅ mega_raw_data.csv created: {len(df)} rows")
    else:
        print(f"\n  ❌ Build failed. Check logs at: {LOG_FILE}")


if __name__ == "__main__":
    main()
