"""
=============================================================================
  3_convert_dhg_dataset.py - DHG-14/28 to raw_data.csv Converter
=============================================================================
  Project : Dynamic Spatial Hand Gesture Recognition
  Purpose : Parse the DHG-14/28 skeleton_world.txt files, remap joints
            (22 → 21), resample sequences (variable → 15 frames), normalize
            coordinates, and output a single raw_data.csv.
  
  Prerequisites:
    1. Download DHG-14/28 from: http://www-rech.telecom-lille.fr/DHGdataset/
       or search "DHG hand gesture 3D skeletal" on Kaggle.
    2. Extract to: Dataset/DHGdataset/
  
  Usage: python Scripts/3_convert_dhg_dataset.py
=============================================================================
"""

import os
import numpy as np
import pandas as pd
from scipy.interpolate import interp1d
import sys
from pathlib import Path

# ─── Configuration ───────────────────────────────────────────────────────────
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DHG_ROOT = os.path.join(PROJECT_ROOT, "Dataset", "DHGdataset")
OUTPUT_CSV = os.path.join(PROJECT_ROOT, "Dataset", "raw_data.csv")

TARGET_SEQ_LEN = 15
TARGET_JOINTS = 21
TARGET_COORDS = 3

# DHG-14/28 has 22 joints:
#   0: Palm Center
#   1: Wrist
#   2-5: Thumb (4 joints)
#   6-9: Index (4 joints)
#   10-13: Middle (4 joints)
#   14-17: Ring (4 joints)
#   18-21: Pinky (4 joints)
#
# We drop joint 0 (Palm Center) to get 21 joints matching MediaPipe:
#   0: Wrist
#   1-4: Thumb
#   5-8: Index
#   9-12: Middle
#   13-16: Ring
#   17-20: Pinky
JOINTS_TO_KEEP = list(range(1, 22))  # indices 1..21 (drop palm center at 0)

GESTURE_NAMES = {
    1: "grab",
    2: "tap",
    3: "expand",
    4: "pinch",
    5: "rotation_cw",
    6: "rotation_ccw",
    7: "swipe_right",
    8: "swipe_left",
    9: "swipe_up",
    10: "swipe_down",
    11: "swipe_x",
    12: "swipe_v",
    13: "swipe_plus",
    14: "shake",
}

# For 28-class mode, each gesture has two variants:
#   finger_1 = performed with one finger
#   finger_2 = performed with whole hand
USE_28_CLASSES = False  # Set True to distinguish finger/hand variants


# ─── Processing Functions ────────────────────────────────────────────────────

def load_skeleton_file(filepath):
    """
    Load a single skeleton_world.txt file.
    
    Each line contains 66 float values (22 joints × 3 coordinates).
    Returns: np.array of shape (num_frames, 22, 3)
    """
    try:
        data = np.loadtxt(filepath)
    except Exception as e:
        raise ValueError(f"Failed to parse {filepath}: {e}")
    
    if data.ndim == 1:
        # Single frame
        data = data.reshape(1, -1)
    
    num_frames = data.shape[0]
    expected_cols = 22 * 3
    
    if data.shape[1] != expected_cols:
        raise ValueError(
            f"Expected {expected_cols} columns but got {data.shape[1]} "
            f"in {filepath}"
        )
    
    return data.reshape(num_frames, 22, 3)


def select_joints(skeleton_seq, joint_indices):
    """
    Select specific joints from the skeleton sequence.
    
    Input:  (num_frames, 22, 3)
    Output: (num_frames, len(joint_indices), 3)
    """
    return skeleton_seq[:, joint_indices, :]


def temporal_resample(skeleton_seq, target_length=15):
    """
    Resample a variable-length sequence to a fixed length using
    linear interpolation along the time axis.
    
    Input:  (T_original, 21, 3)  — variable T
    Output: (target_length, 21, 3)  — fixed frames
    
    Strategy:
      - If T < target: upsample (interpolate intermediate frames)
      - If T > target: downsample (uniformly space the samples)
      - If T == target: pass through
    """
    T_orig = skeleton_seq.shape[0]
    
    if T_orig == target_length:
        return skeleton_seq
    
    if T_orig < 2:
        # Edge case: single frame → repeat
        return np.tile(skeleton_seq, (target_length, 1, 1))
    
    # Flatten: (T, 21, 3) → (T, 63)
    flat = skeleton_seq.reshape(T_orig, -1)
    
    # Linear interpolation along time axis
    x_orig = np.linspace(0, 1, T_orig)
    x_target = np.linspace(0, 1, target_length)
    
    interpolator = interp1d(x_orig, flat, axis=0, kind='linear')
    resampled = interpolator(x_target)
    
    return resampled.reshape(target_length, TARGET_JOINTS, TARGET_COORDS)


def normalize_coordinates(skeleton_seq):
    """
    Normalize 3D coordinates for translation & scale invariance.
    
    Strategy:
      1. Center on wrist (joint 0) at each frame → translation invariance
      2. Scale by max absolute coordinate across entire sequence → scale invariance
    
    Input/Output: (15, 21, 3)
    """
    # Center on wrist at each timestep
    wrist = skeleton_seq[:, 0:1, :]  # (15, 1, 3)
    centered = skeleton_seq - wrist
    
    # Scale by maximum absolute value across entire sequence
    max_val = np.abs(centered).max()
    if max_val > 1e-8:
        centered = centered / max_val
    
    return centered


# ─── Main Pipeline ───────────────────────────────────────────────────────────

def discover_skeleton_files(dhg_root):
    """
    Walk through the DHG-14/28 directory tree and discover all
    skeleton_world.txt files with their metadata.
    
    Returns list of dicts: {path, gesture_id, finger_type, subject_id, essai_id}
    """
    files = []
    
    for gesture_id in range(1, 15):
        gesture_dir = os.path.join(dhg_root, f"gesture_{gesture_id}")
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
                    skeleton_path = os.path.join(
                        subject_dir, f"essai_{essai_id}", "skeleton_world.txt"
                    )
                    
                    if os.path.isfile(skeleton_path):
                        files.append({
                            "path": skeleton_path,
                            "gesture_id": gesture_id,
                            "finger_type": finger_type,
                            "subject_id": subject_id,
                            "essai_id": essai_id,
                        })
    
    return files


def build_dataset():
    """Main conversion pipeline."""
    
    print("\n" + "=" * 60)
    print("  DHG-14/28 → raw_data.csv CONVERTER")
    print("=" * 60)
    print(f"  Source     : {DHG_ROOT}")
    print(f"  Output     : {OUTPUT_CSV}")
    print(f"  Seq Length : {TARGET_SEQ_LEN} frames")
    print(f"  Joints     : {TARGET_JOINTS} (remapped from 22)")
    print(f"  Mode       : {'28 classes (finger/hand)' if USE_28_CLASSES else '14 classes'}")
    print("=" * 60)
    
    if not os.path.isdir(DHG_ROOT):
        print(f"\n  [ERROR] DHG dataset directory not found at:")
        print(f"           {DHG_ROOT}")
        print(f"\n  Please download and extract the dataset first.")
        print(f"  See: http://www-rech.telecom-lille.fr/DHGdataset/")
        print(f"  Or search 'DHG hand gesture 3D skeletal' on Kaggle.")
        sys.exit(1)
    
    # Discover all skeleton files
    print("\n  Discovering skeleton files...")
    skeleton_files = discover_skeleton_files(DHG_ROOT)
    print(f"  Found {len(skeleton_files)} skeleton files.")
    
    if not skeleton_files:
        print("  [ERROR] No skeleton_world.txt files found!")
        sys.exit(1)
    
    # Process each file
    all_rows = []
    sample_id = 0
    stats = {"success": 0, "skipped": 0, "total_orig_frames": 0}
    seq_lengths = []
    
    for i, file_info in enumerate(skeleton_files):
        try:
            # 1. Load raw skeleton
            raw_seq = load_skeleton_file(file_info["path"])
            orig_len = raw_seq.shape[0]
            stats["total_orig_frames"] += orig_len
            seq_lengths.append(orig_len)
            
            # 2. Select 21 joints
            selected = select_joints(raw_seq, JOINTS_TO_KEEP)
            
            # 3. Resample to target length
            resampled = temporal_resample(selected, TARGET_SEQ_LEN)
            
            # 4. Normalize
            normalized = normalize_coordinates(resampled)
            
            # 5. Determine label
            gesture_name = GESTURE_NAMES[file_info["gesture_id"]]
            if USE_28_CLASSES:
                finger_suffix = "_1f" if file_info["finger_type"] == "finger_1" else "_wh"
                gesture_name += finger_suffix
            
            # 6. Flatten and create rows
            flat_frames = normalized.reshape(TARGET_SEQ_LEN, -1)
            
            for frame_idx in range(TARGET_SEQ_LEN):
                row = [sample_id, frame_idx, gesture_name] + \
                      flat_frames[frame_idx].tolist()
                all_rows.append(row)
            
            sample_id += 1
            stats["success"] += 1
            
        except Exception as e:
            print(f"\n  [WARN] Skipping {file_info['path']}: {e}")
            stats["skipped"] += 1
        
        # Progress
        if (i + 1) % 100 == 0 or i == len(skeleton_files) - 1:
            pct = (i + 1) / len(skeleton_files) * 100
            bar = "█" * int(pct // 2.5) + "░" * (40 - int(pct // 2.5))
            print(f"\r  [{bar}] {pct:5.1f}% ({i+1}/{len(skeleton_files)})", 
                  end="", flush=True)
    
    print()  # New line after progress bar
    
    # ── Build DataFrame ──────────────────────────────────────────────────
    landmark_cols = []
    for i in range(TARGET_JOINTS):
        landmark_cols.extend([f"x{i}", f"y{i}", f"z{i}"])
    
    columns = ["sample_id", "frame_index", "label"] + landmark_cols
    df = pd.DataFrame(all_rows, columns=columns)
    
    # ── Save ─────────────────────────────────────────────────────────────
    os.makedirs(os.path.dirname(OUTPUT_CSV), exist_ok=True)
    df.to_csv(OUTPUT_CSV, index=False)
    
    # ── Summary ──────────────────────────────────────────────────────────
    print("\n" + "=" * 60)
    print("  CONVERSION COMPLETE")
    print("=" * 60)
    print(f"  Processed      : {stats['success']} sequences")
    print(f"  Skipped        : {stats['skipped']} sequences")
    print(f"  Total samples  : {sample_id}")
    print(f"  Total rows     : {len(df)} ({sample_id} × {TARGET_SEQ_LEN})")
    print(f"  Unique classes : {df['label'].nunique()}")
    print(f"  CSV columns    : {len(df.columns)}")
    print(f"  File size      : {os.path.getsize(OUTPUT_CSV) / (1024*1024):.1f} MB")
    print(f"  Saved to       : {OUTPUT_CSV}")
    print("=" * 60)
    
    # Sequence length statistics (before resampling)
    if seq_lengths:
        seq_arr = np.array(seq_lengths)
        print(f"\n  Original Sequence Length Stats:")
        print(f"    Min   : {seq_arr.min()} frames")
        print(f"    Max   : {seq_arr.max()} frames")
        print(f"    Mean  : {seq_arr.mean():.1f} frames")
        print(f"    Median: {np.median(seq_arr):.0f} frames")
        print(f"    Total : {seq_arr.sum()} frames → resampled to {sample_id * TARGET_SEQ_LEN}")
    
    # Per-class breakdown
    print(f"\n  Class Distribution:")
    class_counts = df[df["frame_index"] == 0].groupby("label").size()
    for cls in sorted(class_counts.index):
        count = class_counts[cls]
        bar = "█" * min(count // 5, 40)
        print(f"    {cls:20s} : {count:4d} {bar}")
    
    # Quick validation
    print(f"\n  Validation:")
    sample_0 = df[df["sample_id"] == 0]
    print(f"    ✓ Sample 0: {len(sample_0)} frames, label='{sample_0['label'].iloc[0]}'")
    feature_cols = [c for c in df.columns if c not in ['sample_id', 'frame_index', 'label']]
    vals = df[feature_cols].values
    print(f"    ✓ Feature range: [{vals.min():.4f}, {vals.max():.4f}]")
    print(f"    ✓ NaN count: {df.isna().sum().sum()}")
    print(f"    ✓ Inf count: {np.isinf(vals).sum()}")
    print()
    
    return df


if __name__ == "__main__":
    df = build_dataset()
