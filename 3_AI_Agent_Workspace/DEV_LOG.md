# 📝 DEVELOPMENT LOG — MONITOR AR PROJECT
## Ghi chép tự động bởi AI sau mỗi phiên làm việc
## File quy tắc: `.ai_rules.md` — AI PHẢI ghi vào đây sau mỗi phiên

---

## PHIÊN 1 — 2026-05-30 (Kiểm toán ban đầu & Bug Fix)

### 📌 Yêu cầu
Rà soát toàn bộ dự án, sửa lỗi Console đỏ trong Unity, cập nhật planning documents.

### 🧠 Quá trình suy nghĩ
- Đọc toàn bộ 11 file C# trong `Assets/Scripts/` để hiểu kiến trúc
- Đọc `manifest.json` xác định: Sentis 2.1.3, URP 14.0.12, Newtonsoft 3.2.2
- Đọc `packages.config` xác nhận NuGet: sqlite-net-pcl 1.9.172
- Cross-reference code với API documentation

### ✏️ Thay đổi code
- [MODIFY] `SQLiteConnector.cs`: Viết lại hoàn toàn — `SQLiteAsyncConnection` → `SQLiteConnection` + `UniTask.RunOnThreadPool` + `lock`
- [MODIFY] `GestureDetector.cs`: Thêm numerically-stable Softmax cho raw logits output
- [MODIFY] `HandData.cs`: Thêm `HandTrackingReceiver.EnqueueHandData(data)` vào MockProvider
- [MODIFY] `PinchGrabber.cs`: Refactor subscribe `GestureDetector.OnPinchUpdate` (single source of truth)
- [COPY] `gesture_model.onnx.data` → `Assets/Models_ONNX/`

### 💡 Quyết định thiết kế
- Chọn `SQLiteConnection` sync + `UniTask.RunOnThreadPool` thay vì tìm async DLL khác → đơn giản, không thêm dependency
- Softmax trừ max logit trước exp → tránh overflow trên float32
- PinchGrabber subscribe event thay vì tự tính → single source of truth

### 📊 Kết quả
- 5 lỗi biên dịch/logic đã sửa
- Backend code 95% hoàn chỉnh

---

## PHIÊN 2 — 2026-05-30 (Sentis 2.1.3 API Migration)

### 📌 Yêu cầu
Tiếp tục rà soát API compatibility, sửa lỗi Console đỏ còn lại.

### 🧠 Quá trình suy nghĩ
- Search web: "Unity Sentis 2.1.3 API changes TensorFloat"
- Xác nhận: `TensorFloat` → `Tensor<float>`, `CompleteOperationsAndDownload` → `ReadbackAndClone`
- Đây là NGUYÊN NHÂN CHÍNH gây lỗi CS0246 và CS1061 trong Console

### ✏️ Thay đổi code
- [MODIFY] `GestureDetector.cs` — 7 chỗ sửa trong `RunInference()`:
  - L257-258: `TensorFloat` → `Tensor<float>` (khai báo biến)
  - L262-265: `new TensorFloat(...)` → `new Tensor<float>(...)`
  - L269-278: `CompleteOperationsAndDownload()` → `ReadbackAndClone()`
  - L280, L288-289, L297: `outputTensor` → `cpuOutputTensor`
  - L329-333: Thêm `cpuOutputTensor?.Dispose()` (clone phải dispose)
- [MODIFY] `Motion AR.md` — Cập nhật Sentis API notes vào Chương VIII, IX, X + thêm Phụ lục trạng thái

### 💡 Quyết định thiết kế
- `ReadbackAndClone()` tạo clone → caller phải dispose → thêm trong `finally` block
- `PeekOutput()` reference vẫn worker-owned → KHÔNG dispose (khác với clone)

### 📊 Kết quả
- Lỗi Console đỏ chính đã xử lý
- Tạo Roadmap V1.0 chi tiết 7 phases
- Cập nhật `implementation_plan.md`, `walkthrough.md`, `Motion AR.md`

---

## PHIÊN 3 — 2026-05-30 (Phase 1 & 2 Implementation)

### 📌 Yêu cầu
Triển khai Phase 1 (Core Engine) và Phase 2 (Hand Tracking Integration).

### 🧠 Quá trình suy nghĩ
- Chạy `8_export_scaler.py` → verify output: 63 mean, 63 scale values
- Đọc lại `PinchGrabber.cs`, `SnapZone.cs`, `SessionManager.cs` để hiểu event API
- Quyết định tạo `TrainingFlowController` làm orchestrator thay vì sửa PinchGrabber
- Chọn Python UDP bridge cho MediaPipe thay vì native plugin (ổn định hơn, dễ debug)

### ✏️ Thay đổi code
- [MODIFY] `GestureDetector.cs`: Thêm `LoadScalerParams()`, `NormalizeFeatures()`, import `Newtonsoft.Json.Linq`
- [NEW] `GrabbablePart.cs`: IGrabbable implementation — PartID, OnGrab/OnRelease color feedback, SetGrabbable lock
- [NEW] `TrainingFlowController.cs`: Release → FindNearestSnapZone → TrySnap → Validate step → Advance/Mistake
- [NEW] `MediaPipeUDPProvider.cs`: Background thread UDP receiver, volatile `_mainThreadTime`, coordinate conversion
- [NEW] `WebcamBackground.cs`: WebCamTexture → RawImage, horizontal mirror
- [NEW] `mediapipe_server.py`: Python MediaPipe Hands → UDP JSON bridge, CLI args, preview window
- [COPY] `scaler_params.json` → `StreamingAssets/`

### 💡 Quyết định thiết kế
- `Time.time` thread safety: `volatile float _mainThreadTime` cập nhật mỗi frame → dùng cho background thread
- MediaPipe coordinates: `(x - 0.5) * 0.4` → khớp với MockProvider space
- Snap validation 2 lớp: SnapZone validate part ID, TrainingFlowController validate step order
- Wrong step snap → reset zone + restore part + restore Rigidbody → cho user thử lại

### 📊 Kết quả
- 5 file mới, 1 file sửa, 1 data file mới
- Tổng scripts: 15 files C# (từ 11)
- Phase 1: 95% (còn setup Scene trong Unity Editor)
- Phase 2: 80% (còn test live webcam)

---

## PHIÊN 4 — 2026-06-09 (DEV_LOG + AI Rules + Phase 3 UI/UX)

### 📌 Yêu cầu
1. Tạo file DEV_LOG ghi lại toàn bộ quá trình làm việc sau mỗi phiên
2. Tạo file .ai_rules.md bắt buộc AI ghi log
3. Tiếp tục triển khai các bước tiếp theo (Phase 3)

### 🧠 Quá trình suy nghĩ
- Kiểm tra trạng thái workspace: DEV_LOG.md phiên trước đã bị xóa do restructure → tạo lại
- Tạo `.ai_rules.md` với template bắt buộc ghi log + quy tắc dự án
- Đọc `LessonData.cs` verify API: `TotalSteps`, `GetStep()`, `lessonTitle` → khớp cho UI
- Đọc `SessionManager.cs` verify events: `OnStepChanged`, `OnLessonStarted`, `OnLessonCompleted`, `OnMistakeRecorded`
- Quyết định tạo 8 UI components theo Master Plan Chương VII (Giao diện Holographic HUD):
  1. CursorEffects — pulse animation + color transition
  2. InstructionDisplay — auto-update text khi step thay đổi
  3. ErrorOverlay — full-screen red flash khi sai
  4. ScoreDisplay — pop-in animation + score counting + grade letter
  5. SlidePanel — 4 hướng slide, auto-hide, cubic easing
  6. HoverButton — hover-to-click thay thế click truyền thống
  7. ObjectHighlight — proximity-based emission glow
  8. ProgressTracker — step checklist + progress bar
  9. HUDManager — quản lý tổng thể, map gesture → UI action

### ✏️ Thay đổi code
- [NEW] `.ai_rules.md`: File quy tắc bắt buộc AI ghi log sau mỗi phiên
- [NEW] `DEV_LOG.md`: Tạo lại với backfill phiên 1-3
- [NEW] `Scripts/UI/CursorEffects.cs`: Cursor ring + pulse + trail, subscribe OnPinchUpdate
- [NEW] `Scripts/UI/InstructionDisplay.cs`: TMP instruction text, fade transition via UniTask, step counter
- [NEW] `Scripts/UI/ErrorOverlay.cs`: Screen flash Image, subscribe OnMistakeRecorded, 80ms fade-in + 420ms fade-out
- [NEW] `Scripts/UI/ScoreDisplay.cs`: Pop-in (ease-out-back), score counting animation, grade S/A/B/C/F, Vietnamese messages
- [NEW] `Scripts/UI/SlidePanel.cs`: 4 direction slide, cubic easing, auto-hide timer (default 5s), CanvasGroup fade
- [NEW] `Scripts/UI/HoverButton.cs`: Radial loading ring fill, hover 0.3s → activate + beep, cooldown 0.5s
- [NEW] `Scripts/UI/ObjectHighlight.cs`: Emission glow via MaterialPropertyBlock, proximity-based intensity, URP `_EmissionColor`
- [NEW] `Scripts/UI/ProgressTracker.cs`: StepItemUI class, step states (pending/current/completed), progress bar fill
- [NEW] `Scripts/UI/HUDManager.cs`: Central coordinator, gesture → panel toggle (SwipeLeft/Right, OpenPalm, Fist)

### 💡 Quyết định thiết kế
1. **MaterialPropertyBlock thay vì Material instance** — ObjectHighlight dùng `SetPropertyBlock()` thay vì gán material trực tiếp → không tạo material leak, không ảnh hưởng batching
2. **CanvasGroup cho fade** — Tất cả panel đều dùng CanvasGroup alpha thay vì từng element → hiệu quả hơn, 1 draw call
3. **Ease-out-back cho ScoreDisplay** — Công thức `1 + 2.7*(t-1)^3 + 1.7*(t-1)^2` tạo hiệu ứng bounce giống Material Design
4. **HoverButton dùng `UpdateHover()` thay vì OnPointerEnter** — Vì hand tracking cursor không phải pointer event system → phải gọi manual
5. **ProgressTracker tự tạo step items nếu không có prefab** — Fallback pattern: nếu user chưa tạo prefab → tự tạo TextMeshPro đơn giản
6. **Gesture → UI mapping**: SwipeLeft = progress panel, SwipeRight = info panel, OpenPalm = show all, Fist = hide all

### 📊 Kết quả
- 11 file mới tạo (9 UI scripts + .ai_rules + DEV_LOG)
- Tổng scripts C#: **24 files** (từ 15)
- Cấu trúc scripts:
  ```
  Assets/Scripts/
  ├── AI/ (3 files)
  ├── Core/ (1 file)
  ├── Data/ (2 files)
  ├── Database/ (1 file)
  ├── Interaction/ (8 files)
  └── UI/ (9 files) ← MỚI
  ```
- Phase 3 (UI/UX): **80% code hoàn thành** — còn cần:
  - Tạo prefabs trong Unity Editor
  - Setup Canvas hierarchy
  - Import font Inter/Roboto vào TMP
  - Tạo URP Post-processing Volume (Bloom)
  - Audio assets (beep, click sounds)

---

## PHIÊN 5 — 2026-06-09 (Code Review + Phase 4-5 Implementation)

### 📌 Yêu cầu
Kiểm tra các công đoạn dở dang, rà soát lỗi, tiếp tục bước tiếp theo.

### 🧠 Quá trình suy nghĩ
- Kiểm tra git status: tất cả untracked (chưa commit)
- Rà soát cross-references 24 scripts: tất cả API đều khớp ✅
- GestureType enum đủ giá trị, PinchState.Grabbed tồn tại, SnapZone API đầy đủ
- Tiếp tục tạo Phase 4 (AI Tutor: 3 files) + Phase 5 (Infrastructure: 3 files)

### ✏️ Thay đổi code
- [NEW] `AI/VoiceInputManager.cs`: Mic recording, gesture-triggered, auto-stop
- [NEW] `AI/AIChatPanel.cs`: Pipeline text/voice → Gemini → chat bubbles → highlight
- [NEW] `AI/HighlightResponder.cs`: Parse AI response → emission pulse highlight
- [NEW] `UI/AudioFeedbackManager.cs`: 10 audio clips, singleton, volume controls
- [NEW] `Core/SceneBootstrapper.cs`: Validate references + assets lúc startup
- [NEW] `Core/SettingsManager.cs`: PlayerPrefs settings, InputMode enum

### 📊 Kết quả
- **31 C# scripts** tổng | Phase 1-5 code hoàn thành
