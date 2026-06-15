# ✅ NHẬT KÝ CÔNG VIỆC — MONITOR AR PROJECT
## Cập nhật lần cuối: 2026-06-15 | Tổng: 34 C# scripts

> **Hướng dẫn**: Mỗi khi hoàn thành task → đổi `[ ]` thành `[x]`.
> Task đang làm → đổi thành `[/]`.
> AI phải cập nhật file này sau mỗi phiên (theo `.ai_rules.md`)

---

## PHASE 0: CHUẨN BỊ MÔI TRƯỜNG & VERIFY
> **Trạng thái: 🟡 Chưa xác nhận (cần mở Unity)**

- [ ] **0.1** Mở Unity 2022.3.x LTS → Open project `2_Unity_Project/Motion_AR/`
- [ ] **0.2** Kiểm tra Console: **0 lỗi đỏ**
- [ ] **0.3** Verify packages trong `manifest.json` (Sentis 2.1.3, URP 14.0.12, Newtonsoft 3.2.2, TMP 3.0.7)
- [ ] **0.4** Verify plugins: UniTask (`Assets/Plugins/UniTask/`), sqlite-net-pcl DLL
- [ ] **0.5** Verify ONNX model: `gesture_model.onnx` (234KB) + `.onnx.data` (1.9MB)
- [ ] **0.6** Verify `Assets/StreamingAssets/config.json` tồn tại
- [ ] **0.7** Cài Python environment: `pip install -r requirements.txt`
- [ ] **0.8** Cấu hình biến môi trường `GEMINI_API_KEY`

---

## PHASE 1: HOÀN THIỆN CORE ENGINE & DATA LAYER
> **Trạng thái: 🟡 95% code — chưa test trong Unity**

- [x] **1.1** Export StandardScaler Params (`8_export_scaler.py` → `scaler_params.json`)
- [x] **1.2** Thêm Normalize Logic trong `GestureDetector.cs` (`LoadScalerParams`, `NormalizeFeatures`)
- [ ] **1.3** Setup Scene Cơ Bản (`TestScene.unity` — phải làm trong Unity Editor)
  - [ ] Tạo Hierarchy: Camera, Lights, System GOs, Parts, SnapZones
  - [ ] Tạo Layer "Grabbable"
  - [ ] Assign references (ONNX model, GestureDetector, etc.)
- [x] **1.4** Tạo `GrabbablePart.cs` (IGrabbable implementation)
- [ ] **1.5** Tạo LessonData ScriptableObject mẫu (Right-click trong Unity)
- [x] **1.6** Tạo `TrainingFlowController.cs` (Orchestrator: PinchGrabber → SnapZone → SessionManager)
- [ ] **1.7** Test End-to-End với MockProvider
  - [ ] CursorVisual di chuyển theo chuột
  - [ ] Click grab → Part đổi màu cyan
  - [ ] Drag vào SnapZone → snap + audio
  - [ ] Console: "[SessionManager] Advanced to step 1"
  - [ ] Kéo sai → "[SessionManager] Mistake recorded"
  - [ ] ONNX model loaded successfully (console log)
  - [ ] Scaler params loaded successfully (console log)

---

## PHASE 2: TÍCH HỢP HAND TRACKING THỰC TẾ
> **Trạng thái: 🟡 80% code — chưa test webcam thực**

- [x] **2.1** Chọn phương án MediaPipe → **Python Bridge (UDP)** ✅
- [x] **2.2** Implement Python MediaPipe Server (`mediapipe_server.py`)
- [x] **2.3** Implement Unity UDP Receiver (`MediaPipeUDPProvider.cs`)
- [x] **2.4** Webcam Background Renderer (`WebcamBackground.cs`)
- [ ] **2.5** Test hand tracking thực tế qua webcam
  - [ ] Python server chạy → preview webcam
  - [ ] Unity nhận UDP → cursor theo tay
  - [ ] Pinch bằng tay → grab object
  - [ ] Release → snap hoạt động
  - [ ] Timeout 0.3s → auto-release

---

## PHASE 3: UI/UX HOLOGRAPHIC HUD
> **Trạng thái: 🟡 80% code — chưa tạo prefabs/Canvas trong Unity**

- [x] **3.1** `CursorEffects.cs` — Ring + pulse + trail, subscribe OnPinchUpdate
- [x] **3.2** `SlidePanel.cs` — 4 direction slide, cubic easing, auto-hide
- [x] **3.3** `HoverButton.cs` — Radial loading ring, hover 0.3s activate
- [x] **3.4** `ObjectHighlight.cs` — Emission glow via MaterialPropertyBlock
- [ ] **3.5** URP Bloom setup — Post-processing Volume (cần làm trong Unity)
- [x] **3.6** `InstructionDisplay.cs` — Subscribe OnStepChanged, fade transition
- [x] **3.7** `ErrorOverlay.cs` — Screen flash red, subscribe OnMistakeRecorded
- [x] **3.8** `ScoreDisplay.cs` — Pop-in animation, score counting, grade S/A/B/C/F
- [x] **3.9** `ProgressTracker.cs` — Step checklist + progress bar
- [x] **3.10** `HUDManager.cs` — Central coordinator, gesture → panel toggle
- [ ] **3.11** Tạo prefabs và Canvas hierarchy trong Unity Editor
- [ ] **3.12** Import font Inter/Roboto vào TextMeshPro
- [ ] **3.13** Tạo Outline Shader (URP compatible)
- [ ] **3.14** Audio assets (beep, click, snap sounds)

---

## PHASE 4: TÍCH HỢP AI TUTOR
> **Trạng thái: 🟡 Code hoàn thành — chưa test thực tế**

- [x] **4.1** `VoiceInputManager.cs` — Mic recording, gesture-triggered, auto-stop
- [ ] **4.2** Speech-to-Text integration (Gemini multimodal hoặc Google STT)
- [x] **4.3** `AIChatPanel.cs` — Pipeline text/voice → Gemini → chat bubbles → highlight
- [x] **4.4** Context Integration — ContextBuilder dùng PinchGrabber.GrabbedPartID + SnapZone.AllZones
- [x] **4.5** `HighlightResponder.cs` — Parse AI response → emission pulse highlight
- [ ] **4.6** TTS (Text-to-Speech) — Đọc câu trả lời AI (optional)

---

## PHASE 5: TỐI ƯU & BUILD
> **Trạng thái: 🟡 UI code hoàn thành — chưa profiling/build**

- [ ] **5.1** Profiling — Unity Profiler → xác định bottleneck
- [ ] **5.2** ONNX Inference Optimization — Batch mỗi 2-3 frames
- [ ] **5.3** Object Pooling — Pool cho audio sources, UI elements
- [ ] **5.4** LOD cho mô hình 3D
- [ ] **5.5** Dynamic Z-Axis Calibration — Bounding box + push-pull gesture
- [x] **5.6** `LoginScreen.cs` — UI đăng nhập + SQLiteConnector integration
- [x] **5.7** `SessionHistoryPanel.cs` — Bảng lịch sử phiên học
- [x] **5.8** `LessonSelectPanel.cs` — Menu chọn bài học
- [x] **5.9** `AudioFeedbackManager.cs` — 10 audio clips, singleton
- [x] **5.10** `SceneBootstrapper.cs` — Validate references + auto-init DB
- [x] **5.11** `SettingsManager.cs` — PlayerPrefs settings, InputMode enum
- [ ] **5.12** Audio assets integration — Import sound files
- [ ] **5.13** IL2CPP Windows Build — .exe standalone
- [ ] **5.14** User testing — 3-5 người dùng thực

---

## PHASE 6: MỞ RỘNG TƯƠNG LAI
> **Trạng thái: ❌ Chưa bắt đầu**

- [ ] **6.1** Multi-webcam support
- [ ] **6.2** Multiplayer (LAN)
- [ ] **6.3** Export Report (PDF)
- [ ] **6.4** Custom 3D Models (FBX/OBJ import)
- [ ] **6.5** Mobile Companion App
- [ ] **6.6** Cloud Sync

---

## HẠ TẦNG DỰ ÁN (Infrastructure)

- [x] **I.1** `.gitignore` — Cấu hình loại trừ files
- [x] **I.2** `auto_commit.py` — Script tự động commit (watchdog + debounce)
- [ ] **I.3** Auto-commit script **đang chạy** liên tục (cần khởi động)
- [x] **I.4** `.ai_rules.md` — Quy tắc bắt buộc cho AI
- [x] **I.5** `DEV_LOG.md` — Nhật ký phát triển
- [x] **I.6** `task.md` — File này (nhật ký công việc)
- [x] **I.7** `CHAT_HISTORY.md` — Lưu lịch sử chat AI
- [x] **I.8** `README.md` — Documentation dự án
- [x] **I.9** `ROADMAP.md` / `implementation_plan.md` — Kế hoạch chi tiết

---

## THỐNG KÊ TỔNG HỢP

| Metric | Giá trị |
|---|---|
| Tổng C# scripts | 34 files |
| Tasks hoàn thành (code) | ~25/50 |
| Tasks cần Unity Editor | ~12 |
| Phases hoàn thành 100% | 0 (Phase 0 chưa verify) |
| Bugs đã fix | 13 |
| Phiên làm việc | 7 |
