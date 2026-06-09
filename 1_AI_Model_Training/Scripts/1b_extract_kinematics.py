"""
=============================================================================
  1b_extract_kinematics.py — Trích xuất Đặc trưng Động học (Kinematics)
=============================================================================
  Dự án  : Dynamic Spatial Hand Gesture Recognition
  Mục đích: Giải quyết vấn đề Dynamic Time Warping và Speed Variation
            bằng cách bổ sung các đặc trưng vật lý (velocity, acceleration,
            inter-landmark distances) vào dataset gốc.

  Input  : Dataset/raw_data.csv        (63 features/frame)
  Output : Dataset/kinematic_data.csv  (63 + N_new features/frame)

  Hướng dẫn chạy:
    cd D:\WORKSAPCE\Monitor_AR\1_AI_Model_Training
    python Scripts/1b_extract_kinematics.py

  Đặc trưng mới được tạo:
  ┌──────────────────────────────────────────────────────────────────────┐
  │  NHÓM 1: VẬN TỐC (VELOCITY) — Δx, Δy, Δz giữa frame liên tiếp   │
  │  • Wrist (Landmark 0):        vx0, vy0, vz0                        │
  │  • Thumb Tip (Landmark 4):    vx4, vy4, vz4                        │
  │  • Index Tip (Landmark 8):    vx8, vy8, vz8                        │
  │  • Middle Tip (Landmark 12):  vx12, vy12, vz12                     │
  │  • Ring Tip (Landmark 16):    vx16, vy16, vz16                     │
  │  • Pinky Tip (Landmark 20):   vx20, vy20, vz20                     │
  │  → 6 landmarks × 3 coords = 18 features                           │
  │                                                                    │
  │  NHÓM 2: GIA TỐC (ACCELERATION) — ΔΔ giữa velocity liên tiếp     │
  │  • Wrist:     ax0, ay0, az0                                        │
  │  • Index Tip: ax8, ay8, az8                                        │
  │  → 2 landmarks × 3 coords = 6 features                            │
  │                                                                    │
  │  NHÓM 3: KHOẢNG CÁCH INTER-LANDMARK                               │
  │  • dist_thumb_index:  ||L4 - L8||  (Euclidean)  → encode Pinch     │
  │  • dist_thumb_middle: ||L4 - L12|| → encode Grab/Expand            │
  │  • dist_index_wrist:  ||L8 - L0||  → encode Tap amplitude          │
  │  • dist_pinky_thumb:  ||L20 - L4|| → encode hand openness          │
  │  → 4 features                                                      │
  │                                                                    │
  │  NHÓM 4: TỐC ĐỘ TỔNG HỢP (SPEED MAGNITUDE)                       │
  │  • speed_wrist:  ||v_wrist||       → tổng tốc độ di chuyển tay     │
  │  • speed_index:  ||v_index_tip||   → tốc độ ngón trỏ               │
  │  → 2 features                                                      │
  │                                                                    │
  │  NHÓM 5: GÓC NGÓN TAY                                              │
  │  • angle_thumb_index: góc giữa ngón cái và ngón trỏ                │
  │  • angle_index_middle: góc giữa ngón trỏ và ngón giữa              │
  │  → 2 features                                                      │
  │                                                                    │
  │  TỔNG: 63 (gốc) + 18 + 6 + 4 + 2 + 2 = 95 features/frame         │
  └──────────────────────────────────────────────────────────────────────┘
=============================================================================
"""

import os
import sys
import numpy as np
import pandas as pd
from pathlib import Path

# ── Cấu hình ────────────────────────────────────────────────────────────────
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
INPUT_CSV = os.path.join(PROJECT_ROOT, "Dataset", "raw_data.csv")
OUTPUT_CSV = os.path.join(PROJECT_ROOT, "Dataset", "kinematic_data.csv")

NUM_LANDMARKS = 21
NUM_COORDS = 3
SEQ_LEN = 15

# Landmark indices cho các đặc trưng velocity
VELOCITY_LANDMARKS = [0, 4, 8, 12, 16, 20]  # Wrist + 5 fingertips
VELOCITY_NAMES = ["wrist", "thumb_tip", "index_tip", "middle_tip", "ring_tip", "pinky_tip"]

# Landmark indices cho acceleration (chỉ wrist + index)
ACCEL_LANDMARKS = [0, 8]
ACCEL_NAMES = ["wrist", "index_tip"]


# ═══════════════════════════════════════════════════════════════════════════════
# HÀM TÍNH TOÁN ĐẶC TRƯNG
# ═══════════════════════════════════════════════════════════════════════════════

def compute_velocity(frames, landmark_idx):
    """
    Tính vận tốc Δ(x,y,z) giữa frame liên tiếp cho 1 landmark.
    
    Args:
        frames: np.array shape (15, 63) — chuỗi 15 frame
        landmark_idx: index của landmark (0-20)
    
    Returns:
        np.array shape (15, 3) — velocity cho mỗi frame
        Frame 0 có velocity = 0 (không có frame trước đó)
    """
    # Trích tọa độ landmark: cột 3*idx, 3*idx+1, 3*idx+2
    col_start = landmark_idx * 3
    coords = frames[:, col_start:col_start + 3]  # (15, 3)
    
    # Velocity = diff giữa frame liên tiếp
    velocity = np.zeros_like(coords)
    velocity[1:] = coords[1:] - coords[:-1]  # Δ frame_t - frame_{t-1}
    
    return velocity


def compute_acceleration(velocity):
    """
    Tính gia tốc = Δvelocity giữa frame liên tiếp.
    
    Args:
        velocity: np.array shape (15, 3)
    
    Returns:
        np.array shape (15, 3)
    """
    accel = np.zeros_like(velocity)
    accel[1:] = velocity[1:] - velocity[:-1]
    return accel


def compute_distance(frames, lm_a, lm_b):
    """
    Tính khoảng cách Euclidean giữa 2 landmark tại mỗi frame.
    
    Args:
        frames: np.array shape (15, 63)
        lm_a, lm_b: indices của 2 landmark
    
    Returns:
        np.array shape (15,) — khoảng cách tại mỗi frame
    """
    a_start = lm_a * 3
    b_start = lm_b * 3
    
    coords_a = frames[:, a_start:a_start + 3]  # (15, 3)
    coords_b = frames[:, b_start:b_start + 3]  # (15, 3)
    
    diff = coords_a - coords_b
    distance = np.linalg.norm(diff, axis=1)  # (15,)
    
    return distance


def compute_speed_magnitude(velocity):
    """
    Tính tốc độ tổng hợp (scalar) = ||velocity||.
    
    Args:
        velocity: np.array shape (15, 3)
    
    Returns:
        np.array shape (15,)
    """
    return np.linalg.norm(velocity, axis=1)


def compute_angle_between_fingers(frames, base_lm, tip_a, tip_b):
    """
    Tính góc (radian) giữa 2 vector: (base→tip_a) và (base→tip_b).
    Hữu ích để encode mức độ mở/đóng của ngón tay.
    
    Args:
        frames: (15, 63)
        base_lm: landmark gốc (thường là wrist = 0)
        tip_a, tip_b: 2 landmark đầu ngón
    
    Returns:
        np.array shape (15,) — góc (radian) tại mỗi frame
    """
    base = frames[:, base_lm*3:base_lm*3+3]
    a = frames[:, tip_a*3:tip_a*3+3]
    b = frames[:, tip_b*3:tip_b*3+3]
    
    vec_a = a - base  # Vector từ base đến tip_a
    vec_b = b - base  # Vector từ base đến tip_b
    
    # cos(angle) = (vec_a · vec_b) / (||vec_a|| × ||vec_b||)
    dot = np.sum(vec_a * vec_b, axis=1)
    norm_a = np.linalg.norm(vec_a, axis=1)
    norm_b = np.linalg.norm(vec_b, axis=1)
    
    # Tránh chia cho 0
    denom = norm_a * norm_b
    denom = np.where(denom < 1e-8, 1e-8, denom)
    
    cos_angle = np.clip(dot / denom, -1.0, 1.0)
    angle = np.arccos(cos_angle)
    
    return angle


# ═══════════════════════════════════════════════════════════════════════════════
# PIPELINE XỬ LÝ CHÍNH
# ═══════════════════════════════════════════════════════════════════════════════

def process_sample(frames):
    """
    Tính toán tất cả đặc trưng kinematic cho 1 sample (15 frames).
    
    Args:
        frames: np.array shape (15, 63) — dữ liệu gốc
    
    Returns:
        np.array shape (15, N_new) — đặc trưng kinematic mới
    """
    new_features = []
    new_col_names = []
    
    # ── NHÓM 1: VELOCITY (18 features) ───────────────────────────────────
    velocities = {}
    for lm_idx, lm_name in zip(VELOCITY_LANDMARKS, VELOCITY_NAMES):
        vel = compute_velocity(frames, lm_idx)  # (15, 3)
        velocities[lm_idx] = vel
        new_features.append(vel)
        for axis in ['x', 'y', 'z']:
            new_col_names.append(f"v{axis}_{lm_name}")
    
    # ── NHÓM 2: ACCELERATION (6 features) ────────────────────────────────
    for lm_idx, lm_name in zip(ACCEL_LANDMARKS, ACCEL_NAMES):
        vel = velocities[lm_idx]
        acc = compute_acceleration(vel)  # (15, 3)
        new_features.append(acc)
        for axis in ['x', 'y', 'z']:
            new_col_names.append(f"a{axis}_{lm_name}")
    
    # ── NHÓM 3: KHOẢNG CÁCH INTER-LANDMARK (4 features) ─────────────────
    distance_pairs = [
        (4, 8, "dist_thumb_index"),      # Pinch encoding
        (4, 12, "dist_thumb_middle"),     # Grab/Expand encoding
        (8, 0, "dist_index_wrist"),       # Tap amplitude
        (20, 4, "dist_pinky_thumb"),      # Hand openness
    ]
    
    for lm_a, lm_b, name in distance_pairs:
        dist = compute_distance(frames, lm_a, lm_b)  # (15,)
        new_features.append(dist.reshape(-1, 1))
        new_col_names.append(name)
    
    # ── NHÓM 4: TỐC ĐỘ TỔNG HỢP (2 features) ──────────────────────────
    speed_landmarks = [(0, "speed_wrist"), (8, "speed_index")]
    for lm_idx, name in speed_landmarks:
        vel = velocities[lm_idx]
        speed = compute_speed_magnitude(vel)  # (15,)
        new_features.append(speed.reshape(-1, 1))
        new_col_names.append(name)
    
    # ── NHÓM 5: GÓC NGÓN TAY (2 features) ───────────────────────────────
    angle_configs = [
        (0, 4, 8, "angle_thumb_index"),     # Góc ngón cái - trỏ
        (0, 8, 12, "angle_index_middle"),    # Góc ngón trỏ - giữa
    ]
    for base, tip_a, tip_b, name in angle_configs:
        angle = compute_angle_between_fingers(frames, base, tip_a, tip_b)  # (15,)
        new_features.append(angle.reshape(-1, 1))
        new_col_names.append(name)
    
    # ── Ghép tất cả ──────────────────────────────────────────────────────
    kinematic_matrix = np.hstack(new_features)  # (15, N_new)
    
    return kinematic_matrix, new_col_names


def main():
    print(f"\n{'='*65}")
    print(f"  KINEMATIC FEATURE ENGINEERING")
    print(f"  Trich xuat dac trung dong hoc tu du lieu 3D landmarks")
    print(f"{'='*65}")
    print(f"  Input  : {INPUT_CSV}")
    print(f"  Output : {OUTPUT_CSV}")
    print(f"{'='*65}\n")
    
    # ── Đọc dữ liệu gốc ────────────────────────────────────────────────
    if not os.path.isfile(INPUT_CSV):
        print(f"  [LOI] Khong tim thay: {INPUT_CSV}")
        print(f"        Chay Scripts/2_generate_synthetic_data.py truoc!")
        sys.exit(1)
    
    df = pd.read_csv(INPUT_CSV)
    print(f"  DataFrame goc: {df.shape}")
    
    meta_cols = ['sample_id', 'frame_index', 'label']
    feature_cols = [c for c in df.columns if c not in meta_cols]
    
    assert len(feature_cols) == 63, f"Ky vong 63 features, nhan {len(feature_cols)}"
    
    n_samples = df['sample_id'].nunique()
    print(f"  So samples : {n_samples}")
    print(f"  So lop     : {df['label'].nunique()}")
    
    # ── Xử lý từng sample ───────────────────────────────────────────────
    print(f"\n  Dang trich xuat dac trung kinematic...")
    
    all_new_rows = []
    new_col_names = None
    errors = 0
    
    for i, (sample_id, group) in enumerate(df.groupby('sample_id')):
        group_sorted = group.sort_values('frame_index')
        
        if len(group_sorted) != SEQ_LEN:
            errors += 1
            continue
        
        frames = group_sorted[feature_cols].values.astype(np.float32)  # (15, 63)
        
        try:
            kinematic, col_names = process_sample(frames)
            
            if new_col_names is None:
                new_col_names = col_names
            
            all_new_rows.append(kinematic)
        except Exception as e:
            print(f"  [WARN] Sample {sample_id}: {e}")
            errors += 1
        
        # Progress
        if (i + 1) % 500 == 0 or i == n_samples - 1:
            pct = (i + 1) / n_samples * 100
            bar = "█" * int(pct // 2.5) + "░" * (40 - int(pct // 2.5))
            print(f"\r  [{bar}] {pct:5.1f}% ({i+1}/{n_samples})", end="", flush=True)
    
    print()
    
    if not all_new_rows:
        print(f"\n  [LOI] Khong xu ly duoc sample nao!")
        sys.exit(1)
    
    # ── Xây dựng DataFrame mới ───────────────────────────────────────────
    print(f"\n  Dang ghep du lieu...")
    
    # Stack kinematic features: (n_samples, 15, N_new) → flatten to rows
    kinematic_all = np.vstack([k for k in all_new_rows])  # (n_samples * 15, N_new)
    
    # Lọc df gốc (loại bỏ samples bị lỗi)
    valid_sample_ids = df.groupby('sample_id').filter(
        lambda x: len(x) == SEQ_LEN
    )['sample_id'].unique()[:len(all_new_rows)]
    
    df_valid = df[df['sample_id'].isin(valid_sample_ids)].sort_values(
        ['sample_id', 'frame_index']
    ).reset_index(drop=True)
    
    # Tạo DataFrame cho kinematic features
    df_kinematic = pd.DataFrame(kinematic_all, columns=new_col_names)
    
    # Ghép
    assert len(df_valid) == len(df_kinematic), \
        f"Shape mismatch: {len(df_valid)} vs {len(df_kinematic)}"
    
    df_result = pd.concat([df_valid.reset_index(drop=True), df_kinematic], axis=1)
    
    # ── Lưu ──────────────────────────────────────────────────────────────
    os.makedirs(os.path.dirname(OUTPUT_CSV), exist_ok=True)
    df_result.to_csv(OUTPUT_CSV, index=False)
    
    # ── Thống kê ─────────────────────────────────────────────────────────
    n_original = len(feature_cols)
    n_new = len(new_col_names)
    n_total = n_original + n_new
    
    print(f"\n{'='*65}")
    print(f"  HOAN TAT")
    print(f"{'='*65}")
    print(f"  Samples xu ly  : {len(all_new_rows)}")
    print(f"  Samples loi    : {errors}")
    print(f"  Features goc   : {n_original}")
    print(f"  Features moi   : {n_new}")
    print(f"  Tong features  : {n_total}")
    print(f"  Rows           : {len(df_result)}")
    print(f"  Columns        : {len(df_result.columns)}")
    print(f"  File size      : {os.path.getsize(OUTPUT_CSV) / (1024*1024):.1f} MB")
    print(f"  Saved to       : {OUTPUT_CSV}")
    print(f"{'='*65}")
    
    # Chi tiết features mới
    print(f"\n  Chi tiet dac trung moi ({n_new} features):")
    print(f"  {'─' * 50}")
    
    groups = {
        "Velocity (Δx,Δy,Δz)": [c for c in new_col_names if c.startswith('v')],
        "Acceleration (ΔΔ)":   [c for c in new_col_names if c.startswith('a')],
        "Inter-Landmark Dist": [c for c in new_col_names if c.startswith('dist')],
        "Speed Magnitude":     [c for c in new_col_names if c.startswith('speed')],
        "Finger Angles":       [c for c in new_col_names if c.startswith('angle')],
    }
    
    for group_name, cols in groups.items():
        print(f"    {group_name}: {len(cols)} features")
        for c in cols:
            vals = df_result[c]
            print(f"      {c:25s}  "
                  f"mean={vals.mean():+.4f}  "
                  f"std={vals.std():.4f}  "
                  f"range=[{vals.min():.4f}, {vals.max():.4f}]")
    
    # Kiểm tra dist_thumb_index (Pinch encoding)
    print(f"\n  Pinch Encoding Test (dist_thumb_index):")
    if 'dist_thumb_index' in df_result.columns:
        for label in sorted(df_result['label'].unique()):
            subset = df_result[df_result['label'] == label]
            mean_dist = subset['dist_thumb_index'].mean()
            std_dist = subset['dist_thumb_index'].std()
            bar = "█" * int(mean_dist * 100)
            print(f"    {label:15s}  mean={mean_dist:.4f} ± {std_dist:.4f}  {bar}")
    
    print()
    return df_result


if __name__ == "__main__":
    df = main()
