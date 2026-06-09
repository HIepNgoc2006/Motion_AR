"""
=============================================================================
  4_realtime_inference.py — Nhận diện Cử chỉ Tay Thời gian Thực
=============================================================================
  Dự án  : Dynamic Spatial Hand Gesture Recognition
  Mục đích: Sử dụng webcam + MediaPipe để trích xuất 21 landmark 3D,
            tích lũy 15 frame vào sliding window, đưa qua mô hình
            Transformer (hoặc LSTM) đã huấn luyện để dự đoán cử chỉ
            tay theo thời gian thực.
  
  Hướng dẫn cài đặt:
    1. Kích hoạt môi trường ảo:
       .\venv\Scripts\activate
    
    2. Cài đặt thư viện cần thiết (nếu chưa có):
       pip install torch torchvision opencv-python mediapipe numpy
    
    3. Chạy script:
       python Scripts/4_realtime_inference.py
    
    4. Điều khiển:
       - Đưa tay vào khung hình webcam
       - Chờ bộ đệm đầy 15 frame → bắt đầu nhận diện
       - Nhấn 'Q' hoặc ESC để thoát
       - Nhấn 'M' để chuyển đổi giữa Transformer và LSTM

  Lưu ý:
    - Đảm bảo webcam hoạt động bình thường
    - Ánh sáng tốt giúp MediaPipe detect chính xác hơn
    - Cần có file weights đã train trong thư mục Models/
=============================================================================
"""

import os
import sys
import math
import time
import collections
import numpy as np

import cv2
import mediapipe as mp
import torch
import torch.nn as nn

# ═══════════════════════════════════════════════════════════════════════════════
# CẤU HÌNH
# ═══════════════════════════════════════════════════════════════════════════════
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODEL_DIR = os.path.join(PROJECT_ROOT, "Models")

TRANSFORMER_WEIGHTS = os.path.join(MODEL_DIR, "best_transformer.pth")
LSTM_WEIGHTS = os.path.join(MODEL_DIR, "best_lstm.pth")

# Tham số chuỗi
SEQ_LEN = 15
NUM_LANDMARKS = 21
NUM_COORDS = 3
INPUT_DIM = NUM_LANDMARKS * NUM_COORDS  # 63

# Tham số mô hình
D_MODEL = 128
NHEAD = 4
DIM_FEEDFORWARD = 256
NUM_LAYERS = 3
EMBED_SIZE = 128
HIDDEN_SIZE = 128
LSTM_NUM_LAYERS = 2

# Tham số suy luận
CONFIDENCE_THRESHOLD = 0.75   # Ngưỡng confidence tối thiểu để hiển thị
SMOOTHING_FRAMES = 5          # Số frame giữ kết quả ổn định (chống flicker)

# Nhãn cử chỉ — PHẢI khớp với thứ tự LabelEncoder khi huấn luyện
# (Sắp xếp theo alphabetical order vì LabelEncoder sort tự động)
GESTURE_LABELS = sorted([
    "expand", "grab", "pinch", "rotation_ccw", "rotation_cw",
    "shake", "swipe_down", "swipe_left", "swipe_plus",
    "swipe_right", "swipe_up", "swipe_v", "swipe_x", "tap"
])
NUM_CLASSES = len(GESTURE_LABELS)

# Bảng emoji cho từng cử chỉ
GESTURE_EMOJI = {
    "expand": "🖐️", "grab": "✊", "pinch": "🤏",
    "rotation_ccw": "🔄", "rotation_cw": "🔃",
    "shake": "👋", "swipe_down": "⬇️", "swipe_left": "⬅️",
    "swipe_plus": "➕", "swipe_right": "➡️", "swipe_up": "⬆️",
    "swipe_v": "✌️", "swipe_x": "❌", "tap": "👆"
}


# ═══════════════════════════════════════════════════════════════════════════════
# KIẾN TRÚC MÔ HÌNH — Positional Encoding
# ═══════════════════════════════════════════════════════════════════════════════
class PositionalEncoding(nn.Module):
    """Mã hóa vị trí bằng hàm sin/cos chuẩn."""

    def __init__(self, d_model, max_len=500, dropout=0.1):
        super().__init__()
        self.dropout = nn.Dropout(p=dropout)

        pe = torch.zeros(max_len, d_model)
        position = torch.arange(0, max_len, dtype=torch.float32).unsqueeze(1)
        div_term = torch.exp(
            torch.arange(0, d_model, 2, dtype=torch.float32)
            * (-math.log(10000.0) / d_model)
        )
        pe[:, 0::2] = torch.sin(position * div_term)
        pe[:, 1::2] = torch.cos(position * div_term)
        pe = pe.unsqueeze(0)
        self.register_buffer("pe", pe)

    def forward(self, x):
        x = x + self.pe[:, : x.size(1), :]
        return self.dropout(x)


# ═══════════════════════════════════════════════════════════════════════════════
# KIẾN TRÚC MÔ HÌNH — Gesture Transformer
# ═══════════════════════════════════════════════════════════════════════════════
class GestureTransformer(nn.Module):
    """
    Transformer Encoder cho phân loại chuỗi cử chỉ tay.

    Pipeline:
        Input (batch, 15, 63)
          → Linear Projection (63 → 128)
          → Positional Encoding
          → Transformer Encoder (3 layers, 4 heads)
          → Global Mean Pooling
          → Classifier (128 → 64 → num_classes)
    """

    def __init__(self, input_dim=63, d_model=128, nhead=4,
                 dim_feedforward=256, num_layers=3, num_classes=14,
                 dropout=0.2, pe_dropout=0.1):
        super().__init__()
        self.d_model = d_model
        self.input_projection = nn.Linear(input_dim, d_model)
        self.pos_encoder = PositionalEncoding(
            d_model=d_model, max_len=500, dropout=pe_dropout
        )
        encoder_layer = nn.TransformerEncoderLayer(
            d_model=d_model, nhead=nhead,
            dim_feedforward=dim_feedforward,
            dropout=dropout, batch_first=True, activation="relu"
        )
        self.transformer_encoder = nn.TransformerEncoder(
            encoder_layer, num_layers=num_layers
        )
        self.classifier = nn.Sequential(
            nn.Linear(d_model, 64),
            nn.ReLU(),
            nn.Dropout(0.3),
            nn.Linear(64, num_classes),
        )

    def forward(self, x):
        x = self.input_projection(x)
        x = self.pos_encoder(x)
        x = self.transformer_encoder(x)
        x = x.mean(dim=1)
        x = self.classifier(x)
        return x


# ═══════════════════════════════════════════════════════════════════════════════
# KIẾN TRÚC MÔ HÌNH — LSTM Gesture Classifier
# ═══════════════════════════════════════════════════════════════════════════════
class LSTMGestureClassifier(nn.Module):
    """
    LSTM cho phân loại chuỗi cử chỉ tay.

    Pipeline:
        Input (batch, 15, 63)
          → Embedding: Linear(63, 128) + LayerNorm + ReLU
          → LSTM(128, 128, 2 layers)
          → Final hidden state h_n[-1]
          → Classifier: Linear(128, num_classes)
    """

    def __init__(self, input_size=63, embed_size=128, hidden_size=128,
                 num_layers=2, num_classes=14, dropout=0.3):
        super().__init__()
        self.hidden_size = hidden_size
        self.num_layers = num_layers

        self.embedding = nn.Sequential(
            nn.Linear(input_size, embed_size),
            nn.LayerNorm(embed_size),
            nn.ReLU(),
        )
        self.lstm = nn.LSTM(
            input_size=embed_size, hidden_size=hidden_size,
            num_layers=num_layers, batch_first=True,
            dropout=dropout if num_layers > 1 else 0.0,
        )
        self.classifier = nn.Linear(hidden_size, num_classes)

    def forward(self, x, return_hidden=False):
        x = self.embedding(x)
        output, (h_n, c_n) = self.lstm(x)
        h_final = h_n[-1]
        logits = self.classifier(h_final)
        if return_hidden:
            return logits, h_final
        return logits


# ═══════════════════════════════════════════════════════════════════════════════
# TIỆN ÍCH — Trích xuất & Chuẩn hóa Landmark
# ═══════════════════════════════════════════════════════════════════════════════
def extract_landmarks(hand_landmarks):
    """
    Trích xuất tọa độ (x, y, z) từ MediaPipe hand_landmarks.
    Trả về: list 63 giá trị float.
    """
    coords = []
    for lm in hand_landmarks.landmark:
        coords.extend([lm.x, lm.y, lm.z])
    return coords


def normalize_frame(landmarks_flat):
    """
    Chuẩn hóa tọa độ cho translation & scale invariance.
    - Center trên wrist (landmark 0)
    - Scale bằng max absolute value
    """
    arr = np.array(landmarks_flat, dtype=np.float32).reshape(NUM_LANDMARKS, NUM_COORDS)

    # Center trên wrist
    wrist = arr[0:1, :]
    centered = arr - wrist

    # Scale
    max_val = np.abs(centered).max()
    if max_val > 1e-8:
        centered = centered / max_val

    return centered.flatten().tolist()


# ═══════════════════════════════════════════════════════════════════════════════
# TIỆN ÍCH — Vẽ UI Overlay
# ═══════════════════════════════════════════════════════════════════════════════
def draw_ui_overlay(frame, gesture_name, confidence, fps, buffer_fill,
                    model_name, is_predicting):
    """
    Vẽ giao diện overlay chuyên nghiệp lên khung hình.
    """
    h, w, _ = frame.shape

    # ── Header Panel ─────────────────────────────────────────────────────
    overlay = frame.copy()
    cv2.rectangle(overlay, (0, 0), (w, 95), (15, 15, 15), -1)
    frame = cv2.addWeighted(overlay, 0.80, frame, 0.20, 0)

    # Tiêu đề dự án
    cv2.putText(frame, "HAND GESTURE RECOGNITION", (15, 28),
                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (220, 220, 220), 2)

    # Mô hình đang sử dụng
    model_color = (180, 120, 255) if model_name == "Transformer" else (120, 220, 180)
    cv2.putText(frame, f"Model: {model_name}", (15, 55),
                cv2.FONT_HERSHEY_SIMPLEX, 0.5, model_color, 1)

    # FPS
    fps_color = (100, 255, 100) if fps > 20 else (100, 200, 255) if fps > 10 else (100, 100, 255)
    cv2.putText(frame, f"FPS: {fps:.0f}", (w - 110, 28),
                cv2.FONT_HERSHEY_SIMPLEX, 0.6, fps_color, 2)

    # Buffer indicator
    bar_x, bar_y, bar_w, bar_h = 15, 70, 200, 12
    fill_ratio = buffer_fill / SEQ_LEN
    cv2.rectangle(frame, (bar_x, bar_y), (bar_x + bar_w, bar_y + bar_h),
                  (60, 60, 60), -1)
    fill_w = int(fill_ratio * bar_w)
    fill_color = (80, 200, 120) if fill_ratio >= 1.0 else (255, 200, 80)
    cv2.rectangle(frame, (bar_x, bar_y), (bar_x + fill_w, bar_y + bar_h),
                  fill_color, -1)
    cv2.rectangle(frame, (bar_x, bar_y), (bar_x + bar_w, bar_y + bar_h),
                  (100, 100, 100), 1)
    cv2.putText(frame, f"Buffer: {buffer_fill}/{SEQ_LEN}", (bar_x + bar_w + 10, bar_y + 11),
                cv2.FONT_HERSHEY_SIMPLEX, 0.4, (180, 180, 180), 1)

    # ── Prediction Panel (Bottom) ────────────────────────────────────────
    if is_predicting and gesture_name:
        # Background panel
        panel_h = 90
        overlay2 = frame.copy()
        cv2.rectangle(overlay2, (0, h - panel_h), (w, h), (15, 15, 15), -1)
        frame = cv2.addWeighted(overlay2, 0.80, frame, 0.20, 0)

        # Confidence bar
        conf_bar_x = 20
        conf_bar_y = h - 25
        conf_bar_w = w - 40
        conf_bar_h = 10
        cv2.rectangle(frame, (conf_bar_x, conf_bar_y),
                      (conf_bar_x + conf_bar_w, conf_bar_y + conf_bar_h),
                      (40, 40, 40), -1)

        conf_fill = int(confidence * conf_bar_w)
        if confidence >= CONFIDENCE_THRESHOLD:
            conf_color = (80, 230, 120)  # Xanh lá
        elif confidence >= 0.5:
            conf_color = (80, 200, 255)  # Vàng cam
        else:
            conf_color = (80, 80, 230)   # Đỏ

        cv2.rectangle(frame, (conf_bar_x, conf_bar_y),
                      (conf_bar_x + conf_fill, conf_bar_y + conf_bar_h),
                      conf_color, -1)

        # Gesture name
        display_text = f"{gesture_name.upper()}"
        text_color = (255, 255, 255) if confidence >= CONFIDENCE_THRESHOLD else (140, 140, 140)
        cv2.putText(frame, display_text, (20, h - 55),
                    cv2.FONT_HERSHEY_SIMPLEX, 1.2, text_color, 3)

        # Confidence percentage
        conf_text = f"{confidence * 100:.1f}%"
        cv2.putText(frame, conf_text, (w - 120, h - 55),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.8, conf_color, 2)

    elif not is_predicting:
        # Chờ buffer đầy
        cv2.putText(frame, "Dua tay vao khung hinh...", (15, h - 20),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (150, 150, 150), 1)

    # ── Hướng dẫn phím ───────────────────────────────────────────────────
    cv2.putText(frame, "[Q] Thoat  [M] Doi model", (w - 260, 55),
                cv2.FONT_HERSHEY_SIMPLEX, 0.4, (120, 120, 120), 1)

    return frame


# ═══════════════════════════════════════════════════════════════════════════════
# VÒNG LẶP CHÍNH — REAL-TIME INFERENCE
# ═══════════════════════════════════════════════════════════════════════════════
def main():
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"\n{'='*60}")
    print(f"  REAL-TIME HAND GESTURE RECOGNITION")
    print(f"{'='*60}")
    print(f"  Device      : {device}")
    print(f"  Seq Length   : {SEQ_LEN} frames")
    print(f"  Features     : {INPUT_DIM} per frame")
    print(f"  Classes      : {NUM_CLASSES}")
    print(f"  Threshold    : {CONFIDENCE_THRESHOLD * 100:.0f}%")
    print(f"{'='*60}")

    # ── Tải mô hình ──────────────────────────────────────────────────────
    models = {}
    available_models = []

    # Thử tải Transformer
    if os.path.isfile(TRANSFORMER_WEIGHTS):
        print(f"\n  [OK] Đang tải Transformer từ: {TRANSFORMER_WEIGHTS}")
        transformer = GestureTransformer(
            input_dim=INPUT_DIM, d_model=D_MODEL, nhead=NHEAD,
            dim_feedforward=DIM_FEEDFORWARD, num_layers=NUM_LAYERS,
            num_classes=NUM_CLASSES, dropout=0.2, pe_dropout=0.1
        ).to(device)
        state = torch.load(TRANSFORMER_WEIGHTS, map_location=device, weights_only=True)
        transformer.load_state_dict(state)
        transformer.eval()
        models["Transformer"] = transformer
        available_models.append("Transformer")
        params = sum(p.numel() for p in transformer.parameters())
        print(f"       ✓ Tải thành công ({params:,} tham số)")
    else:
        print(f"\n  [--] Không tìm thấy Transformer weights: {TRANSFORMER_WEIGHTS}")

    # Thử tải LSTM
    if os.path.isfile(LSTM_WEIGHTS):
        print(f"  [OK] Đang tải LSTM từ: {LSTM_WEIGHTS}")
        lstm = LSTMGestureClassifier(
            input_size=INPUT_DIM, embed_size=EMBED_SIZE,
            hidden_size=HIDDEN_SIZE, num_layers=LSTM_NUM_LAYERS,
            num_classes=NUM_CLASSES, dropout=0.3
        ).to(device)
        state = torch.load(LSTM_WEIGHTS, map_location=device, weights_only=True)
        lstm.load_state_dict(state)
        lstm.eval()
        models["LSTM"] = lstm
        available_models.append("LSTM")
        params = sum(p.numel() for p in lstm.parameters())
        print(f"       ✓ Tải thành công ({params:,} tham số)")
    else:
        print(f"  [--] Không tìm thấy LSTM weights: {LSTM_WEIGHTS}")

    if not available_models:
        print("\n  [LỖI] Không tìm thấy file weights nào trong thư mục Models/!")
        print(f"         Hãy chạy các notebook huấn luyện trước.")
        print(f"         Cần có: best_transformer.pth hoặc best_lstm.pth")
        sys.exit(1)

    current_model_idx = 0
    current_model_name = available_models[current_model_idx]
    model = models[current_model_name]
    print(f"\n  → Sử dụng mô hình: {current_model_name}")

    # ── Khởi tạo MediaPipe Hands ─────────────────────────────────────────
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

    # ── Khởi tạo Webcam ─────────────────────────────────────────────────
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("\n  [LỖI] Không thể mở webcam! Kiểm tra kết nối camera.")
        sys.exit(1)

    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)

    actual_w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    actual_h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    print(f"  📷 Webcam: {actual_w}×{actual_h}")

    # ── Sliding Window Buffer ────────────────────────────────────────────
    frame_buffer = collections.deque(maxlen=SEQ_LEN)

    # ── Smoothing State ──────────────────────────────────────────────────
    current_gesture = None
    current_confidence = 0.0
    gesture_hold_counter = 0   # Đếm số frame giữ cùng kết quả
    prediction_history = collections.deque(maxlen=SMOOTHING_FRAMES)

    # ── FPS Counter ──────────────────────────────────────────────────────
    fps = 0.0
    fps_update_interval = 10
    frame_count = 0
    fps_start_time = time.time()

    print(f"\n  ✅ Sẵn sàng! Đưa tay vào khung hình webcam.")
    print(f"  📌 Điều khiển: [Q/ESC] Thoát | [M] Đổi model\n")

    # ── Main Loop ────────────────────────────────────────────────────────
    while True:
        ret, frame = cap.read()
        if not ret:
            continue

        frame = cv2.flip(frame, 1)  # Mirror
        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = hands.process(rgb_frame)

        is_predicting = False

        if results.multi_hand_landmarks:
            for hand_landmarks in results.multi_hand_landmarks:
                # Vẽ skeleton tay
                mp_drawing.draw_landmarks(
                    frame, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                    mp_drawing_styles.get_default_hand_landmarks_style(),
                    mp_drawing_styles.get_default_hand_connections_style(),
                )

                # Trích xuất & chuẩn hóa landmarks
                raw_coords = extract_landmarks(hand_landmarks)
                normalized_coords = normalize_frame(raw_coords)
                frame_buffer.append(normalized_coords)

                # Suy luận khi buffer đầy
                if len(frame_buffer) == SEQ_LEN:
                    # Chuyển sang tensor (1, 15, 63)
                    seq_array = np.array(list(frame_buffer), dtype=np.float32)
                    seq_tensor = torch.tensor(seq_array).unsqueeze(0).to(device)

                    # Forward pass
                    with torch.no_grad():
                        logits = model(seq_tensor)
                        probs = torch.softmax(logits, dim=1)
                        confidence, predicted_idx = probs.max(dim=1)
                        confidence = confidence.item()
                        predicted_idx = predicted_idx.item()

                    predicted_gesture = GESTURE_LABELS[predicted_idx]

                    # ── Smoothing Logic ───────────────────────────────
                    prediction_history.append((predicted_gesture, confidence))

                    if confidence >= CONFIDENCE_THRESHOLD:
                        # Kiểm tra consistency trong history
                        recent_gestures = [p[0] for p in prediction_history]
                        most_common = max(set(recent_gestures),
                                          key=recent_gestures.count)
                        consistency = recent_gestures.count(most_common) / len(recent_gestures)

                        if consistency >= 0.6:
                            # Đủ ổn định → cập nhật
                            avg_conf = np.mean([
                                p[1] for p in prediction_history
                                if p[0] == most_common
                            ])
                            current_gesture = most_common
                            current_confidence = avg_conf
                            gesture_hold_counter = SMOOTHING_FRAMES
                            is_predicting = True
                        elif gesture_hold_counter > 0:
                            # Giữ kết quả cũ (chống flicker)
                            gesture_hold_counter -= 1
                            is_predicting = True
                        else:
                            current_gesture = predicted_gesture
                            current_confidence = confidence
                            is_predicting = True
                    elif gesture_hold_counter > 0:
                        # Confidence thấp nhưng vẫn trong cooldown
                        gesture_hold_counter -= 1
                        is_predicting = True

                # Chỉ xử lý bàn tay đầu tiên
                break
        else:
            # Không detect tay → xóa buffer dần
            if len(frame_buffer) > 0:
                frame_buffer.clear()
            if gesture_hold_counter > 0:
                gesture_hold_counter -= 1
                is_predicting = True

        # ── Cập nhật FPS ─────────────────────────────────────────────────
        frame_count += 1
        if frame_count >= fps_update_interval:
            elapsed = time.time() - fps_start_time
            fps = frame_count / elapsed if elapsed > 0 else 0
            frame_count = 0
            fps_start_time = time.time()

        # ── Vẽ UI ────────────────────────────────────────────────────────
        frame = draw_ui_overlay(
            frame,
            gesture_name=current_gesture,
            confidence=current_confidence,
            fps=fps,
            buffer_fill=len(frame_buffer),
            model_name=current_model_name,
            is_predicting=is_predicting,
        )

        # Viền xanh khi đang nhận diện thành công
        if is_predicting and current_confidence >= CONFIDENCE_THRESHOLD:
            cv2.rectangle(frame, (0, 0),
                          (frame.shape[1] - 1, frame.shape[0] - 1),
                          (80, 230, 120), 3)

        cv2.imshow("Hand Gesture Recognition", frame)

        # ── Xử lý phím ──────────────────────────────────────────────────
        key = cv2.waitKey(1) & 0xFF

        if key == ord("q") or key == ord("Q") or key == 27:
            break

        elif key == ord("m") or key == ord("M"):
            # Chuyển đổi mô hình
            if len(available_models) > 1:
                current_model_idx = (current_model_idx + 1) % len(available_models)
                current_model_name = available_models[current_model_idx]
                model = models[current_model_name]
                frame_buffer.clear()
                prediction_history.clear()
                current_gesture = None
                gesture_hold_counter = 0
                print(f"  → Đã chuyển sang: {current_model_name}")

    # ── Cleanup ──────────────────────────────────────────────────────────
    cap.release()
    cv2.destroyAllWindows()
    hands.close()
    print("\n  ✅ Đã thoát. Cảm ơn bạn đã sử dụng!")


if __name__ == "__main__":
    main()
