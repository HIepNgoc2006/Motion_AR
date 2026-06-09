"""
=============================================================================
  5_os_controller.py — Điều khiển Hệ điều hành bằng Cử chỉ Tay
=============================================================================
  Dự án  : Dynamic Spatial Hand Gesture Recognition
  Mục đích: Biến webcam thành bộ điều khiển HCI (Human-Computer Interaction)
            — nhận diện cử chỉ tay qua Transformer model rồi ánh xạ sang
            các hành động hệ điều hành: media control, volume, slide,
            drag-and-drop, show desktop.

  ╔══════════════════════════════════════════════════════════════════════╗
  ║  HƯỚNG DẪN CÀI ĐẶT & CHẠY                                        ║
  ║                                                                    ║
  ║  Bước 1 — Kích hoạt môi trường ảo:                                 ║
  ║    cd D:\WORKSAPCE\Monitor_AR\1_AI_Model_Training         ║
  ║    .\venv\Scripts\activate                                         ║
  ║                                                                    ║
  ║  Bước 2 — Cài đặt thư viện:                                       ║
  ║    pip install torch opencv-python mediapipe numpy pyautogui pycaw  ║
  ║    pip install comtypes                                            ║
  ║                                                                    ║
  ║  Bước 3 — Chạy script:                                            ║
  ║    python Scripts/5_os_controller.py                                ║
  ║                                                                    ║
  ║  Bước 4 — Điều khiển:                                              ║
  ║    [Q/ESC] Thoát                                                   ║
  ║    [P]     Chuyển chế độ: Media Player ↔ PowerPoint                ║
  ║    [D]     Bật/Tắt chế độ kéo-thả (Drag & Drop)                   ║
  ╚══════════════════════════════════════════════════════════════════════╝

  ┌──────────────────────────────────────────────────────────────────────┐
  │  BẢNG ÁNH XẠ CỬ CHỈ → HÀNH ĐỘNG HỆ ĐIỀU HÀNH                      │
  ├──────────────┬───────────────────┬──────────────────────────────────┤
  │  Cử chỉ      │  Media Mode       │  PowerPoint Mode                │
  ├──────────────┼───────────────────┼──────────────────────────────────┤
  │  swipe_left  │  Previous Track   │  Slide trước (←)                │
  │  swipe_right │  Next Track       │  Slide tiếp (→)                 │
  │  swipe_up    │  Volume Up +5%    │  Page Up                        │
  │  swipe_down  │  Volume Down -5%  │  Page Down                      │
  │  rotation_cw │  Volume Up +2%    │  Volume Up +2%                  │
  │  rotation_ccw│  Volume Down -2%  │  Volume Down -2%                │
  │  grab        │  Show Desktop     │  Show Desktop                   │
  │  tap         │  Play/Pause       │  Enter (xác nhận)               │
  │  pinch       │  Drag & Drop      │  Drag & Drop                    │
  │  expand      │  Release drag     │  Release drag                   │
  │  shake       │  Mute/Unmute      │  Escape                         │
  └──────────────┴───────────────────┴──────────────────────────────────┘
=============================================================================
"""

import os
import sys
import math
import time
import collections
import threading
import numpy as np

import cv2
import mediapipe as mp
import torch
import torch.nn as nn

# ── PyAutoGUI cho điều khiển bàn phím / chuột ───────────────────────────────
import pyautogui
pyautogui.FAILSAFE = True       # Di chuột góc trên-trái → dừng khẩn cấp
pyautogui.PAUSE = 0.02          # Delay nhỏ giữa các lệnh

# ── PyCaw cho điều khiển âm lượng Windows ────────────────────────────────────
try:
    from ctypes import cast, POINTER
    from comtypes import CLSCTX_ALL
    from pycaw.pycaw import AudioUtilities, IAudioEndpointVolume
    PYCAW_AVAILABLE = True
except ImportError:
    PYCAW_AVAILABLE = False
    print("  [WARN] pycaw không khả dụng — chức năng volume sẽ dùng phím media.")


# ═══════════════════════════════════════════════════════════════════════════════
# CẤU HÌNH
# ═══════════════════════════════════════════════════════════════════════════════
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODEL_DIR = os.path.join(PROJECT_ROOT, "Models")
TRANSFORMER_WEIGHTS = os.path.join(MODEL_DIR, "best_transformer.pth")

SEQ_LEN = 15
NUM_LANDMARKS = 21
NUM_COORDS = 3
INPUT_DIM = 63
D_MODEL = 128
NHEAD = 4
DIM_FEEDFORWARD = 256
NUM_LAYERS_TF = 3

CONFIDENCE_THRESHOLD = 0.80         # Cao hơn inference thông thường → tránh trigger nhầm
SMOOTHING_FRAMES = 5

# ── Cooldown (giây) — ngăn spam lệnh OS ──────────────────────────────────────
GESTURE_COOLDOWNS = {
    "swipe_left":    1.2,   # Không được spam prev/next liên tục
    "swipe_right":   1.2,
    "swipe_up":      0.5,   # Volume có thể nhanh hơn
    "swipe_down":    0.5,
    "rotation_cw":   0.3,   # Volume mịn
    "rotation_ccw":  0.3,
    "grab":          2.5,   # Show desktop cần cooldown dài
    "tap":           1.0,
    "pinch":         0.4,   # Drag cần responsiveness cao
    "expand":        0.8,
    "shake":         1.5,
    "swipe_x":       1.5,
    "swipe_v":       1.5,
    "swipe_plus":    1.5,
}

# Nhãn cử chỉ (alphabetical — khớp LabelEncoder)
GESTURE_LABELS = sorted([
    "expand", "grab", "pinch", "rotation_ccw", "rotation_cw",
    "shake", "swipe_down", "swipe_left", "swipe_plus",
    "swipe_right", "swipe_up", "swipe_v", "swipe_x", "tap"
])
NUM_CLASSES = len(GESTURE_LABELS)


# ═══════════════════════════════════════════════════════════════════════════════
# KIẾN TRÚC MÔ HÌNH (copy từ Tuan7_Transformer.ipynb)
# ═══════════════════════════════════════════════════════════════════════════════
class PositionalEncoding(nn.Module):
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
        x = x + self.pe[:, :x.size(1), :]
        return self.dropout(x)


class GestureTransformer(nn.Module):
    def __init__(self, input_dim=63, d_model=128, nhead=4,
                 dim_feedforward=256, num_layers=3, num_classes=14,
                 dropout=0.2, pe_dropout=0.1):
        super().__init__()
        self.d_model = d_model
        self.input_projection = nn.Linear(input_dim, d_model)
        self.pos_encoder = PositionalEncoding(d_model, 500, pe_dropout)
        encoder_layer = nn.TransformerEncoderLayer(
            d_model=d_model, nhead=nhead,
            dim_feedforward=dim_feedforward,
            dropout=dropout, batch_first=True, activation="relu"
        )
        self.transformer_encoder = nn.TransformerEncoder(encoder_layer, num_layers)
        self.classifier = nn.Sequential(
            nn.Linear(d_model, 64), nn.ReLU(), nn.Dropout(0.3),
            nn.Linear(64, num_classes),
        )

    def forward(self, x):
        x = self.input_projection(x)
        x = self.pos_encoder(x)
        x = self.transformer_encoder(x)
        x = x.mean(dim=1)
        return self.classifier(x)


# ═══════════════════════════════════════════════════════════════════════════════
# ĐIỀU KHIỂN ÂM LƯỢNG (PyCaw + fallback)
# ═══════════════════════════════════════════════════════════════════════════════
class VolumeController:
    """
    Bộ điều khiển âm lượng Windows — sử dụng COM API (PyCaw).
    Nếu không có PyCaw, fallback sang phím media.
    """

    def __init__(self):
        self.volume_interface = None
        self.vol_range = (-65.25, 0.0)   # dB range mặc định

        if PYCAW_AVAILABLE:
            try:
                devices = AudioUtilities.GetSpeakers()
                interface = devices.Activate(
                    IAudioEndpointVolume._iid_, CLSCTX_ALL, None
                )
                self.volume_interface = cast(interface, POINTER(IAudioEndpointVolume))
                self.vol_range = self.volume_interface.GetVolumeRange()[:2]
                print("  [OK] PyCaw volume control initialized")
            except Exception as e:
                print(f"  [WARN] PyCaw init failed: {e}")
                self.volume_interface = None

    def get_volume_percent(self):
        """Lấy âm lượng hiện tại (0-100%)."""
        if self.volume_interface:
            try:
                # GetMasterVolumeLevelScalar trả về 0.0 - 1.0
                return self.volume_interface.GetMasterVolumeLevelScalar() * 100
            except Exception:
                return 50.0
        return 50.0

    def set_volume_percent(self, percent):
        """Đặt âm lượng (0-100%)."""
        percent = max(0.0, min(100.0, percent))
        if self.volume_interface:
            try:
                self.volume_interface.SetMasterVolumeLevelScalar(percent / 100.0, None)
                return True
            except Exception:
                pass
        return False

    def change_volume(self, delta_percent):
        """Thay đổi âm lượng ±delta%. Trả về mức mới."""
        current = self.get_volume_percent()
        new_vol = max(0.0, min(100.0, current + delta_percent))
        if self.volume_interface:
            self.set_volume_percent(new_vol)
        else:
            # Fallback: dùng phím volume
            steps = max(1, abs(int(delta_percent / 2)))
            key = 'volumeup' if delta_percent > 0 else 'volumedown'
            for _ in range(steps):
                pyautogui.press(key)
        return new_vol

    def toggle_mute(self):
        """Bật/tắt mute."""
        if self.volume_interface:
            try:
                is_muted = self.volume_interface.GetMute()
                self.volume_interface.SetMute(not is_muted, None)
                return not is_muted
            except Exception:
                pass
        pyautogui.press('volumemute')
        return None


# ═══════════════════════════════════════════════════════════════════════════════
# HUD — Heads-Up Display cho trạng thái hành động OS
# ═══════════════════════════════════════════════════════════════════════════════
class ActionHUD:
    """
    Hiển thị hành động OS vừa trigger dưới dạng flash text.
    Text sáng dần → giữ → mờ dần (fade in → hold → fade out).
    """

    def __init__(self, display_duration=1.5):
        self.message = ""
        self.color = (0, 255, 0)
        self.trigger_time = 0.0
        self.duration = display_duration
        self.icon = ""

    def trigger(self, message, color=(0, 255, 100), icon=">>"):
        """Kích hoạt hiển thị thông báo mới."""
        self.message = message
        self.color = color
        self.trigger_time = time.time()
        self.icon = icon

    def draw(self, frame):
        """Vẽ thông báo lên frame nếu đang active."""
        elapsed = time.time() - self.trigger_time
        if elapsed > self.duration or not self.message:
            return frame

        # Tính opacity (fade in → hold → fade out)
        if elapsed < 0.15:
            alpha = elapsed / 0.15
        elif elapsed < self.duration - 0.4:
            alpha = 1.0
        else:
            alpha = (self.duration - elapsed) / 0.4

        alpha = max(0.0, min(1.0, alpha))

        h, w, _ = frame.shape

        # ── Panel nền ────────────────────────────────────────────────────
        panel_h = 60
        panel_y = h // 2 - panel_h // 2
        overlay = frame.copy()
        cv2.rectangle(overlay, (0, panel_y), (w, panel_y + panel_h),
                      (10, 10, 10), -1)
        frame = cv2.addWeighted(overlay, 0.7 * alpha, frame, 1.0 - 0.7 * alpha, 0)

        # ── Flash text ───────────────────────────────────────────────────
        text = f"{self.icon}  {self.message}  {self.icon}"
        font = cv2.FONT_HERSHEY_SIMPLEX
        scale = 1.0
        thickness = 3

        # Tính kích thước text để căn giữa
        (tw, th), _ = cv2.getTextSize(text, font, scale, thickness)
        tx = (w - tw) // 2
        ty = panel_y + panel_h // 2 + th // 2

        # Màu với alpha
        color = tuple(int(c * alpha) for c in self.color)

        # Viền đen (shadow)
        cv2.putText(frame, text, (tx + 2, ty + 2), font, scale,
                    (0, 0, 0), thickness + 1, cv2.LINE_AA)
        # Text chính
        cv2.putText(frame, text, (tx, ty), font, scale,
                    color, thickness, cv2.LINE_AA)

        # ── Thanh volume (nếu là hành động volume) ───────────────────────
        if "VOLUME" in self.message and "%" in self.message:
            try:
                vol_str = self.message.split()[-1].replace("%", "")
                vol_pct = float(vol_str)
                bar_y = panel_y + panel_h + 5
                bar_w = int(vol_pct / 100 * (w - 40))
                cv2.rectangle(frame, (20, bar_y), (w - 20, bar_y + 8),
                              (40, 40, 40), -1)
                bar_color = (80, 230, 120) if vol_pct < 80 else (80, 180, 255)
                cv2.rectangle(frame, (20, bar_y), (20 + bar_w, bar_y + 8),
                              tuple(int(c * alpha) for c in bar_color), -1)
            except (ValueError, IndexError):
                pass

        return frame


# ═══════════════════════════════════════════════════════════════════════════════
# BỘ ĐIỀU KHIỂN HÀNH ĐỘNG OS
# ═══════════════════════════════════════════════════════════════════════════════
class OSActionDispatcher:
    """
    Ánh xạ cử chỉ tay → hành động hệ điều hành.
    Tự quản lý cooldown cho từng cử chỉ.
    """

    def __init__(self, volume_ctrl, hud):
        self.volume = volume_ctrl
        self.hud = hud
        self.mode = "media"   # "media" hoặc "ppt"
        self.is_dragging = False
        self.drag_enabled = True

        # Thời điểm trigger cuối của từng cử chỉ
        self.last_action_time = {g: 0.0 for g in GESTURE_LABELS}

    def can_trigger(self, gesture):
        """Kiểm tra cooldown cho cử chỉ."""
        cooldown = GESTURE_COOLDOWNS.get(gesture, 1.0)
        return (time.time() - self.last_action_time.get(gesture, 0)) >= cooldown

    def dispatch(self, gesture, confidence, index_tip_xy=None):
        """
        Thực hiện hành động OS tương ứng với cử chỉ.

        Args:
            gesture: Tên cử chỉ đã nhận diện.
            confidence: Độ tin cậy (0-1).
            index_tip_xy: Tọa độ (x, y) ngón trỏ trên màn hình (cho drag).
        
        Returns:
            True nếu hành động được trigger thành công.
        """
        if not self.can_trigger(gesture):
            return False

        self.last_action_time[gesture] = time.time()
        triggered = True

        # ═══════════════════════════════════════════════════════════════════
        # SWIPE LEFT / RIGHT — Media hoặc Slide
        # ═══════════════════════════════════════════════════════════════════
        if gesture == "swipe_left":
            if self.mode == "media":
                pyautogui.press('prevtrack')
                self.hud.trigger("PREVIOUS TRACK", (255, 180, 80), "<<")
            else:
                pyautogui.press('left')
                self.hud.trigger("SLIDE TRUOC", (255, 180, 80), "<<")

        elif gesture == "swipe_right":
            if self.mode == "media":
                pyautogui.press('nexttrack')
                self.hud.trigger("NEXT TRACK", (80, 180, 255), ">>")
            else:
                pyautogui.press('right')
                self.hud.trigger("SLIDE TIEP", (80, 180, 255), ">>")

        # ═══════════════════════════════════════════════════════════════════
        # SWIPE UP / DOWN — Volume lớn ±5%
        # ═══════════════════════════════════════════════════════════════════
        elif gesture == "swipe_up":
            if self.mode == "ppt":
                pyautogui.press('pageup')
                self.hud.trigger("PAGE UP", (120, 220, 255), "^^")
            else:
                new_vol = self.volume.change_volume(+5)
                self.hud.trigger(f"VOLUME UP {new_vol:.0f}%", (80, 255, 120), "++")

        elif gesture == "swipe_down":
            if self.mode == "ppt":
                pyautogui.press('pagedown')
                self.hud.trigger("PAGE DOWN", (120, 220, 255), "vv")
            else:
                new_vol = self.volume.change_volume(-5)
                self.hud.trigger(f"VOLUME DOWN {new_vol:.0f}%", (255, 160, 80), "--")

        # ═══════════════════════════════════════════════════════════════════
        # ROTATION CW / CCW — Volume mịn ±2%
        # ═══════════════════════════════════════════════════════════════════
        elif gesture == "rotation_cw":
            new_vol = self.volume.change_volume(+2)
            self.hud.trigger(f"VOLUME UP {new_vol:.0f}%", (80, 255, 120), "+")

        elif gesture == "rotation_ccw":
            new_vol = self.volume.change_volume(-2)
            self.hud.trigger(f"VOLUME DOWN {new_vol:.0f}%", (255, 160, 80), "-")

        # ═══════════════════════════════════════════════════════════════════
        # GRAB — Show / Hide Desktop
        # ═══════════════════════════════════════════════════════════════════
        elif gesture == "grab":
            pyautogui.hotkey('win', 'd')
            self.hud.trigger("SHOW DESKTOP", (180, 120, 255), "##")

        # ═══════════════════════════════════════════════════════════════════
        # TAP — Play/Pause hoặc Enter
        # ═══════════════════════════════════════════════════════════════════
        elif gesture == "tap":
            if self.mode == "media":
                pyautogui.press('playpause')
                self.hud.trigger("PLAY / PAUSE", (255, 255, 100), "||")
            else:
                pyautogui.press('enter')
                self.hud.trigger("ENTER", (255, 255, 100), "OK")

        # ═══════════════════════════════════════════════════════════════════
        # PINCH — Bắt đầu Drag (giữ chuột trái)
        # ═══════════════════════════════════════════════════════════════════
        elif gesture == "pinch":
            if self.drag_enabled and index_tip_xy:
                sx, sy = index_tip_xy
                if not self.is_dragging:
                    pyautogui.moveTo(sx, sy, duration=0.05)
                    pyautogui.mouseDown(button='left')
                    self.is_dragging = True
                    self.hud.trigger("DRAG START", (255, 100, 255), "><")
                else:
                    # Đang drag → di chuyển chuột theo ngón trỏ
                    pyautogui.moveTo(sx, sy, duration=0.02)
            else:
                triggered = False

        # ═══════════════════════════════════════════════════════════════════
        # EXPAND — Thả Drag (release chuột)
        # ═══════════════════════════════════════════════════════════════════
        elif gesture == "expand":
            if self.is_dragging:
                pyautogui.mouseUp(button='left')
                self.is_dragging = False
                self.hud.trigger("DRAG RELEASE", (100, 255, 200), "<>")
            else:
                triggered = False

        # ═══════════════════════════════════════════════════════════════════
        # SHAKE — Mute/Unmute hoặc Escape
        # ═══════════════════════════════════════════════════════════════════
        elif gesture == "shake":
            if self.mode == "media":
                is_muted = self.volume.toggle_mute()
                status = "MUTED" if is_muted else "UNMUTED"
                color = (255, 80, 80) if is_muted else (80, 255, 120)
                self.hud.trigger(f"AUDIO {status}", color, "XX")
            else:
                pyautogui.press('escape')
                self.hud.trigger("ESCAPE", (255, 80, 80), "XX")

        else:
            triggered = False

        if triggered:
            print(f"  [{self.mode.upper():>5}] {gesture:15s}  "
                  f"(conf: {confidence:.0%})  → {self.hud.message}")

        return triggered

    def toggle_mode(self):
        """Chuyển đổi Media ↔ PowerPoint."""
        self.mode = "ppt" if self.mode == "media" else "media"
        label = "PowerPoint" if self.mode == "ppt" else "Media Player"
        self.hud.trigger(f"MODE: {label}", (200, 200, 255), "~~")
        return self.mode

    def cleanup(self):
        """Dọn dẹp trạng thái kéo-thả."""
        if self.is_dragging:
            pyautogui.mouseUp(button='left')
            self.is_dragging = False


# ═══════════════════════════════════════════════════════════════════════════════
# TIỆN ÍCH — Trích xuất Landmark & Tọa độ Ngón trỏ
# ═══════════════════════════════════════════════════════════════════════════════
def extract_landmarks(hand_landmarks):
    coords = []
    for lm in hand_landmarks.landmark:
        coords.extend([lm.x, lm.y, lm.z])
    return coords


def normalize_frame(landmarks_flat):
    arr = np.array(landmarks_flat, dtype=np.float32).reshape(NUM_LANDMARKS, NUM_COORDS)
    wrist = arr[0:1, :]
    centered = arr - wrist
    max_val = np.abs(centered).max()
    if max_val > 1e-8:
        centered = centered / max_val
    return centered.flatten().tolist()


def get_index_tip_screen_coords(hand_landmarks, frame_w, frame_h):
    """
    Lấy tọa độ đầu ngón trỏ (landmark 8) và chuyển sang tọa độ màn hình.
    MediaPipe trả về tọa độ chuẩn hóa [0,1] → nhân với kích thước màn hình.
    """
    index_tip = hand_landmarks.landmark[8]
    screen_w, screen_h = pyautogui.size()

    # Mirrored camera → đảo x
    sx = int((1.0 - index_tip.x) * screen_w)
    sy = int(index_tip.y * screen_h)

    # Clamp vào vùng an toàn
    sx = max(5, min(screen_w - 5, sx))
    sy = max(5, min(screen_h - 5, sy))

    return sx, sy


# ═══════════════════════════════════════════════════════════════════════════════
# VẼ HUD GIAO DIỆN
# ═══════════════════════════════════════════════════════════════════════════════
def draw_controller_hud(frame, gesture, confidence, fps, buffer_fill,
                        mode, is_dragging, drag_enabled, action_hud):
    h, w, _ = frame.shape

    # ── Header ───────────────────────────────────────────────────────────
    overlay = frame.copy()
    cv2.rectangle(overlay, (0, 0), (w, 100), (10, 10, 10), -1)
    frame = cv2.addWeighted(overlay, 0.82, frame, 0.18, 0)

    # Tiêu đề
    cv2.putText(frame, "HCI OS CONTROLLER", (15, 30),
                cv2.FONT_HERSHEY_SIMPLEX, 0.8, (255, 255, 255), 2, cv2.LINE_AA)

    # Mode badge
    mode_text = "MEDIA PLAYER" if mode == "media" else "POWERPOINT"
    mode_color = (80, 200, 255) if mode == "media" else (255, 160, 80)
    cv2.putText(frame, mode_text, (15, 55),
                cv2.FONT_HERSHEY_SIMPLEX, 0.5, mode_color, 2, cv2.LINE_AA)

    # Drag status
    if drag_enabled:
        drag_text = "DRAGGING" if is_dragging else "DRAG: ON"
        drag_color = (255, 100, 255) if is_dragging else (120, 180, 120)
    else:
        drag_text = "DRAG: OFF"
        drag_color = (120, 120, 120)
    cv2.putText(frame, drag_text, (15, 80),
                cv2.FONT_HERSHEY_SIMPLEX, 0.45, drag_color, 1, cv2.LINE_AA)

    # FPS
    fps_color = (100, 255, 100) if fps > 20 else (100, 200, 255) if fps > 10 else (100, 100, 255)
    cv2.putText(frame, f"FPS: {fps:.0f}", (w - 110, 30),
                cv2.FONT_HERSHEY_SIMPLEX, 0.6, fps_color, 2, cv2.LINE_AA)

    # Buffer bar
    bar_x, bar_y, bar_w, bar_h = w - 220, 50, 200, 10
    fill_ratio = buffer_fill / SEQ_LEN
    cv2.rectangle(frame, (bar_x, bar_y), (bar_x + bar_w, bar_y + bar_h),
                  (50, 50, 50), -1)
    fill_w = int(fill_ratio * bar_w)
    fill_color = (80, 200, 120) if fill_ratio >= 1.0 else (255, 200, 80)
    cv2.rectangle(frame, (bar_x, bar_y), (bar_x + fill_w, bar_y + bar_h),
                  fill_color, -1)
    cv2.putText(frame, f"{buffer_fill}/{SEQ_LEN}", (bar_x + bar_w + 5, bar_y + 10),
                cv2.FONT_HERSHEY_SIMPLEX, 0.35, (160, 160, 160), 1)

    # Hotkeys
    cv2.putText(frame, "[Q] Thoat  [P] Mode  [D] Drag", (w - 320, 85),
                cv2.FONT_HERSHEY_SIMPLEX, 0.38, (100, 100, 100), 1, cv2.LINE_AA)

    # ── Gesture Prediction Panel (bottom) ────────────────────────────────
    if gesture:
        panel_h = 70
        overlay2 = frame.copy()
        cv2.rectangle(overlay2, (0, h - panel_h), (w, h), (10, 10, 10), -1)
        frame = cv2.addWeighted(overlay2, 0.82, frame, 0.18, 0)

        conf_color = (80, 230, 120) if confidence >= CONFIDENCE_THRESHOLD else (180, 180, 180)
        cv2.putText(frame, gesture.upper(), (20, h - 35),
                    cv2.FONT_HERSHEY_SIMPLEX, 1.1, conf_color, 3, cv2.LINE_AA)
        cv2.putText(frame, f"{confidence*100:.1f}%", (w - 100, h - 35),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, conf_color, 2, cv2.LINE_AA)

        # Mini confidence bar
        conf_bar_y = h - 15
        bar_fill = int(confidence * (w - 40))
        cv2.rectangle(frame, (20, conf_bar_y), (w - 20, conf_bar_y + 6),
                      (30, 30, 30), -1)
        cv2.rectangle(frame, (20, conf_bar_y), (20 + bar_fill, conf_bar_y + 6),
                      conf_color, -1)

    # ── Action HUD (center flash) ────────────────────────────────────────
    frame = action_hud.draw(frame)

    return frame


# ═══════════════════════════════════════════════════════════════════════════════
# MAIN — VÒNG LẶP ĐIỀU KHIỂN
# ═══════════════════════════════════════════════════════════════════════════════
def main():
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

    print(f"\n{'='*65}")
    print(f"  HCI OS CONTROLLER — Dieu khien He dieu hanh bang Cu chi Tay")
    print(f"{'='*65}")
    print(f"  Device         : {device}")
    print(f"  Confidence     : >= {CONFIDENCE_THRESHOLD*100:.0f}%")
    print(f"  Model          : GestureTransformer")
    print(f"{'='*65}")

    # ── Tải mô hình ──────────────────────────────────────────────────────
    if not os.path.isfile(TRANSFORMER_WEIGHTS):
        print(f"\n  [LOI] Khong tim thay: {TRANSFORMER_WEIGHTS}")
        print(f"        Chay notebook Tuan7_Transformer.ipynb truoc!")
        sys.exit(1)

    model = GestureTransformer(
        input_dim=INPUT_DIM, d_model=D_MODEL, nhead=NHEAD,
        dim_feedforward=DIM_FEEDFORWARD, num_layers=NUM_LAYERS_TF,
        num_classes=NUM_CLASSES, dropout=0.2, pe_dropout=0.1
    ).to(device)

    state = torch.load(TRANSFORMER_WEIGHTS, map_location=device, weights_only=True)
    model.load_state_dict(state)
    model.eval()
    params = sum(p.numel() for p in model.parameters())
    print(f"  [OK] Transformer loaded ({params:,} params)")

    # ── Khởi tạo hệ thống ───────────────────────────────────────────────
    volume_ctrl = VolumeController()
    action_hud = ActionHUD(display_duration=1.5)
    dispatcher = OSActionDispatcher(volume_ctrl, action_hud)

    mp_hands = mp.solutions.hands
    mp_drawing = mp.solutions.drawing_utils
    mp_drawing_styles = mp.solutions.drawing_styles

    hands = mp_hands.Hands(
        static_image_mode=False, max_num_hands=1,
        min_detection_confidence=0.7, min_tracking_confidence=0.6,
        model_complexity=1,
    )

    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("\n  [LOI] Khong the mo webcam!")
        sys.exit(1)

    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
    cam_w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    cam_h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    print(f"  [OK] Webcam: {cam_w}x{cam_h}")

    # ── State ────────────────────────────────────────────────────────────
    frame_buffer = collections.deque(maxlen=SEQ_LEN)
    prediction_history = collections.deque(maxlen=SMOOTHING_FRAMES)
    current_gesture = None
    current_confidence = 0.0
    gesture_hold_counter = 0

    fps = 0.0
    frame_count = 0
    fps_start = time.time()

    initial_vol = volume_ctrl.get_volume_percent()
    print(f"  [OK] Volume: {initial_vol:.0f}%")
    print(f"\n  San sang! Dua tay vao khung hinh webcam.")
    print(f"  [Q] Thoat | [P] Media/PPT | [D] Drag On/Off\n")

    # ── Main Loop ────────────────────────────────────────────────────────
    while True:
        ret, frame = cap.read()
        if not ret:
            continue

        frame = cv2.flip(frame, 1)
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = hands.process(rgb)

        index_tip_screen = None

        if results.multi_hand_landmarks:
            for hand_lm in results.multi_hand_landmarks:
                # Vẽ skeleton tay
                mp_drawing.draw_landmarks(
                    frame, hand_lm, mp_hands.HAND_CONNECTIONS,
                    mp_drawing_styles.get_default_hand_landmarks_style(),
                    mp_drawing_styles.get_default_hand_connections_style(),
                )

                # Tọa độ ngón trỏ cho drag
                index_tip_screen = get_index_tip_screen_coords(
                    hand_lm, cam_w, cam_h
                )

                # Trích xuất + chuẩn hóa
                raw = extract_landmarks(hand_lm)
                normalized = normalize_frame(raw)
                frame_buffer.append(normalized)

                # ── Suy luận ─────────────────────────────────────────
                if len(frame_buffer) == SEQ_LEN:
                    seq = np.array(list(frame_buffer), dtype=np.float32)
                    tensor = torch.tensor(seq).unsqueeze(0).to(device)

                    with torch.no_grad():
                        logits = model(tensor)
                        probs = torch.softmax(logits, dim=1)
                        conf, pred_idx = probs.max(dim=1)
                        conf = conf.item()
                        pred_idx = pred_idx.item()

                    predicted = GESTURE_LABELS[pred_idx]
                    prediction_history.append((predicted, conf))

                    # Smoothing
                    if conf >= CONFIDENCE_THRESHOLD:
                        recents = [p[0] for p in prediction_history]
                        most_common = max(set(recents), key=recents.count)
                        consistency = recents.count(most_common) / len(recents)

                        if consistency >= 0.6:
                            avg_c = np.mean([p[1] for p in prediction_history
                                             if p[0] == most_common])
                            # ── Chỉ trigger nếu gesture MỚI hoặc gesture lặp có cooldown ok
                            if most_common != current_gesture or dispatcher.can_trigger(most_common):
                                current_gesture = most_common
                                current_confidence = avg_c
                                gesture_hold_counter = SMOOTHING_FRAMES

                                # DISPATCH HÀNH ĐỘNG OS
                                dispatcher.dispatch(
                                    current_gesture, current_confidence,
                                    index_tip_xy=index_tip_screen
                                )
                    elif gesture_hold_counter > 0:
                        gesture_hold_counter -= 1
                    else:
                        current_gesture = None
                        current_confidence = 0.0

                    # Drag tracking: nếu đang drag + gesture là pinch → cập nhật vị trí
                    if dispatcher.is_dragging and index_tip_screen:
                        pyautogui.moveTo(index_tip_screen[0], index_tip_screen[1],
                                         duration=0.01)

                break  # Chỉ xử lý 1 tay
        else:
            # Không detect tay
            if len(frame_buffer) > 0:
                frame_buffer.clear()
            if gesture_hold_counter > 0:
                gesture_hold_counter -= 1
            else:
                current_gesture = None

            # Nếu đang drag mà mất tay → release
            if dispatcher.is_dragging:
                dispatcher.cleanup()
                action_hud.trigger("DRAG LOST", (255, 80, 80), "!!")

        # ── FPS ──────────────────────────────────────────────────────────
        frame_count += 1
        if frame_count >= 10:
            elapsed = time.time() - fps_start
            fps = frame_count / elapsed if elapsed > 0 else 0
            frame_count = 0
            fps_start = time.time()

        # ── Vẽ HUD ──────────────────────────────────────────────────────
        frame = draw_controller_hud(
            frame, current_gesture, current_confidence, fps,
            len(frame_buffer), dispatcher.mode,
            dispatcher.is_dragging, dispatcher.drag_enabled, action_hud
        )

        # Viền khi trigger thành công
        elapsed_since = time.time() - action_hud.trigger_time
        if elapsed_since < action_hud.duration and action_hud.message:
            border_alpha = max(0, 1.0 - elapsed_since / action_hud.duration)
            border_color = tuple(int(c * border_alpha) for c in action_hud.color)
            cv2.rectangle(frame, (0, 0), (frame.shape[1]-1, frame.shape[0]-1),
                          border_color, 4)

        cv2.imshow("HCI OS Controller", frame)

        # ── Phím ─────────────────────────────────────────────────────────
        key = cv2.waitKey(1) & 0xFF

        if key == ord('q') or key == ord('Q') or key == 27:
            break
        elif key == ord('p') or key == ord('P'):
            new_mode = dispatcher.toggle_mode()
            print(f"  → Mode: {new_mode}")
        elif key == ord('d') or key == ord('D'):
            dispatcher.drag_enabled = not dispatcher.drag_enabled
            status = "ON" if dispatcher.drag_enabled else "OFF"
            action_hud.trigger(f"DRAG: {status}",
                               (120, 255, 120) if dispatcher.drag_enabled else (180, 180, 180), "~~")
            print(f"  → Drag: {status}")

    # ── Cleanup ──────────────────────────────────────────────────────────
    dispatcher.cleanup()
    cap.release()
    cv2.destroyAllWindows()
    hands.close()
    print("\n  Da thoat. Cam on ban da su dung HCI OS Controller!")


if __name__ == "__main__":
    main()
