"""
mediapipe_server.py — UDP server gửi 21 hand landmarks từ MediaPipe tới Unity.

Protocol: JSON qua UDP, mỗi frame 1 packet.
Format:
    {"tracking": true, "landmarks": [{"x": 0.5, "y": 0.3, "z": -0.01}, ...]}
    {"tracking": false}

Usage:
    python mediapipe_server.py [--port 5065] [--camera 0] [--mirror]
"""

import cv2
import mediapipe as mp
import json
import socket
import time
import argparse
import sys

def parse_args():
    parser = argparse.ArgumentParser(description="MediaPipe Hands → Unity UDP bridge")
    parser.add_argument("--port", type=int, default=5065, help="UDP port (default: 5065)")
    parser.add_argument("--camera", type=int, default=0, help="Camera index (default: 0)")
    parser.add_argument("--mirror", action="store_true", default=True,
                        help="Mirror the webcam horizontally (default: True)")
    parser.add_argument("--no-mirror", action="store_false", dest="mirror")
    parser.add_argument("--width", type=int, default=1280, help="Camera width")
    parser.add_argument("--height", type=int, default=720, help="Camera height")
    parser.add_argument("--show-preview", action="store_true", default=True,
                        help="Show webcam preview window")
    parser.add_argument("--no-preview", action="store_false", dest="show_preview")
    return parser.parse_args()

def main():
    args = parse_args()

    # Fix Windows console encoding
    if sys.platform == "win32":
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')

    UDP_IP = "127.0.0.1"
    UDP_PORT = args.port

    # MediaPipe Hands setup
    mp_hands = mp.solutions.hands
    mp_drawing = mp.solutions.drawing_utils
    hands = mp_hands.Hands(
        static_image_mode=False,
        max_num_hands=1,
        min_detection_confidence=0.7,
        min_tracking_confidence=0.5
    )

    # UDP socket
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    # Webcam
    cap = cv2.VideoCapture(args.camera)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, args.width)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, args.height)

    if not cap.isOpened():
        print(f"[ERROR] Cannot open camera {args.camera}")
        return

    actual_w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    actual_h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    actual_fps = cap.get(cv2.CAP_PROP_FPS)

    print("=" * 60)
    print("  MEDIAPIPE HANDS → UNITY UDP BRIDGE")
    print("=" * 60)
    print(f"  Camera    : {args.camera} ({actual_w}x{actual_h} @ {actual_fps:.0f}fps)")
    print(f"  UDP Target: {UDP_IP}:{UDP_PORT}")
    print(f"  Mirror    : {args.mirror}")
    print(f"  Preview   : {args.show_preview}")
    print("=" * 60)
    print("  Press ESC to quit")
    print()

    frame_count = 0
    tracking_count = 0
    start_time = time.time()

    try:
        while cap.isOpened():
            ret, frame = cap.read()
            if not ret:
                print("[WARN] Failed to read frame")
                continue

            frame_count += 1

            # Mirror for "magic mirror" effect
            if args.mirror:
                frame = cv2.flip(frame, 1)

            # Convert BGR → RGB for MediaPipe
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            rgb.flags.writeable = False
            results = hands.process(rgb)
            rgb.flags.writeable = True

            if results.multi_hand_landmarks:
                hand = results.multi_hand_landmarks[0]
                tracking_count += 1

                landmarks = []
                for lm in hand.landmark:
                    landmarks.append({
                        "x": round(lm.x, 6),
                        "y": round(lm.y, 6),
                        "z": round(lm.z, 6)
                    })

                packet = json.dumps({
                    "tracking": True,
                    "landmarks": landmarks,
                    "timestamp": time.time()
                })
                sock.sendto(packet.encode('utf-8'), (UDP_IP, UDP_PORT))

                # Draw landmarks on preview
                if args.show_preview:
                    mp_drawing.draw_landmarks(
                        frame, hand, mp_hands.HAND_CONNECTIONS)

                    # Show tracking info
                    thumb = hand.landmark[4]
                    index = hand.landmark[8]
                    pinch_dist = ((thumb.x - index.x)**2 +
                                  (thumb.y - index.y)**2 +
                                  (thumb.z - index.z)**2) ** 0.5
                    status = "PINCH" if pinch_dist < 0.05 else "OPEN"
                    cv2.putText(frame, f"Status: {status} (d={pinch_dist:.3f})",
                                (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.8,
                                (0, 255, 0) if status == "OPEN" else (0, 0, 255), 2)
            else:
                # No hand detected
                packet = json.dumps({"tracking": False})
                sock.sendto(packet.encode('utf-8'), (UDP_IP, UDP_PORT))

                if args.show_preview:
                    cv2.putText(frame, "No hand detected", (10, 30),
                                cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 0, 255), 2)

            if args.show_preview:
                # Show FPS and tracking rate
                elapsed = time.time() - start_time
                fps = frame_count / max(elapsed, 0.001)
                track_rate = tracking_count / max(frame_count, 1) * 100
                cv2.putText(frame, f"FPS: {fps:.0f} | Tracking: {track_rate:.0f}%",
                            (10, actual_h - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.6,
                            (255, 255, 255), 1)
                cv2.imshow("MediaPipe Hands → Unity", frame)

                if cv2.waitKey(1) & 0xFF == 27:  # ESC
                    break
            else:
                # Print stats periodically
                if frame_count % 150 == 0:
                    elapsed = time.time() - start_time
                    fps = frame_count / max(elapsed, 0.001)
                    track_rate = tracking_count / max(frame_count, 1) * 100
                    print(f"  [Stats] Frames: {frame_count} | "
                          f"FPS: {fps:.0f} | Tracking: {track_rate:.0f}%")

    except KeyboardInterrupt:
        print("\n[INFO] Interrupted by user")
    finally:
        cap.release()
        if args.show_preview:
            cv2.destroyAllWindows()
        hands.close()
        sock.close()
        print("[INFO] Server stopped")

if __name__ == '__main__':
    main()
