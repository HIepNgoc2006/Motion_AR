# Walkthrough — Monitor AR Development Progress

## Tổng quan
**3 phiên làm việc**: 7 lỗi sửa + 5 file mới + 3 file sửa đổi lớn

---

## Phiên 1 — Bug Fixes (Session 1)

| # | File | Thay đổi |
|---|---|---|
| 1 | SQLiteConnector.cs | Viết lại: `SQLiteAsyncConnection` → `SQLiteConnection` + `UniTask.RunOnThreadPool` + `lock` |
| 2 | Models_ONNX/ | Copy `gesture_model.onnx.data` (1.9MB weights) |
| 3 | GestureDetector.cs | Thêm numerically-stable Softmax cho model output |
| 4 | HandData.cs | Bridge MockProvider → `ConcurrentQueue` via `EnqueueHandData` |
| 5 | PinchGrabber.cs | Refactor → subscribe `GestureDetector.OnPinchUpdate` (single source of truth) |

---

## Phiên 2 — Sentis 2.1.3 Migration (Session 2)

| Thay đổi | Cũ | Mới |
|---|---|---|
| Tensor type | `TensorFloat` | `Tensor<float>` |
| Output readback | `CompleteOperationsAndDownload()` | `ReadbackAndClone()` |
| Dispose pattern | Không dispose (worker-owned) | `cpuOutputTensor?.Dispose()` (clone-owned) |

---

## Phiên 3 — Phase 1 & 2 Implementation (Session 3, 2026-05-30)

### ✅ StandardScaler Integration

1. **Chạy** [8_export_scaler.py](file:///d:/WORKSAPCE/Monitor_AR/1_AI_Model_Training/Scripts/8_export_scaler.py) → `scaler_params.json` (63 mean, 63 scale)
2. **Copy** vào [StreamingAssets/scaler_params.json](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/StreamingAssets/scaler_params.json)
3. **Sửa** [GestureDetector.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/GestureDetector.cs):
   - `LoadScalerParams()` — đọc JSON → `_scalerMean[63]` + `_scalerScale[63]`
   - `NormalizeFeatures(float[])` — áp dụng `(x - mean) / scale` trước inference
   - Gọi `NormalizeFeatures` trong `ProcessDynamicGesture` trước khi add vào sliding window

### ✅ [NEW] [GrabbablePart.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/GrabbablePart.cs)

Concrete `IGrabbable` implementation:
- `PartID` — unique ID matching `StepData.requiredPartID`
- `OnGrab()` — đổi màu cyan
- `OnRelease()` — restore màu gốc
- `SetGrabbable(false)` — lock sau khi snap đúng
- `[RequireComponent(typeof(Collider))]`

### ✅ [NEW] [TrainingFlowController.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/TrainingFlowController.cs)

Orchestrator kết nối PinchGrabber → SnapZone → SessionManager:
- Subscribe `PinchGrabber.OnObjectReleased`
- Tìm nearest SnapZone → `TrySnap()`
- Validate: đúng part + đúng zone + đúng step → `AdvanceStep()`
- Sai → `RecordMistake()` + `ResetZone()` + restore grabbability
- Events: `OnCorrectPlacement`, `OnIncorrectPlacement`, `OnLessonFinished`

### ✅ [NEW] [MediaPipeUDPProvider.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/MediaPipeUDPProvider.cs)

Unity-side UDP receiver cho MediaPipe:
- Background thread + `UdpClient` → JSON parse → `HandData`
- Thread-safe `_mainThreadTime` (volatile float, updated in `Update()`)
- Coordinate conversion: MediaPipe [0,1] → app space [-0.2, 0.2]
- Pushes vào `HandTrackingReceiver.EnqueueHandData()` (Producer-Consumer pattern)
- Stats: `PacketsReceived`, `PacketsTracking`

### ✅ [NEW] [mediapipe_server.py](file:///d:/WORKSAPCE/Monitor_AR/1_AI_Model_Training/Scripts/mediapipe_server.py)

Python MediaPipe → UDP bridge:
- CLI: `--port`, `--camera`, `--mirror`, `--show-preview`
- JSON format: `{"tracking": true, "landmarks": [{x,y,z}, ...]}`
- Preview window: landmark drawing, pinch detection status, FPS counter
- Graceful shutdown (ESC / Ctrl+C)

### ✅ [NEW] [WebcamBackground.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/WebcamBackground.cs)

Webcam feed → `RawImage` background:
- Horizontal mirror cho "magic mirror" effect
- Configurable resolution, FPS, camera index

---

## Cấu trúc Scripts hiện tại (15 files)

```
Assets/Scripts/
├── AI/
│   ├── ConfigLoader.cs
│   ├── ContextBuilder.cs
│   └── GeminiApiConnector.cs
├── Core/
│   └── SessionManager.cs
├── Data/
│   ├── LessonData.cs
│   └── StepData.cs
├── Database/
│   └── SQLiteConnector.cs
└── Interaction/
    ├── GestureDetector.cs       ← Modified (Sentis 2.1.3 + StandardScaler)
    ├── GrabbablePart.cs         ← NEW
    ├── HandData.cs
    ├── HandTrackingReceiver.cs
    ├── MediaPipeUDPProvider.cs  ← NEW
    ├── PinchGrabber.cs
    ├── SnapZone.cs
    ├── TrainingFlowController.cs ← NEW
    └── WebcamBackground.cs      ← NEW
```

---

## Bước tiếp theo (User Actions)

1. **Mở Unity** → verify 0 compile errors
2. **Tạo TestScene** với hierarchy từ ROADMAP (Camera, Lights, System GOs, Parts, SnapZones)
3. **Tạo LessonData** ScriptableObject với 2-3 steps demo
4. **Test E2E với MockProvider**: Click → Grab → Drag → Snap → Advance
5. **Test MediaPipe**: `python Scripts/mediapipe_server.py` + thêm `MediaPipeUDPProvider` component
