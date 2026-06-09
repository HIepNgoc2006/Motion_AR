"""
=============================================================================
  1_collect_data.py - Real-Time Hand Gesture Data Collection Script
=============================================================================
  Project : Dynamic Spatial Hand Gesture Recognition
  Purpose : Capture 3D hand landmarks from webcam using MediaPipe,
            record 15-frame gesture sequences, and save to raw_data.csv.
  Usage   : python Scripts/1_collect_data.py
=============================================================================
"""

import cv2
import mediapipe as mp
import pandas as pd
import numpy as np
import os
import time
from datetime import datetime

# ─────────────────────────────────────────────────────────────────────────────
# Configuration
# ─────────────────────────────────────────────────────────────────────────────
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATASET_DIR = os.path.join(PROJECT_ROOT, "Dataset")
OUTPUT_CSV = os.path.join(DATASET_DIR, "raw_data.csv")

SEQUENCE_LENGTH = 15          # Number of frames per gesture sample
NUM_LANDMARKS = 21            # MediaPipe hand landmarks
NUM_COORDS = 3                # X, Y, Z per landmark
FEATURES_PER_FRAME = NUM_LANDMARKS * NUM_COORDS  # 63

# Define your gesture classes here
GESTURE_CLASSES = [
    "swipe_left",
    "swipe_right",
    "swipe_up",
    "swipe_down",
    "grab",
    "pinch",
    "wave",
    "rotate_cw",
    "rotate_ccw",
    "tap",
]

SAMPLES_PER_CLASS = 50        # Target samples per gesture class
COOLDOWN_SECONDS = 1.5        # Pause between recordings

# ─────────────────────────────────────────────────────────────────────────────
# MediaPipe Hands Setup
# ─────────────────────────────────────────────────────────────────────────────
mp_hands = mp.solutions.hands
mp_drawing = mp.solutions.drawing_utils
mp_drawing_styles = mp.solutions.drawing_styles

hands = mp_hands.Hands(
    static_image_mode=False,
    max_num_hands=1,
    min_detection_confidence=0.7,
    min_tracking_confidence=0.6,
    model_complexity=1,
)

# ─────────────────────────────────────────────────────────────────────────────
# Column Names for the CSV
# ─────────────────────────────────────────────────────────────────────────────
landmark_columns = []
for i in range(NUM_LANDMARKS):
    landmark_columns.extend([f"x{i}", f"y{i}", f"z{i}"])

CSV_COLUMNS = ["sample_id", "frame_index", "label"] + landmark_columns


def extract_landmarks(hand_landmarks):
    """
    Extract (x, y, z) coordinates from a MediaPipe hand_landmarks object.
    Returns a flat list of 63 values.
    """
    coords = []
    for lm in hand_landmarks.landmark:
        coords.extend([lm.x, lm.y, lm.z])
    return coords


def draw_info_panel(frame, gesture_name, sample_count, total_target,
                    is_recording, frame_count):
    """
    Draw an informational overlay on the video feed.
    """
    h, w, _ = frame.shape
    
    # Semi-transparent overlay
    overlay = frame.copy()
    cv2.rectangle(overlay, (0, 0), (w, 80), (20, 20, 20), -1)
    frame = cv2.addWeighted(overlay, 0.7, frame, 0.3, 0)
    
    # Gesture info
    status_color = (0, 255, 100) if is_recording else (200, 200, 200)
    status_text = f"RECORDING [{frame_count}/{SEQUENCE_LENGTH}]" if is_recording else "READY"
    
    cv2.putText(frame, f"Gesture: {gesture_name}", (10, 25),
                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)
    cv2.putText(frame, f"Samples: {sample_count}/{total_target}", (10, 50),
                cv2.FONT_HERSHEY_SIMPLEX, 0.6, (180, 230, 255), 1)
    cv2.putText(frame, status_text, (10, 75),
                cv2.FONT_HERSHEY_SIMPLEX, 0.6, status_color, 2)
    
    # Progress bar
    progress = sample_count / total_target if total_target > 0 else 0
    bar_width = int(progress * (w - 20))
    cv2.rectangle(frame, (10, h - 15), (w - 10, h - 5), (50, 50, 50), -1)
    cv2.rectangle(frame, (10, h - 15), (10 + bar_width, h - 5), (0, 200, 120), -1)
    
    return frame


def collect_data():
    """
    Main data collection loop.
    
    Controls:
      - SPACE : Start recording a 15-frame gesture sequence
      - N     : Skip to next gesture class
      - P     : Go back to previous gesture class
      - Q/ESC : Quit and save
    """
    os.makedirs(DATASET_DIR, exist_ok=True)
    
    # Load existing data if any
    if os.path.exists(OUTPUT_CSV):
        existing_df = pd.read_csv(OUTPUT_CSV)
        all_data = existing_df.values.tolist()
        next_sample_id = int(existing_df["sample_id"].max()) + 1 if len(existing_df) > 0 else 0
        print(f"[INFO] Loaded {len(existing_df)} existing rows "
              f"({existing_df['sample_id'].nunique()} samples)")
    else:
        all_data = []
        next_sample_id = 0
    
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("[ERROR] Cannot open webcam. Please check your camera connection.")
        return
    
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
    
    class_idx = 0
    recording = False
    frame_buffer = []
    last_record_time = 0
    
    print("\n" + "=" * 60)
    print("  HAND GESTURE DATA COLLECTION")
    print("=" * 60)
    print(f"  Sequence Length : {SEQUENCE_LENGTH} frames")
    print(f"  Features/Frame  : {FEATURES_PER_FRAME}")
    print(f"  Gesture Classes : {len(GESTURE_CLASSES)}")
    print(f"  Target Samples  : {SAMPLES_PER_CLASS} per class")
    print("=" * 60)
    print("\n  Controls:")
    print("    [SPACE] Start recording")
    print("    [N]     Next gesture class")
    print("    [P]     Previous gesture class")
    print("    [Q/ESC] Quit and save")
    print()
    
    while True:
        ret, frame = cap.read()
        if not ret:
            print("[WARN] Frame capture failed. Retrying...")
            continue
        
        frame = cv2.flip(frame, 1)  # Mirror for intuitive interaction
        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = hands.process(rgb_frame)
        
        current_gesture = GESTURE_CLASSES[class_idx]
        
        # Count existing samples for current gesture
        existing_count = sum(
            1 for row in all_data
            if row[2] == current_gesture and row[1] == 0
        )
        
        # Draw hand landmarks
        if results.multi_hand_landmarks:
            for hand_landmarks in results.multi_hand_landmarks:
                mp_drawing.draw_landmarks(
                    frame, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                    mp_drawing_styles.get_default_hand_landmarks_style(),
                    mp_drawing_styles.get_default_hand_connections_style(),
                )
                
                # If recording, capture frame data
                if recording:
                    coords = extract_landmarks(hand_landmarks)
                    frame_buffer.append(coords)
                    
                    if len(frame_buffer) >= SEQUENCE_LENGTH:
                        # Save the completed sequence
                        for fi, frame_coords in enumerate(frame_buffer):
                            row = [next_sample_id, fi, current_gesture] + frame_coords
                            all_data.append(row)
                        
                        next_sample_id += 1
                        recording = False
                        frame_buffer = []
                        last_record_time = time.time()
                        print(f"  ✓ Sample {next_sample_id - 1} recorded "
                              f"for '{current_gesture}' "
                              f"(total: {existing_count + 1})")
        
        # If recording but no hand detected
        elif recording:
            # Use zero-padding for missing frames (optional — could also cancel)
            frame_buffer.append([0.0] * FEATURES_PER_FRAME)
            if len(frame_buffer) >= SEQUENCE_LENGTH:
                for fi, frame_coords in enumerate(frame_buffer):
                    row = [next_sample_id, fi, current_gesture] + frame_coords
                    all_data.append(row)
                next_sample_id += 1
                recording = False
                frame_buffer = []
                last_record_time = time.time()
                print(f"  ⚠ Sample {next_sample_id - 1} recorded with "
                      f"missing frames for '{current_gesture}'")
        
        # Draw UI
        frame = draw_info_panel(
            frame, current_gesture, existing_count,
            SAMPLES_PER_CLASS, recording, len(frame_buffer)
        )
        
        # Recording indicator (red border)
        if recording:
            cv2.rectangle(frame, (0, 0),
                          (frame.shape[1] - 1, frame.shape[0] - 1),
                          (0, 0, 255), 4)
        
        cv2.imshow("Hand Gesture Collection", frame)
        
        # Handle key presses
        key = cv2.waitKey(1) & 0xFF
        
        if key == ord(' '):
            # Start recording (with cooldown)
            if not recording and (time.time() - last_record_time) > COOLDOWN_SECONDS:
                recording = True
                frame_buffer = []
                print(f"  ● Recording gesture '{current_gesture}'...")
        
        elif key == ord('n') or key == ord('N'):
            # Next gesture class
            if not recording:
                class_idx = (class_idx + 1) % len(GESTURE_CLASSES)
                print(f"\n  → Switched to: {GESTURE_CLASSES[class_idx]}")
        
        elif key == ord('p') or key == ord('P'):
            # Previous gesture class
            if not recording:
                class_idx = (class_idx - 1) % len(GESTURE_CLASSES)
                print(f"\n  → Switched to: {GESTURE_CLASSES[class_idx]}")
        
        elif key == ord('q') or key == ord('Q') or key == 27:
            # Quit
            break
    
    # ── Save Data ────────────────────────────────────────────────────────
    cap.release()
    cv2.destroyAllWindows()
    hands.close()
    
    if all_data:
        df = pd.DataFrame(all_data, columns=CSV_COLUMNS)
        df.to_csv(OUTPUT_CSV, index=False)
        
        total_samples = df["sample_id"].nunique()
        total_rows = len(df)
        classes_recorded = df["label"].nunique()
        
        print("\n" + "=" * 60)
        print("  DATA COLLECTION SUMMARY")
        print("=" * 60)
        print(f"  Total Samples  : {total_samples}")
        print(f"  Total Rows     : {total_rows}")
        print(f"  Classes        : {classes_recorded}")
        print(f"  Saved to       : {OUTPUT_CSV}")
        print("=" * 60)
        
        # Per-class breakdown
        print("\n  Per-Class Breakdown:")
        class_counts = df[df["frame_index"] == 0].groupby("label").size()
        for cls, count in class_counts.items():
            bar = "█" * min(count, 50)
            print(f"    {cls:20s} : {count:4d} {bar}")
        print()
    else:
        print("\n[INFO] No data collected. Exiting.")


if __name__ == "__main__":
    collect_data()
