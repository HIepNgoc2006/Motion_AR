# 💬 LỊCH SỬ CHAT AI — MONITOR AR PROJECT
## File này lưu lại toàn bộ lịch sử trao đổi giữa User và AI
## Mục đích: Tránh mất lịch sử chat, dễ tra cứu, backup

> **Quy tắc**: AI PHẢI ghi vào file này sau mỗi phiên trao đổi.
> Format: Timestamp + Yêu cầu User + Phản hồi AI (tóm tắt) + Thay đổi code

---

## Phiên 1 — 2026-05-30 10:00 | Kiểm toán ban đầu & Bug Fix

**👤 User:** Rà soát toàn bộ dự án, sửa lỗi Console đỏ trong Unity, cập nhật planning documents.

**🤖 AI:** 
- Đọc toàn bộ 11 file C# trong `Assets/Scripts/`
- Phát hiện và sửa 5 lỗi critical/high:
  1. `SQLiteConnector.cs`: `SQLiteAsyncConnection` → `SQLiteConnection` + `UniTask.RunOnThreadPool`
  2. `GestureDetector.cs`: Thêm Softmax cho raw logits
  3. `HandData.cs`: Bridge MockProvider → ConcurrentQueue
  4. `PinchGrabber.cs`: Refactor single source of truth
  5. Copy `gesture_model.onnx.data` weights
- **Files changed**: SQLiteConnector.cs, GestureDetector.cs, HandData.cs, PinchGrabber.cs, gesture_model.onnx.data

---

## Phiên 2 — 2026-05-30 14:00 | Sentis 2.1.3 API Migration

**👤 User:** Tiếp tục rà soát API compatibility, sửa lỗi Console đỏ còn lại.

**🤖 AI:**
- Research Sentis 2.1.3 API changes
- Sửa 7 chỗ trong `GestureDetector.cs`:
  - `TensorFloat` → `Tensor<float>`
  - `CompleteOperationsAndDownload()` → `ReadbackAndClone()`
  - Thêm `cpuOutputTensor?.Dispose()`
- Tạo ROADMAP V1.0 (7 phases, 32 tasks)
- **Files changed**: GestureDetector.cs, Motion AR.md, implementation_plan.md

---

## Phiên 3 — 2026-05-30 18:00 | Phase 1 & 2 Implementation

**👤 User:** Triển khai Phase 1 (Core Engine) và Phase 2 (Hand Tracking).

**🤖 AI:**
- Chạy `8_export_scaler.py` → `scaler_params.json`
- Sửa `GestureDetector.cs`: thêm LoadScalerParams, NormalizeFeatures
- Tạo 5 file mới:
  - `GrabbablePart.cs` — IGrabbable implementation
  - `TrainingFlowController.cs` — Orchestrator
  - `MediaPipeUDPProvider.cs` — UDP receiver
  - `WebcamBackground.cs` — Webcam feed
  - `mediapipe_server.py` — Python MediaPipe bridge
- **Files changed**: GestureDetector.cs + 5 new files
- **Kết quả**: 15 C# scripts total

---

## Phiên 4 — 2026-06-09 10:00 | DEV_LOG + AI Rules + Phase 3 UI/UX

**👤 User:** 
1. Tạo file DEV_LOG ghi lại quá trình làm việc
2. Tạo file .ai_rules.md bắt buộc AI ghi log
3. Tiếp tục Phase 3

**🤖 AI:**
- Tạo `.ai_rules.md` với template bắt buộc ghi log
- Tạo `DEV_LOG.md` với backfill phiên 1-3
- Tạo 9 UI scripts mới:
  1. `CursorEffects.cs` — Cursor ring + pulse + trail
  2. `InstructionDisplay.cs` — Step instruction text
  3. `ErrorOverlay.cs` — Screen flash red
  4. `ScoreDisplay.cs` — Pop-in animation + grade
  5. `SlidePanel.cs` — 4 direction slide panel
  6. `HoverButton.cs` — Radial loading ring
  7. `ObjectHighlight.cs` — Proximity glow
  8. `ProgressTracker.cs` — Step checklist
  9. `HUDManager.cs` — Central coordinator
- **Files changed**: 11 new files
- **Kết quả**: 24 C# scripts total

---

## Phiên 5 — 2026-06-09 16:00 | Code Review + Phase 4-5

**👤 User:** Kiểm tra công đoạn dở dang, rà soát lỗi, tiếp tục bước tiếp theo.

**🤖 AI:**
- Rà soát cross-references 24 scripts → tất cả API khớp ✅
- Tạo 6 file mới (Phase 4 + 5):
  - `VoiceInputManager.cs` — Mic recording
  - `AIChatPanel.cs` — Chat UI pipeline
  - `HighlightResponder.cs` — Parse AI → highlight
  - `AudioFeedbackManager.cs` — 10 audio clips singleton
  - `SceneBootstrapper.cs` — Startup validator
  - `SettingsManager.cs` — PlayerPrefs settings
- **Files changed**: 6 new files
- **Kết quả**: 31 C# scripts total

---

## Phiên 6 — 2026-06-15 08:00 | Code Review + Bug Fixes + Login/History UI

**👤 User:** Review toàn bộ dự án, tiếp tục triển khai các bước tiếp theo.

**🤖 AI:**
- Phát hiện 6 bugs, sửa toàn bộ:
  1. SceneBootstrapper namespace sai
  2. PinchGrabber thiếu expose held object
  3. SnapZone._allZones private
  4. AIChatPanel context rỗng
  5. SQLiteConnector không auto-init
  6. ConfigLoader timeout sai
- Tạo 3 file UI mới:
  - `LoginScreen.cs` — UI đăng nhập
  - `SessionHistoryPanel.cs` — Bảng lịch sử
  - `LessonSelectPanel.cs` — Menu chọn bài
- **Files changed**: 5 modified + 3 new
- **Kết quả**: 34 C# scripts total

---

## Phiên 7 — 2026-06-15 10:50 | Kiểm tra luồng công việc + Hạ tầng dự án

**👤 User:**
1. Kiểm tra xem dự án đã có luồng công việc chưa → tạo nhật ký công việc (task checklist)
2. Lưu lại toàn bộ lịch sử chat AI vào file
3. Kiểm tra logic tự động commit

**🤖 AI:**
- Kiểm tra toàn bộ project: có implementation_plan.md (ROADMAP) + DEV_LOG.md, nhưng CHƯA có task checklist
- Tạo `task.md` — nhật ký công việc dạng checklist (50+ tasks, đánh dấu trạng thái hiện tại)
- Tạo `CHAT_HISTORY.md` — file lưu lịch sử chat AI (backfill 6 phiên trước)
- Kiểm tra `auto_commit.py`: Script đã có đầy đủ logic (watchdog, debounce 5s, auto-push option), NHƯNG:
  - Auto-push **tắt** mặc định (cần `--push` flag)
  - Script cần **chạy thủ công** (`python auto_commit.py`)
  - Git log chỉ có 4 commits → script chưa được sử dụng thường xuyên
- Cập nhật `.ai_rules.md` thêm quy tắc ghi chat history + cập nhật task.md
- **Files changed**: task.md (NEW), CHAT_HISTORY.md (NEW), .ai_rules.md (MODIFIED)
- **Kết quả**: Hạ tầng project management hoàn thiện

---

## Phiên 8 — 2026-06-15 11:02 | Triển khai bước tiếp theo (Phase 3-5)

**👤 User:** Triển khai bước tiếp theo của dự án.

**🤖 AI:**
- Phân tích task.md → xác định 6 tasks code-only chưa hoàn thành
- Tạo 4 file C# mới:
  - `SpeechToText.cs` — Gemini multimodal audio STT (AudioClip → WAV → Base64 → API)
  - `TextToSpeech.cs` — Windows SAPI TTS với queue playback, Vietnamese voice
  - `ObjectPooler.cs` — Generic object pool system
  - `ZAxisCalibrator.cs` — 3-mode Z-axis depth calibration
- Tạo 1 shader mới:
  - `OutlineShader.shader` — URP outline via back-face vertex extrusion + pulse animation
- Sửa 3 file hiện tại:
  - `AIChatPanel.cs` — Kết nối full pipeline STT + TTS
  - `GestureDetector.cs` — Thêm frame-skip ONNX optimization
  - `SceneBootstrapper.cs` — Thêm validation cho 4 component mới
- **Files changed**: 4 new + 1 shader + 3 modified = 8 files
- **Kết quả**: 38 C# scripts + 1 shader, ~32/50 tasks hoàn thành

---

## Phiên 9 — 2026-06-19 13:25 | App Flow + Scene Setup + Performance Monitor

**👤 User:** Tiếp tục triển khai dự án, chú ý những gì các file markdown đã ghi.

**🤖 AI:**
- Đọc toàn bộ markdown files: README, CHAT_HISTORY, task.md, DEV_LOG, .ai_rules, implementation_plan
- Phân tích 38 scripts hiện tại, xác định gaps trong architecture
- Tạo 5 file C# mới:
  1. `AppFlowManager.cs` — App navigation (Login → LessonSelect → Training → Score) with fade transitions
  2. `PerformanceMonitor.cs` — FPS/memory overlay, color-coded, auto-create fallback UI
  3. `SceneSetupHelper.cs` — Editor script tự tạo scene hierarchy (menu: Motion AR → Setup Training Scene)
  4. `InputModeSwitch.cs` — Mock ↔ MediaPipe hot-swap at runtime
  5. `SettingsPanel.cs` — Settings UI (volume, input mode, features, hand size)
- Sửa 2 file hiện tại:
  1. `SettingsManager.cs` — Thêm GetString/SetString generic helpers
  2. `SceneBootstrapper.cs` — Thêm using MotionAR.UI, validation cho 3 component mới
- **Files changed**: 5 new + 2 modified = 7 files
- **Kết quả**: 43 C# scripts + 1 shader + 1 Editor script, ~38/55 tasks hoàn thành

---

> **GHI CHÚ**: File này sẽ được cập nhật tự động sau mỗi phiên trao đổi với AI.
> Nếu lịch sử quá dài, các phiên cũ sẽ được tóm tắt (archive) vào `CHAT_HISTORY_ARCHIVE.md`.

