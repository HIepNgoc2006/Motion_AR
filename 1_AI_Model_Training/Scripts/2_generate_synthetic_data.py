"""
=============================================================================
  2_generate_synthetic_data.py - Synthetic Gesture Data Generator
=============================================================================
  Project : Dynamic Spatial Hand Gesture Recognition
  Purpose : Generate synthetic 3D hand landmark sequences for immediate
            notebook testing, without needing a real dataset download.
  Output  : Dataset/raw_data.csv
  Usage   : python Scripts/2_generate_synthetic_data.py
=============================================================================
"""

import numpy as np
import pandas as pd
import os
import sys

np.random.seed(42)

# ─── Configuration ───────────────────────────────────────────────────────────
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUTPUT_CSV = os.path.join(PROJECT_ROOT, "Dataset", "raw_data.csv")

GESTURES = [
    "grab", "tap", "expand", "pinch",
    "rotation_cw", "rotation_ccw",
    "swipe_right", "swipe_left",
    "swipe_up", "swipe_down",
    "swipe_x", "swipe_v",
    "swipe_plus", "shake"
]

SAMPLES_PER_CLASS = 200   # Total: 14 × 200 = 2,800 samples
SEQ_LEN = 15
NUM_JOINTS = 21
NUM_COORDS = 3
FEATURES_PER_FRAME = NUM_JOINTS * NUM_COORDS  # 63


# ─── Hand Pose Generation ───────────────────────────────────────────────────
def generate_base_hand():
    """
    Generate a plausible resting hand pose with 21 joints in 3D space.
    
    Joint layout (MediaPipe convention):
      0: Wrist
      1-4: Thumb (CMC, MCP, IP, Tip)
      5-8: Index (MCP, PIP, DIP, Tip)
      9-12: Middle (MCP, PIP, DIP, Tip)
      13-16: Ring (MCP, PIP, DIP, Tip)
      17-20: Pinky (MCP, PIP, DIP, Tip)
    """
    hand = np.zeros((NUM_JOINTS, NUM_COORDS))
    
    # Wrist at center-bottom
    hand[0] = [0.5, 0.8, 0.0]
    
    # Finger base positions (spread along x-axis, extending upward in y)
    finger_bases_x = [0.38, 0.42, 0.48, 0.54, 0.60]
    
    for finger in range(5):
        base_x = finger_bases_x[finger]
        for joint in range(4):
            idx = 1 + finger * 4 + joint
            
            # Each joint extends slightly upward and outward
            hand[idx] = [
                base_x + joint * 0.005 + np.random.normal(0, 0.005),
                0.75 - (joint + 1) * 0.07 + np.random.normal(0, 0.005),
                np.random.normal(0, 0.015)
            ]
    
    return hand


def apply_gesture_motion(base_hand, gesture, frame_idx, total_frames):
    """
    Apply gesture-specific temporal motion patterns to a base hand pose.
    Each gesture creates a unique trajectory through the 63-dimensional
    coordinate space over the 15-frame sequence.
    """
    t = frame_idx / (total_frames - 1)  # Normalized time [0, 1]
    hand = base_hand.copy()
    
    # ── Translation gestures ──
    if gesture == "swipe_right":
        hand[:, 0] += t * 0.3 + t**2 * 0.05  # Accelerating rightward
        hand[:, 2] += np.sin(t * np.pi) * 0.02  # Slight arc in Z
        
    elif gesture == "swipe_left":
        hand[:, 0] -= t * 0.3 + t**2 * 0.05
        hand[:, 2] += np.sin(t * np.pi) * 0.02
        
    elif gesture == "swipe_up":
        hand[:, 1] -= t * 0.3 + t**2 * 0.05
        hand[:, 2] -= np.sin(t * np.pi) * 0.015
        
    elif gesture == "swipe_down":
        hand[:, 1] += t * 0.3 + t**2 * 0.05
        hand[:, 2] += np.sin(t * np.pi) * 0.015
    
    # ── Grasping gestures ──
    elif gesture == "grab":
        # All fingers curl toward palm center
        palm_center = np.array([0.5, 0.7, 0.0])
        for j in range(1, 21):
            direction = palm_center - hand[j]
            hand[j] += direction * t * 0.6
            # Fingertips curl more than bases
            joint_in_finger = (j - 1) % 4
            hand[j, 2] -= t * 0.03 * (joint_in_finger + 1)
    
    elif gesture == "expand":
        # Opposite of grab — fingers spread outward
        palm_center = np.array([0.5, 0.7, 0.0])
        for j in range(1, 21):
            direction = hand[j] - palm_center
            hand[j] += direction * t * 0.5
            hand[j, 2] += t * 0.02
    
    elif gesture == "pinch":
        # Thumb and index converge; other fingers stay
        thumb_target = np.array([0.43, 0.58, 0.01])
        index_target = np.array([0.43, 0.58, -0.01])
        for j in range(1, 5):  # Thumb joints
            hand[j] += (thumb_target - hand[j]) * t * 0.7
        for j in range(5, 9):  # Index joints
            hand[j] += (index_target - hand[j]) * t * 0.7
    
    elif gesture == "tap":
        # Quick down-up motion of index finger (bell curve)
        tap_amount = np.sin(t * np.pi) * 0.12
        for j in range(5, 9):  # Index finger
            hand[j, 1] += tap_amount
            hand[j, 2] -= tap_amount * 0.3
    
    # ── Rotation gestures ──
    elif gesture == "rotation_cw":
        angle = t * np.pi * 1.2
        cx, cy = 0.5, 0.65
        cos_a, sin_a = np.cos(angle), np.sin(angle)
        for j in range(21):
            dx = hand[j, 0] - cx
            dy = hand[j, 1] - cy
            hand[j, 0] = cx + dx * cos_a - dy * sin_a
            hand[j, 1] = cy + dx * sin_a + dy * cos_a
            hand[j, 2] += np.sin(angle) * 0.02
    
    elif gesture == "rotation_ccw":
        angle = -t * np.pi * 1.2
        cx, cy = 0.5, 0.65
        cos_a, sin_a = np.cos(angle), np.sin(angle)
        for j in range(21):
            dx = hand[j, 0] - cx
            dy = hand[j, 1] - cy
            hand[j, 0] = cx + dx * cos_a - dy * sin_a
            hand[j, 1] = cy + dx * sin_a + dy * cos_a
            hand[j, 2] += np.sin(angle) * 0.02
    
    # ── Complex path gestures ──
    elif gesture == "swipe_x":
        # Diagonal down-right then down-left (X shape)
        if t < 0.5:
            s = t * 2  # 0→1 for first half
            hand[:, 0] += s * 0.25
            hand[:, 1] += s * 0.25
        else:
            s = (t - 0.5) * 2  # 0→1 for second half
            hand[:, 0] += 0.25 - s * 0.25
            hand[:, 1] += 0.25 + s * 0.05
            hand[:, 0] -= s * 0.12
    
    elif gesture == "swipe_v":
        # Down-left then down-right (V shape)
        if t < 0.5:
            s = t * 2
            hand[:, 0] -= s * 0.2
            hand[:, 1] += s * 0.25
        else:
            s = (t - 0.5) * 2
            hand[:, 0] -= 0.2 - s * 0.4
            hand[:, 1] += 0.25
    
    elif gesture == "swipe_plus":
        # Vertical then horizontal (+ shape)
        if t < 0.25:
            hand[:, 1] -= t * 4 * 0.15
        elif t < 0.5:
            hand[:, 1] -= 0.15 - (t - 0.25) * 4 * 0.15
        elif t < 0.75:
            hand[:, 0] += (t - 0.5) * 4 * 0.15
        else:
            hand[:, 0] += 0.15 - (t - 0.75) * 4 * 0.15
    
    elif gesture == "shake":
        # Rapid lateral oscillation
        hand[:, 0] += 0.08 * np.sin(t * 6 * np.pi)
        hand[:, 1] += 0.03 * np.sin(t * 4 * np.pi + 0.5)
        hand[:, 2] += 0.02 * np.cos(t * 5 * np.pi)
    
    # ── Add realistic noise ──
    noise_scale = 0.006 + 0.002 * np.random.random()
    hand += np.random.normal(0, noise_scale, hand.shape)
    
    return hand


# ─── Main Generation Loop ───────────────────────────────────────────────────
def generate_dataset():
    """Generate the complete synthetic dataset and save to CSV."""
    
    print("\n" + "=" * 60)
    print("  SYNTHETIC GESTURE DATA GENERATOR")
    print("=" * 60)
    print(f"  Gestures       : {len(GESTURES)}")
    print(f"  Samples/Class  : {SAMPLES_PER_CLASS}")
    print(f"  Total Samples  : {len(GESTURES) * SAMPLES_PER_CLASS}")
    print(f"  Sequence Length : {SEQ_LEN} frames")
    print(f"  Features/Frame : {FEATURES_PER_FRAME}")
    print(f"  Output         : {OUTPUT_CSV}")
    print("=" * 60)
    
    rows = []
    sample_id = 0
    
    for g_idx, gesture in enumerate(GESTURES):
        for s in range(SAMPLES_PER_CLASS):
            # Generate a unique base hand pose with per-sample variation
            base = generate_base_hand()
            base += np.random.normal(0, 0.015, base.shape)
            
            # Optional: slight random speed variation per sample
            speed_factor = 0.85 + np.random.random() * 0.3  # 0.85x to 1.15x
            
            for f in range(SEQ_LEN):
                # Apply speed variation by warping the time parameter
                adjusted_f = min(SEQ_LEN - 1, f * speed_factor)
                hand = apply_gesture_motion(base, gesture, adjusted_f, SEQ_LEN)
                flat = hand.flatten().tolist()
                rows.append([sample_id, f, gesture] + flat)
            
            sample_id += 1
        
        # Progress indicator
        pct = (g_idx + 1) / len(GESTURES) * 100
        bar = "█" * int(pct // 2.5) + "░" * (40 - int(pct // 2.5))
        print(f"\r  [{bar}] {pct:5.1f}% — {gesture}", end="", flush=True)
    
    print()  # New line after progress bar
    
    # ── Build DataFrame ──────────────────────────────────────────────────
    cols = ["sample_id", "frame_index", "label"]
    for i in range(NUM_JOINTS):
        cols.extend([f"x{i}", f"y{i}", f"z{i}"])
    
    df = pd.DataFrame(rows, columns=cols)
    
    # ── Save ─────────────────────────────────────────────────────────────
    os.makedirs(os.path.dirname(OUTPUT_CSV), exist_ok=True)
    df.to_csv(OUTPUT_CSV, index=False)
    
    # ── Summary ──────────────────────────────────────────────────────────
    print("\n" + "=" * 60)
    print("  GENERATION COMPLETE")
    print("=" * 60)
    print(f"  Total samples  : {sample_id}")
    print(f"  Total rows     : {len(df)} ({sample_id} × {SEQ_LEN})")
    print(f"  Classes        : {df['label'].nunique()}")
    print(f"  CSV columns    : {len(df.columns)} (3 meta + {FEATURES_PER_FRAME} features)")
    print(f"  File size      : {os.path.getsize(OUTPUT_CSV) / (1024*1024):.1f} MB")
    print(f"  Saved to       : {OUTPUT_CSV}")
    print("=" * 60)
    
    # Per-class breakdown
    print("\n  Class Distribution:")
    for gesture in GESTURES:
        count = len(df[(df["label"] == gesture) & (df["frame_index"] == 0)])
        bar = "█" * (count // 5)
        print(f"    {gesture:15s} : {count:4d} {bar}")
    
    # Quick validation
    print("\n  Validation Checks:")
    sample_0 = df[df["sample_id"] == 0]
    print(f"    ✓ Sample 0 has {len(sample_0)} frames (expect {SEQ_LEN})")
    print(f"    ✓ Frame indices: {sorted(sample_0['frame_index'].unique().tolist())}")
    feature_cols = [c for c in df.columns if c not in ['sample_id', 'frame_index', 'label']]
    print(f"    ✓ Feature columns: {len(feature_cols)} (expect {FEATURES_PER_FRAME})")
    print(f"    ✓ Feature range: [{df[feature_cols].min().min():.4f}, {df[feature_cols].max().max():.4f}]")
    print()
    
    return df


if __name__ == "__main__":
    df = generate_dataset()
