# Walkthrough — Monitor AR Development Progress

## Tổng q4: **4 phiên làm việc**: 17 lỗi sửa + 5 file mới + 3 file sửa đổi lớn + Toàn diện Code Review
5: 
6: ---
7: 
8: ## Phiên 1 — Bug Fixes (Session 1)
9: 
10: | # | File | Thay đổi |
11: |---|---|---|
12: | 1 | SQLiteConnector.cs | Viết lại: `SQLiteAsyncConnection` → `SQLiteConnection` + `UniTask.RunOnThreadPool` + `lock` |
13: | 2 | Models_ONNX/ | Copy `gesture_model.onnx.data` (1.9MB weights) |
14: | 3 | GestureDetector.cs | Thêm numerically-stable Softmax cho model output |
15: | 4 | HandData.cs | Bridge MockProvider → `ConcurrentQueue` via `EnqueueHandData` |
16: | 5 | PinchGrabber.cs | Refactor → subscribe `GestureDetector.OnPinchUpdate` (single source of truth) |
17: 
18: ---
19: 
20: ## Phiên 2 — Sentis 2.1.3 Migration (Session 2)
21: 
22: | Thay đổi | Cũ | Mới |
23: |---|---|---|
24: | Tensor type | `TensorFloat` | `Tensor<float>` |
25: | Output readback | `CompleteOperationsAndDownload()` | `ReadbackAndClone()` |
26: | Dispose pattern | Không dispose (worker-owned) | `cpuOutputTensor?.Dispose()` (clone-owned) |
27: 
28: ---
29: 
30: ## Phiên 3 — Phase 1 & 2 Implementation (Session 3, 2026-05-30)
31: 
32: ### ✅ StandardScaler Integration
33: 
34: 1. **Chạy** [8_export_scaler.py](file:///d:/WORKSAPCE/Monitor_AR/1_AI_Model_Training/Scripts/8_export_scaler.py) → `scaler_params.json` (63 mean, 63 scale)
35: 2. **Copy** vào [StreamingAssets/scaler_params.json](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/StreamingAssets/scaler_params.json)
36: 3. **Sửa** [GestureDetector.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/GestureDetector.cs):
37:    - `LoadScalerParams()` — đọc JSON → `_scalerMean[63]` + `_scalerScale[63]`
38:    - `NormalizeFeatures(float[])` — áp dụng `(x - mean) / scale` trước inference
39:    - Gọi `NormalizeFeatures` trong `ProcessDynamicGesture` trước khi add vào sliding window
40: 
41: ### ✅ [NEW] [GrabbablePart.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/GrabbablePart.cs)
42: 
43: Concrete `IGrabbable` implementation:
44: - `PartID` — unique ID matching `StepData.requiredPartID`
45: - `OnGrab()` — đổi màu cyan
46: - `OnRelease()` — restore màu gốc
47: - `SetGrabbable(false)` — lock sau khi snap đúng
48: - `[RequireComponent(typeof(Collider))]`
49: 
50: ### ✅ [NEW] [TrainingFlowController.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/TrainingFlowController.cs)
51: 
52: Orchestrator kết nối PinchGrabber → SnapZone → SessionManager:
53: - Subscribe `PinchGrabber.OnObjectReleased`
54: - Tìm nearest SnapZone → `TrySnap()`
55: - Validate: đúng part + đúng zone + đúng step → `AdvanceStep()`
56: - Sai → `RecordMistake()` + `ResetZone()` + restore grabbability
57: - Events: `OnCorrectPlacement`, `OnIncorrectPlacement`, `OnLessonFinished`
58: 
59: ### ✅ [NEW] [MediaPipeUDPProvider.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/MediaPipeUDPProvider.cs)
60: 
61: Unity-side UDP receiver cho MediaPipe:
62: - Background thread + `UdpClient` → JSON parse → `HandData`
63: - Thread-safe `_mainThreadTime` (volatile float, updated in `Update()`)
64: - Coordinate conversion: MediaPipe [0,1] → app space [-0.2, 0.2]
65: - Pushes vào `HandTrackingReceiver.EnqueueHandData()` (Producer-Consumer pattern)
66: - Stats: `PacketsReceived`, `PacketsTracking`
67: 
68: ### ✅ [NEW] [mediapipe_server.py](file:///d:/WORKSAPCE/Monitor_AR/1_AI_Model_Training/Scripts/mediapipe_server.py)
69: 
70: Python MediaPipe → UDP bridge:
71: - CLI: `--port`, `--camera`, `--mirror`, `--show-preview`
72: - JSON format: `{"tracking": true, "landmarks": [{x,y,z}, ...]}`
73: - Preview window: landmark drawing, pinch detection status, FPS counter
74: - Graceful shutdown (ESC / Ctrl+C)
75: 
76: ### ✅ [NEW] [WebcamBackground.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/WebcamBackground.cs)
77: 
78: Webcam feed → `RawImage` background:
79: - Horizontal mirror cho "magic mirror" effect
80: - Configurable resolution, FPS, camera index
81: 
82: ---
83: 
84: ## Phiên 14 — Code Review & Bug Fixes (Session 14, 2026-06-20)
85: 
86: ### ✅ 5-Axis Code Review
87: Thực hiện đánh giá toàn diện mã nguồn gồm 49 C# scripts để phát hiện các lỗ hổng về độ chính xác, hiệu năng, kiến trúc, bảo mật và khả năng bảo trì.
88: 
89: ### ✅ Sửa 10 lỗi Critical (P0) & Important (P1)
90: 1. **Sửa rò rỉ CancellationTokenSource (P0)**: Thêm `using` vào `timeoutCts` trong `GeminiApiConnector.cs`.
91: 2. **Sửa Race Condition (P0)**: Capture local values trước khi gọi async DB save trong `SessionManager.cs`.
92: 3. **Sửa rò rỉ Material (P0)**: Dùng `MaterialPropertyBlock` thay thế cho việc tạo material mới mỗi khi chớp đỏ báo lỗi trong `SnapZone.cs`.
93: 4. **Sửa Thread-safety (P1)**: Chuyển đổi `UnityEngine.Random` sang `System.Random` instance trong lớp tĩnh `ProceduralAudio.cs`.
94: 5. **Tối ưu hóa GC Allocations (P1)**: Pre-allocate feature buffer `float[63]` trong `GestureDetector.cs`.
95: 6. **Cải thiện thuật toán cửa sổ trượt (P1)**: Thay `List` bằng `Queue` trong `GestureDetector.cs` cho độ phức tạp O(1) thay vì O(n).
96: 7. **Ngăn chặn xung đột Transition (P1)**: Thêm `_isTransitioning` guard và hủy CTS trong `AppFlowManager.cs`.
97: 8. **Dọn dẹp code rác**: Xóa biến `_isVisible` không sử dụng trong `ConfirmDialog.cs` để giải quyết cảnh báo CS0414.
98: 9. **Bổ sung giải phóng tài nguyên**: Hủy bỏ CTS trong `OnDestroy` của `AIChatPanel.cs` phòng rò rỉ bộ nhớ.
99: 10. **Sửa anti-pattern Async**: Chuyển `async void` thành `async UniTaskVoid` ở `ObjectPooler.ReturnDelayed`.
100: 
101: ---
102: 
103: ## Cấu trúc Scripts hiện tại (49 files)
104: 
105: ```
106: Assets/Scripts/
107: ├── AI/ (7 files)
108: ├── Core/ (8 files)
109: ├── Data/ (2 files)
110: ├── Database/ (1 file)
111: ├── Editor/ (1 file)
112: ├── Interaction/ (10 files)
113: └── UI/ (20 files)
114: ```
115: 
116: ---
117: 
118: ## Bước tiếp theo (User Actions)

1. **Mở Unity** → Verify build sạch (0 errors, 1 warning).
2. **Tạo TestScene** với hierarchy từ ROADMAP (Camera, Lights, System GOs, Parts, SnapZones).
3. **Tạo LessonData** ScriptableObject với 2-3 steps demo.
4. **Test E2E với MockProvider**: Click → Grab → Drag → Snap → Advance.
5. **Test MediaPipe**: `python Scripts/mediapipe_server.py` + thêm `MediaPipeUDPProvider` component.
