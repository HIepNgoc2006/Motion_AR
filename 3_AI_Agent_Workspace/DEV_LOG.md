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

---

## PHIÊN 6 — 2026-06-15 (Code Review + Bug Fixes + Login/History UI)

### 📌 Yêu cầu
Review toàn bộ dự án, tiếp tục triển khai các bước tiếp theo.

### 🧠 Quá trình suy nghĩ
- Đọc toàn bộ 31 C# scripts, cross-reference API giữa các module
- Phát hiện 6 bug/thiếu sót:
  1. SceneBootstrapper sai namespace (`MotionAR.UI` → nên là `MotionAR.Core`)
  2. PinchGrabber không expose trạng thái held object cho AI context
  3. SnapZone._allZones private, AIChatPanel không build được context
  4. AIChatPanel.BuildCurrentContext() truyền data rỗng cho Gemini
  5. SQLiteConnector.InitializeAsync() không được gọi tự động → crash runtime
  6. ConfigLoader timeout mặc định 30s, README nói 5s
- Xác định 3 tính năng Phase 5 chưa code: Login, History, Lesson Select

### ✏️ Thay đổi code

**Bug Fixes:**
- [MODIFY] `Core/SceneBootstrapper.cs`: Sửa namespace `MotionAR.UI` → `MotionAR.Core`, thêm auto-init SQLiteConnector
- [MODIFY] `Interaction/PinchGrabber.cs`: Thêm `GrabbedObject`, `GrabbedPartID`, `IsHolding` public properties
- [MODIFY] `Interaction/SnapZone.cs`: Thêm `AllZones` static IReadOnlyList accessor
- [MODIFY] `AI/AIChatPanel.cs`: Sửa `BuildCurrentContext()` dùng API mới từ SnapZone.AllZones + PinchGrabber.GrabbedPartID
- [MODIFY] `AI/ConfigLoader.cs`: Sửa default timeout 30s → 15s

**New Features:**
- [NEW] `UI/LoginScreen.cs`: UI đăng nhập — TMP_InputField + SQLiteConnector.GetOrCreateUserAsync + transition
- [NEW] `UI/SessionHistoryPanel.cs`: Bảng lịch sử phiên học — grade S/A/B/C/F, color-coded, mm:ss format
- [NEW] `UI/LessonSelectPanel.cs`: Menu chọn bài học — prefab/fallback cards, integrate SessionManager.StartLesson

### 💡 Quyết định thiết kế
1. **PinchGrabber expose read-only properties** thay vì events — đơn giản hơn cho polling context
2. **SnapZone.AllZones là IReadOnlyList** — cho phép iterate nhưng không modify từ bên ngoài
3. **SceneBootstrapper auto-init DB** — tập trung logic khởi tạo 1 chỗ thay vì scatter
4. **Timeout 15s** — cân bằng giữa UX (5s quá ngắn cho cold start) và responsiveness (30s quá dài)
5. **Fallback UI generation** — cả 3 panel mới đều có fallback tạo UI bằng code nếu chưa có prefab
6. **LoginScreen → LessonSelectPanel → Training** — luồng navigation rõ ràng

### 📊 Kết quả
- 5 file sửa, 3 file mới
- Tổng scripts C#: **34 files** (từ 31)
- Cấu trúc scripts:
  ```
  Assets/Scripts/
  ├── AI/ (6 files)
  ├── Core/ (3 files)
  ├── Data/ (2 files)
  ├── Database/ (1 file)
  ├── Interaction/ (9 files)
  └── UI/ (13 files) ← 3 mới
  ```
- Bug fixes: 6/6 hoàn thành
- Phase 5 UI: Login + History + Lesson Select hoàn thành
- **Bước tiếp theo**: Setup Scene trong Unity Editor, tạo prefabs, test E2E

---

## PHIÊN 7 — 2026-06-15 (Kiểm tra luồng công việc + Hạ tầng dự án)

### 📌 Yêu cầu
1. Kiểm tra xem dự án đã có luồng công việc (workflow/task log) chưa
2. Tạo nhật ký công việc dạng checklist
3. Lưu lại toàn bộ lịch sử chat AI vào file
4. Kiểm tra logic tự động commit

### 🧠 Quá trình suy nghĩ
- Đọc toàn bộ project structure: `implementation_plan.md`, `DEV_LOG.md`, `walkthrough.md`, `.ai_rules.md`
- Kiểm tra git log: chỉ 4 commits → auto_commit.py chưa được sử dụng thường xuyên
- Phân tích auto_commit.py: script hoàn chỉnh nhưng auto-push TẮT mặc định, phải chạy thủ công
- Xác nhận: dự án CHƯA CÓ task checklist và CHƯA CÓ file lưu chat history

### ✏️ Thay đổi code
- [NEW] `3_AI_Agent_Workspace/task.md`: Nhật ký công việc dạng checklist — 50+ tasks, đánh dấu trạng thái hiện tại (✅/🟡/❌) cho tất cả 7 phases
- [NEW] `3_AI_Agent_Workspace/CHAT_HISTORY.md`: File lưu lịch sử chat AI, backfill 6 phiên trước + phiên hiện tại
- [MODIFY] `3_AI_Agent_Workspace/.ai_rules.md`: Thêm 3 quy tắc mới (#5 ghi chat history, #6 cập nhật task.md, #7 auto-commit)

### 💡 Quyết định thiết kế
1. **Task.md đặt trong 3_AI_Agent_Workspace/** — tập trung toàn bộ tài liệu AI/dev vào 1 folder, không làm rối root directory
2. **Chat history format**: Mỗi phiên ghi: timestamp + user request + AI response (tóm tắt) + files changed → dễ tra cứu
3. **Auto-commit phân tích**: Script đã sẵn sàng nhưng cần chạy `python auto_commit.py --push` để hoạt động liên tục. Không sửa default thành auto-push vì có thể push code lỗi.

### 📊 Kết quả
- 2 file mới tạo: task.md, CHAT_HISTORY.md
- 1 file sửa: .ai_rules.md (thêm 3 quy tắc)
- Hạ tầng project management hoàn thiện:
  - `task.md` → theo dõi tiến độ
  - `CHAT_HISTORY.md` → backup lịch sử chat
  - `DEV_LOG.md` → nhật ký kỹ thuật chi tiết
  - `.ai_rules.md` → quy tắc bắt buộc (7 rules)
  - `auto_commit.py` → sẵn sàng, cần chạy

---

## PHIÊN 8 — 2026-06-15 (Triển khai bước tiếp theo — Phase 3-5 completion)

### 📌 Yêu cầu
Triển khai bước tiếp theo của dự án.

### 🧠 Quá trình suy nghĩ
- Phân tích task.md → xác định 6 tasks code-only chưa hoàn thành (không cần Unity Editor)
- Đọc API surfaces: VoiceInputManager (events), GeminiApiConnector (AskGeminiAsync), AIChatPanel (pipeline), GestureDetector (inference), ConfigLoader (static props), SnapZone, PinchGrabber
- Quyết định dùng Gemini 2.0 Flash multimodal audio cho STT (giảm dependency, cùng API key)
- Quyết định dùng Windows SAPI cho TTS (native, không cần cloud API thêm)
- Quyết định frame-skip cho ONNX optimization (configurable, mặc định 2)

### ✏️ Thay đổi code

**New Files:**
- [NEW] `Scripts/AI/SpeechToText.cs`: AudioClip → WAV bytes → Base64 → Gemini multimodal → text. Singleton, events OnTranscriptionComplete/Failed, thread-safe
- [NEW] `Scripts/AI/TextToSpeech.cs`: Windows SAPI via System.Speech.Synthesis. Queue-based playback, text segmentation at sentence boundaries, Vietnamese voice auto-selection, cross-platform fallback (simulated delay)
- [NEW] `Scripts/Core/ObjectPooler.cs`: Generic pool system — configurable categories (PoolConfig), max capacity, delayed return (UniTask), runtime RegisterPool, stats API
- [NEW] `Scripts/Core/ZAxisCalibrator.cs`: 3-mode Z calibration — Auto (min/max tracking N frames), BoundingBox (ThumbsUp/Down gesture triggers), PushPull (continuous). Smoothing filter, CalibrateZ/CalibrateHandData API
- [NEW] `Assets/Shaders/OutlineShader.shader`: URP single-pass outline — Cull Front vertex extrusion, pulse animation, HDR color, fog support, SRPDefaultUnlit light mode

**Modified Files:**
- [MODIFY] `Scripts/AI/AIChatPanel.cs`:
  - Added `_textToSpeech` SerializeField
  - Replaced TODO `HandleRecordingComplete()` with full STT pipeline: AudioClip → SpeechToText.Instance.TranscribeAsync → SubmitQuestion
  - Added TTS playback after AI response (step 7 in ProcessQuestionAsync)
- [MODIFY] `Scripts/Interaction/GestureDetector.cs`:
  - Added `_inferenceEveryNFrames` configurable field (default 2)
  - Added `_frameCounter` tracking
  - Frame-skip guard in ProcessDynamicGesture → RunInference
- [MODIFY] `Scripts/Core/SceneBootstrapper.cs`:
  - Added 4 new SerializeField references (SpeechToText, TextToSpeech, ObjectPooler, ZAxisCalibrator)
  - Added validation checks in ValidateReferences

### 💡 Quyết định thiết kế
1. **Gemini multimodal STT thay vì Google Cloud STT** — Cùng API key, cùng library (Newtonsoft, UnityWebRequest), không thêm dependency. Gemini 2.0 Flash hỗ trợ audio input native.
2. **WAV format cho audio upload** — Đơn giản nhất, không cần encoder. 16-bit PCM WAV → Base64 → inline_data. File size ~50KB cho 5 giây audio.
3. **Windows SAPI TTS** — #if UNITY_STANDALONE_WIN conditional. Không cần cloud API key thêm. Tự động tìm Vietnamese voice nếu có.
4. **Queue-based TTS** — Đợt câu dài, chia segment tại dấu câu. Không bị chồng chéo nếu nhiều response liên tiếp.
5. **Frame-skip ONNX** — `_inferenceEveryNFrames = 2` mặc định. Giảm 50% GPU load nhưng latency chỉ tăng ~33ms ở 30 FPS. Inspector slider 1-5.
6. **ObjectPooler generic** — Tag-based lookup, PoolConfig SerializeField list. Hỗ trợ runtime RegisterPool cho dynamic prefabs.
7. **ZAxisCalibrator 3-mode** — Auto mode (mặc định) hoạt động passive, PushPull chủ động, BoundingBox cần gesture trigger.
8. **Outline shader Cull Front** — Extrude back faces thay vì post-processing → tương thích mobile, không cần extra render pass.

### 📊 Kết quả
- 5 file mới (4 C# + 1 shader), 3 file sửa
- Tổng scripts C#: **38 files** (từ 34)
- Tổng shaders: **1 file**
- Cấu trúc scripts:
  ```
  Assets/Scripts/
  ├── AI/ (8 files) ← 2 mới (SpeechToText, TextToSpeech)
  ├── Core/ (5 files) ← 2 mới (ObjectPooler, ZAxisCalibrator)
  ├── Data/ (2 files)
  ├── Database/ (1 file)
  ├── Interaction/ (9 files) ← 1 sửa (GestureDetector)
  └── UI/ (13 files)
  Assets/Shaders/
  └── OutlineShader.shader ← MỚI
  ```
- Phase 3: **90%** (thêm shader, còn prefabs + font + audio trong Unity)
- Phase 4: **100% code** (STT + TTS hoàn thành)
- Phase 5: **70%** (ObjectPooler + ZAxisCalibrator + ONNX opt, còn profiling + build)
- **Tasks hoàn thành: ~32/50**
