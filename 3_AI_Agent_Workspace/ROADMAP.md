# 🗺️ ROADMAP HOÀN CHỈNH — MONITOR AR TECHNICAL TRAINING SIMULATOR
## Phiên bản V1.0 — Ngày lập: 2026-05-30 | Vai trò: Senior Architect & Technical PM

---

## A. TÓM TẮT HIỆN TRẠNG DỰ ÁN

### ✅ Đã hoàn thành (Backend = 95%)

| Hạng mục | File | Mô tả |
|---|---|---|
| **Cấu trúc Scripts** | `Assets/Scripts/{AI,Core,Data,Database,Interaction}/` | 5 folders, 11 files C# — khớp 100% Master Plan |
| **Producer-Consumer Queue** | [HandTrackingReceiver.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/HandTrackingReceiver.cs) | `ConcurrentQueue<HandData>`, timeout 0.3s, lerp smoothing |
| **Hybrid Gesture Detection** | [GestureDetector.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/GestureDetector.cs) | Pinch (Euclidean 0ms) + ONNX (Sentis 2.1.3 `Tensor<float>`) |
| **Pinch Grab/Release** | [PinchGrabber.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/PinchGrabber.cs) | Hysteresis, single source of truth từ GestureDetector |
| **Snap System** | [SnapZone.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/SnapZone.cs) | Snap + validation + error flash (UniTask) + audio |
| **Data-Driven Lessons** | [LessonData.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Data/LessonData.cs) / [StepData.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Data/StepData.cs) | ScriptableObject, OnValidate sort |
| **Session Management** | [SessionManager.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Core/SessionManager.cs) | Singleton, events, auto-save to DB |
| **SQLite Database** | [SQLiteConnector.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Database/SQLiteConnector.cs) | `SQLiteConnection` + `UniTask.RunOnThreadPool` + `lock` |
| **Gemini AI Connector** | [GeminiApiConnector.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/AI/GeminiApiConnector.cs) | UniTask, timeout, error handling, Vietnamese messages |
| **Context Builder** | [ContextBuilder.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/AI/ContextBuilder.cs) | JSON payload cho Gemini |
| **Config Loader** | [ConfigLoader.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/AI/ConfigLoader.cs) | Env var > config.json > defaults |
| **Mock Provider** | [HandData.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/HandData.cs) (MockHandTrackingProvider) | Mouse simulation, bridge to ConcurrentQueue |
| **ONNX Model** | `Assets/Models_ONNX/gesture_model.onnx` + `.onnx.data` | Transformer, 14 classes, input [1,15,63] |
| **AI Training Pipeline** | `1_AI_Model_Training/` | raw_data.csv (53MB), Transformer model, 10 scripts, 6 notebooks |

### ✅ 7 lỗi đã sửa

| # | Lỗi | Severity | File |
|---|---|---|---|
| 1 | Sentis API `TensorFloat` → `Tensor<float>` | 🔴 Critical | GestureDetector.cs |
| 2 | `SQLiteAsyncConnection` không tồn tại | 🔴 Critical | SQLiteConnector.cs |
| 3 | Thiếu `gesture_model.onnx.data` weights | 🔴 Critical | Models_ONNX/ |
| 4 | Thiếu Softmax trên logits output | 🟡 High | GestureDetector.cs |
| 5 | MockProvider không enqueue ConcurrentQueue | 🟡 High | HandData.cs |
| 6 | PinchGrabber duplicate state machine | 🟡 Medium | PinchGrabber.cs |
| 7 | project_report.md outdated | 🟢 Low | project_report.md |

### ❌ Tồn tại chính (Integration & UI = 20%)

| # | Vấn đề | Impact |
|---|---|---|
| B1 | **StandardScaler chưa sync** — Training normalize, Unity inference không | Accuracy giảm 30-50% |
| B2 | **Chưa có MediaPipe** — Chỉ có MockProvider (chuột) | Không test được tay thật |
| B3 | **Chưa setup Scene** — Không test end-to-end | Không verify flow |
| B4 | **Chưa có UI HUD** — Master Plan mô tả Holographic nhưng 0 code | Không có giao diện |
| B5 | **Chưa có voice input** — Gemini connector sẵn sàng nhưng thiếu mic | Không hỏi AI bằng giọng |
| B6 | **Chưa có Z-Axis calibration** — Không hiệu chuẩn chiều sâu | Tương tác 3D thiếu chính xác |

---

## B. ROADMAP CHI TIẾT THEO PHASE

---

### ═══════════════════════════════════════════════════
### PHASE 0: CHUẨN BỊ MÔI TRƯỜNG & VERIFY (1-2 ngày)
### ═══════════════════════════════════════════════════

**Mục tiêu:** Đảm bảo Unity project compile 0 lỗi, tất cả dependencies resolve đúng.

#### Checklist

- [ ] **0.1** Mở Unity 2022.3.x LTS → Open project `2_Unity_Project/Motion_AR/`
- [ ] **0.2** Kiểm tra Console: **0 lỗi đỏ** (errors), chấp nhận warnings
- [ ] **0.3** Verify packages trong `manifest.json`:
  ```
  com.unity.sentis: 2.1.3           ← ONNX inference
  com.unity.render-pipelines.universal: 14.0.12  ← URP
  com.unity.nuget.newtonsoft-json: 3.2.2         ← JSON
  com.unity.textmeshpro: 3.0.7                   ← UI text
  ```
- [ ] **0.4** Verify plugins:
  - `Assets/Plugins/UniTask/` — folder tồn tại, có `Runtime/` và `Editor/`
  - `Assets/Packages/sqlite-net-pcl.1.9.172/lib/netstandard2.0/SQLite-net.dll` — 107KB
- [ ] **0.5** Verify ONNX model:
  - `Assets/Models_ONNX/gesture_model.onnx` — 234KB
  - `Assets/Models_ONNX/gesture_model.onnx.data` — 1.9MB
- [ ] **0.6** Verify `Assets/StreamingAssets/config.json` tồn tại
- [ ] **0.7** Cài Python environment cho `1_AI_Model_Training/`:
  ```powershell
  cd D:\WORKSAPCE\Monitor_AR\1_AI_Model_Training
  pip install -r requirements.txt
  ```
- [ ] **0.8** Cấu hình biến môi trường Gemini (tùy chọn):
  ```powershell
  [System.Environment]::SetEnvironmentVariable("GEMINI_API_KEY", "your-key", "User")
  ```

#### Tiêu chí hoàn thành (Definition of Done)
- Unity Console hiển thị 0 errors
- Tất cả 11 scripts C# compile thành công
- `Play Mode` vào được mà không crash

#### Rủi ro & Xử lý
| Rủi ro | Xử lý |
|---|---|
| Unity phiên bản khác 2022.3.x | Cài đúng 2022.3 LTS từ Unity Hub |
| NuGet packages không restore | Click chuột phải `Assets/packages.config` → Restore |
| Sentis version conflict | Kiểm tra `manifest.json` phải ghi `"com.unity.sentis": "2.1.3"` |

---

### ═══════════════════════════════════════════════════
### PHASE 1: HOÀN THIỆN CORE ENGINE & DATA LAYER (5-7 ngày)
### ═══════════════════════════════════════════════════

**Mục tiêu:** StandardScaler hoạt động, Scene cơ bản chạy được, test end-to-end Pinch→Grab→Snap thành công với MockProvider.

---

#### Task 1.1: Export StandardScaler Params (Python)

**File:** [8_export_scaler.py](file:///d:/WORKSAPCE/Monitor_AR/1_AI_Model_Training/Scripts/8_export_scaler.py)

```powershell
cd D:\WORKSAPCE\Monitor_AR\1_AI_Model_Training
python Scripts/8_export_scaler.py
```

**Output:** `Models/scaler_params.json` — JSON chứa `mean[63]` và `scale[63]`

**Copy vào Unity:**
```powershell
copy "D:\WORKSAPCE\Monitor_AR\1_AI_Model_Training\Models\scaler_params.json" `
     "D:\WORKSAPCE\Monitor_AR\2_Unity_Project\Motion_AR\Assets\StreamingAssets\scaler_params.json"
```

**Kiểm tra:** File JSON phải có đúng 63 giá trị trong mỗi array `mean` và `scale`.

---

#### Task 1.2: Thêm Normalize Logic trong GestureDetector.cs

**File sửa:** [GestureDetector.cs](file:///d:/WORKSAPCE/Monitor_AR/2_Unity_Project/Motion_AR/Assets/Scripts/Interaction/GestureDetector.cs)

**Thay đổi cần làm:**

```csharp
// === THÊM FIELDS MỚI ===
private float[] _scalerMean;
private float[] _scalerScale;
private bool _scalerLoaded;

// === THÊM TRONG Start() ===
LoadScalerParams();

// === HÀM MỚI: Load scaler từ StreamingAssets ===
/// <summary>
/// Loads StandardScaler parameters (mean and scale arrays) from
/// StreamingAssets/scaler_params.json for feature normalization.
/// </summary>
private void LoadScalerParams()
{
    _scalerLoaded = false;
    string path = System.IO.Path.Combine(
        Application.streamingAssetsPath, "scaler_params.json");

    if (!System.IO.File.Exists(path))
    {
        Debug.LogWarning(
            "[GestureDetector] scaler_params.json not found. " +
            "Inference will run WITHOUT normalization (accuracy may suffer).");
        return;
    }

    try
    {
        string json = System.IO.File.ReadAllText(path);
        var jObj = Newtonsoft.Json.Linq.JObject.Parse(json);
        _scalerMean = jObj["mean"].ToObject<float[]>();
        _scalerScale = jObj["scale"].ToObject<float[]>();

        if (_scalerMean.Length == TOTAL_FEATURES &&
            _scalerScale.Length == TOTAL_FEATURES)
        {
            _scalerLoaded = true;
            Debug.Log("[GestureDetector] Scaler params loaded successfully.");
        }
        else
        {
            Debug.LogError(
                $"[GestureDetector] Scaler dimension mismatch: " +
                $"mean={_scalerMean.Length}, scale={_scalerScale.Length}, " +
                $"expected={TOTAL_FEATURES}");
        }
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"[GestureDetector] Failed to load scaler: {ex.Message}");
    }
}

// === HÀM MỚI: Normalize features ===
/// <summary>
/// Applies StandardScaler normalization: (x - mean) / scale for each feature.
/// </summary>
private void NormalizeFeatures(float[] features)
{
    if (!_scalerLoaded) return;
    for (int i = 0; i < features.Length && i < TOTAL_FEATURES; i++)
    {
        if (_scalerScale[i] > 1e-8f)
        {
            features[i] = (features[i] - _scalerMean[i]) / _scalerScale[i];
        }
    }
}
```

**Sửa `ProcessDynamicGesture()`** — thêm normalize trước khi add vào sliding window:

```csharp
// Flatten 21 landmarks × 3 floats = float[63].
float[] features = new float[TOTAL_FEATURES];
for (int i = 0; i < LANDMARKS_COUNT; i++)
{
    int offset = i * FEATURES_PER_LANDMARK;
    features[offset] = data.Landmarks[i].x;
    features[offset + 1] = data.Landmarks[i].y;
    features[offset + 2] = data.Landmarks[i].z;
}

// ★ THÊM DÒNG NÀY: Normalize trước khi add vào buffer
NormalizeFeatures(features);

// Add to sliding window (FIFO).
_slidingWindow.Add(features);
```

**Import cần thêm ở đầu file:**
```csharp
using Newtonsoft.Json.Linq; // cho JObject.Parse
```

---

#### Task 1.3: Setup Scene Cơ Bản

**File tạo mới:** `Assets/Scenes/TestScene.unity` (tạo trong Unity Editor)

**Hierarchy cần tạo:**

```
TestScene
├── Main Camera (tag: MainCamera)
│   └── URP Camera Data
├── Directional Light
├── --- SYSTEM ---
│   ├── [GO] HandTrackingSystem
│   │   ├── Component: HandTrackingReceiver
│   │   ├── Component: MockHandTrackingProvider
│   │   └── Component: GestureDetector (assign ONNX model)
│   ├── [GO] PinchGrabSystem
│   │   └── Component: PinchGrabber (ref → GestureDetector, HandTrackingReceiver)
│   ├── [GO] SessionManager
│   │   └── Component: SessionManager
│   └── [GO] GeminiConnector
│       └── Component: GeminiApiConnector
├── --- CURSOR ---
│   └── [GO] CursorVisual (Sphere, scale 0.05, emission cyan)
│       → Assign vào HandTrackingReceiver.cursorVisual
├── --- WORKSPACE ---
│   ├── [GO] Part_A (Cube, scale 0.3, Layer: "Grabbable")
│   │   └── Component: GrabbablePart (implements IGrabbable)
│   │   └── Component: Rigidbody (useGravity: false)
│   │   └── Component: BoxCollider
│   ├── [GO] Part_B (Cylinder, scale 0.2)
│   │   └── Component: GrabbablePart
│   │   └── Component: Rigidbody (useGravity: false)
│   │   └── Component: CapsuleCollider
│   └── [GO] SnapZone_A
│       └── Component: SnapZone (expectedPartID: "part_a")
│       └── Gizmo: WireSphere (cyan)
└── --- UI ---
    └── [GO] Canvas (Screen Space - Overlay, sẽ build ở Phase 3)
```

**File mới cần tạo:** `Assets/Scripts/Interaction/GrabbablePart.cs`

```csharp
using UnityEngine;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Concrete implementation of IGrabbable for 3D parts in the training scene.
    /// Attach to any GameObject that should be grabbable via pinch gesture.
    /// </summary>
    public class GrabbablePart : MonoBehaviour, IGrabbable
    {
        [SerializeField]
        [Tooltip("Unique identifier for this part (e.g., 'monitor_screen').")]
        private string _partID;

        [SerializeField]
        [Tooltip("Whether this part can currently be grabbed.")]
        private bool _canGrab = true;

        private Renderer _renderer;
        private Color _originalColor;

        /// <summary>Unique identifier for this grabbable part.</summary>
        public string PartID => _partID;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer == null)
                _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null)
                _originalColor = _renderer.material.color;
        }

        /// <summary>Returns true if the part can be grabbed.</summary>
        public bool CanGrab() => _canGrab;

        /// <summary>Called when grabbed. Applies visual feedback.</summary>
        public void OnGrab()
        {
            if (_renderer != null)
                _renderer.material.color = Color.cyan;
        }

        /// <summary>Called when released. Restores original color.</summary>
        public void OnRelease()
        {
            if (_renderer != null)
                _renderer.material.color = _originalColor;
        }

        /// <summary>Lock/unlock grabbability (e.g., after snapping).</summary>
        public void SetGrabbable(bool canGrab)
        {
            _canGrab = canGrab;
        }
    }
}
```

**Cần tạo Layer:** Edit → Project Settings → Tags and Layers → Add "Grabbable" layer

---

#### Task 1.4: Tạo LessonData Mẫu

Trong Unity Editor: **Right-click Assets → Create → AR_Training → LessonData**

Cấu hình:
```
lessonID: "demo_lesson_01"
lessonTitle: "Demo — Lắp ráp cơ bản"
description: "Bài học demo test flow"
assemblySteps:
  [0] stepIndex: 0, requiredPartID: "part_a", snapZoneID: "snap_a", instructionText: "Gắp Part A và đặt vào vị trí A"
  [1] stepIndex: 1, requiredPartID: "part_b", snapZoneID: "snap_b", instructionText: "Gắp Part B và đặt vào vị trí B"
```

---

#### Task 1.5: Kết nối SnapZone với SessionManager

**File mới:** `Assets/Scripts/Interaction/TrainingFlowController.cs`

```csharp
using UnityEngine;
using MotionAR.Core;
using MotionAR.Data;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Orchestrates the training flow: listens for snap events,
    /// validates against the current step, advances or records mistakes.
    /// </summary>
    public class TrainingFlowController : MonoBehaviour
    {
        [SerializeField] private PinchGrabber _pinchGrabber;
        [SerializeField] private LessonData _lessonData;

        private void OnEnable()
        {
            if (_pinchGrabber != null)
                _pinchGrabber.OnObjectReleased += HandleObjectReleased;
        }

        private void OnDisable()
        {
            if (_pinchGrabber != null)
                _pinchGrabber.OnObjectReleased -= HandleObjectReleased;
        }

        private void Start()
        {
            if (_lessonData != null && SessionManager.Instance != null)
            {
                SessionManager.Instance.StartLesson(_lessonData);
            }
        }

        private void HandleObjectReleased(GameObject part, Vector3 position)
        {
            // Find nearest snap zone
            SnapZone nearestZone = SnapZone.FindNearestSnapZone(position, 1.0f);
            if (nearestZone == null) return;

            bool snapped = nearestZone.TrySnap(part);
            if (!snapped)
            {
                SessionManager.Instance?.RecordMistake();
                return;
            }

            // Verify correct step
            StepData currentStep = SessionManager.Instance?.GetCurrentStep();
            if (currentStep == null) return;

            IGrabbable grabbable = part.GetComponent<IGrabbable>();
            if (grabbable != null &&
                grabbable.PartID == currentStep.requiredPartID &&
                nearestZone.SnapZoneID == currentStep.snapZoneID)
            {
                // Correct placement — lock part and advance
                GrabbablePart gp = part.GetComponent<GrabbablePart>();
                if (gp != null) gp.SetGrabbable(false);
                SessionManager.Instance.AdvanceStep();
            }
            else
            {
                SessionManager.Instance.RecordMistake();
            }
        }
    }
}
```

---

#### Task 1.6: Test End-to-End với MockProvider

**Bước test thủ công trong Play Mode:**

1. ✅ Di chuột → CursorVisual di chuyển mượt mà theo chuột
2. ✅ Click trái + di chuột gần Part_A → Part_A bị grab (đổi màu cyan, scale -5%)
3. ✅ Kéo Part_A vào SnapZone_A → snap + audio (nếu có)
4. ✅ Console log: `[SessionManager] Advanced to step 1`
5. ✅ Kéo Part_B vào SnapZone_B → log: `[SessionManager] Lesson complete! Score: 100`
6. ✅ Kéo sai Part_B vào SnapZone_A → log: `[SessionManager] Mistake recorded`
7. ✅ GestureDetector log: `[GestureDetector] ONNX model loaded successfully`
8. ✅ GestureDetector log: `[GestureDetector] Scaler params loaded successfully`

#### Definition of Done
- MockProvider → CursorVisual mượt mà
- Grab/Release hoạt động không giật
- Snap đúng vị trí → advance step
- Snap sai → mistake recorded
- Lesson complete → score saved to SQLite
- ONNX inference chạy không crash (console không có error)

#### Rủi ro & Xử lý
| Rủi ro | Xử lý |
|---|---|
| `scaler_params.json` dimension mismatch | Chạy lại `8_export_scaler.py`, kiểm tra `raw_data.csv` có 63 feature columns |
| ONNX model crash khi inference | Kiểm tra `.onnx.data` đã copy, GPU driver cập nhật. Fallback: `BackendType.CPU` |
| SnapZone không detect collider | Kiểm tra Layer "Grabbable" gán đúng, Collider không bị trigger |
| Cursor không di chuyển | Kiểm tra `_mainCamera` reference, `_virtualPlaneZ > 0` |

---

### ═══════════════════════════════════════════════════
### PHASE 2: TÍCH HỢP HAND TRACKING THỰC TẾ (5-8 ngày)
### ═══════════════════════════════════════════════════

**Mục tiêu:** Người dùng có thể dùng tay thật qua webcam để điều khiển ứng dụng.

---

#### Task 2.1: Chọn phương án MediaPipe

| Phương án | Ưu | Nhược | Khuyến nghị |
|---|---|---|---|
| **A. MediaPipe Unity Plugin (hoangkimdev)** | Native, nhanh, tích hợp sâu | Khó cài, cần native build | ⭐ Tốt nhất nếu build thành công |
| **B. Python Bridge (UDP/WebSocket)** | Dễ cài, MediaPipe Python ổn định | Thêm latency, cần chạy 2 process | ✅ Backup plan |
| **C. OpenCV + DNN trong Unity** | Không dependency ngoài | Chậm, không chính xác bằng MediaPipe | ❌ Không khuyến nghị |

**Khuyến nghị: Phương án B (Python Bridge)** — Ổn định nhất, dễ debug, production-ready.

---

#### Task 2.2: Implement Python MediaPipe Server

**File mới:** `1_AI_Model_Training/Scripts/mediapipe_server.py`

```python
"""
UDP server gửi 21 hand landmarks từ MediaPipe tới Unity.
Protocol: JSON qua UDP, mỗi frame 1 packet.
"""
import cv2
import mediapipe as mp
import json
import socket
import time

UDP_IP = "127.0.0.1"
UDP_PORT = 5065

mp_hands = mp.solutions.hands
hands = mp_hands.Hands(
    static_image_mode=False,
    max_num_hands=1,
    min_detection_confidence=0.7,
    min_tracking_confidence=0.5
)

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
cap = cv2.VideoCapture(0)

print(f"MediaPipe server started on {UDP_IP}:{UDP_PORT}")
while cap.isOpened():
    ret, frame = cap.read()
    if not ret:
        continue

    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = hands.process(rgb)

    if results.multi_hand_landmarks:
        hand = results.multi_hand_landmarks[0]
        landmarks = []
        for lm in hand.landmark:
            landmarks.append({"x": lm.x, "y": lm.y, "z": lm.z})
        
        packet = json.dumps({
            "tracking": True,
            "landmarks": landmarks,
            "timestamp": time.time()
        })
        sock.sendto(packet.encode(), (UDP_IP, UDP_PORT))
    else:
        packet = json.dumps({"tracking": False})
        sock.sendto(packet.encode(), (UDP_IP, UDP_PORT))
    
    # Display preview
    cv2.imshow("MediaPipe", frame)
    if cv2.waitKey(1) & 0xFF == 27:  # ESC to quit
        break

cap.release()
cv2.destroyAllWindows()
```

---

#### Task 2.3: Implement Unity UDP Receiver

**File mới:** `Assets/Scripts/Interaction/MediaPipeUDPProvider.cs`

```csharp
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Receives hand landmark data from a Python MediaPipe server via UDP.
    /// Runs on a background thread and pushes data into the Producer-Consumer queue.
    /// </summary>
    public class MediaPipeUDPProvider : MonoBehaviour, IHandTrackingProvider
    {
        [SerializeField] private int _port = 5065;
        [SerializeField] private bool _autoStart = true;

        public bool IsAvailable => _isRunning;
        public event Action<HandData> OnHandDataReceived;

        private UdpClient _udpClient;
        private Thread _receiveThread;
        private volatile bool _isRunning;

        /// <summary>Starts listening for UDP packets on a background thread.</summary>
        public void StartTracking()
        {
            if (_isRunning) return;
            _isRunning = true;
            _udpClient = new UdpClient(_port);
            _receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "MediaPipe-UDP"
            };
            _receiveThread.Start();
            Debug.Log($"[MediaPipeUDP] Listening on port {_port}");
        }

        /// <summary>Stops listening and cleans up resources.</summary>
        public void StopTracking()
        {
            _isRunning = false;
            _udpClient?.Close();
        }

        private void Start()
        {
            if (_autoStart) StartTracking();
        }

        private void OnDestroy() => StopTracking();

        private void ReceiveLoop()
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            while (_isRunning)
            {
                try
                {
                    byte[] data = _udpClient.Receive(ref remote);
                    string json = Encoding.UTF8.GetString(data);
                    ProcessPacket(json);
                }
                catch (SocketException) when (!_isRunning) { break; }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MediaPipeUDP] {ex.Message}");
                }
            }
        }

        private void ProcessPacket(string json)
        {
            try
            {
                JObject packet = JObject.Parse(json);
                bool tracking = packet.Value<bool>("tracking");
                if (!tracking) return;

                JArray landmarks = (JArray)packet["landmarks"];
                if (landmarks == null || landmarks.Count < 21) return;

                Vector3[] positions = new Vector3[21];
                for (int i = 0; i < 21; i++)
                {
                    JObject lm = (JObject)landmarks[i];
                    // MediaPipe: x,y ∈ [0,1], z relative
                    // Convert to normalized space matching MockProvider
                    positions[i] = new Vector3(
                        (lm.Value<float>("x") - 0.5f) * 0.4f,
                        (lm.Value<float>("y") - 0.5f) * -0.4f, // flip Y
                        lm.Value<float>("z") * 0.1f
                    );
                }

                // CRITICAL: Push into Producer-Consumer queue
                HandData handData = new HandData(positions, UnityEngine.Time.time, true);
                HandTrackingReceiver.EnqueueHandData(handData);
                OnHandDataReceived?.Invoke(handData);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MediaPipeUDP] Parse error: {ex.Message}");
            }
        }
    }
}
```

> [!IMPORTANT]
> `Time.time` không thể được gọi từ background thread. Thay vào đó, dùng một biến `_lastMainThreadTime` được cập nhật mỗi frame trong `Update()`, hoặc dùng timestamp khác (ví dụ `0f`). Cần sửa dòng `HandData(positions, UnityEngine.Time.time, true)` thành `HandData(positions, _lastMainThreadTime, true)` với `_lastMainThreadTime` được update trên Main Thread.

---

#### Task 2.4: Webcam Background Renderer

**File mới:** `Assets/Scripts/Interaction/WebcamBackground.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Displays the webcam feed as the background layer of the AR scene.
    /// Mirrors the image horizontally to match the "magic mirror" UX.
    /// </summary>
    public class WebcamBackground : MonoBehaviour
    {
        [SerializeField] private RawImage _backgroundImage;
        [SerializeField] private bool _mirrorHorizontally = true;
        private WebCamTexture _webcamTexture;

        private void Start()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogWarning("[WebcamBG] No webcam detected.");
                return;
            }

            _webcamTexture = new WebCamTexture(devices[0].name, 1280, 720, 30);
            _webcamTexture.Play();
            _backgroundImage.texture = _webcamTexture;

            if (_mirrorHorizontally)
            {
                _backgroundImage.rectTransform.localScale =
                    new Vector3(-1, 1, 1);
            }
        }

        private void OnDestroy()
        {
            if (_webcamTexture != null && _webcamTexture.isPlaying)
                _webcamTexture.Stop();
        }
    }
}
```

---

#### Definition of Done (Phase 2)
- Python `mediapipe_server.py` chạy → hiện preview webcam
- Unity nhận UDP → cursor di chuyển theo tay thật
- Pinch bằng tay thật → grab object
- Release → snap hoạt động
- Timeout 0.3s khi rụt tay → auto-release
- Webcam feed hiển thị làm background

#### Rủi ro & Xử lý
| Rủi ro | Xử lý |
|---|---|
| UDP packet loss | Tăng buffer size, thêm sequence number |
| `Time.time` trên background thread | Dùng biến `volatile float` cập nhật từ Main Thread |
| Webcam bị chiếm bởi Python | Python dùng OpenCV camera, Unity dùng `WebCamTexture` → cần 2 webcam hoặc share |
| MediaPipe thiếu sáng | Thêm check brightness → hiển thị warning UI |

> [!WARNING]
> **Camera sharing:** Nếu chỉ có 1 webcam, Python MediaPipe sẽ chiếm camera. Hai giải pháp:
> 1. **Dùng 2 webcam** — 1 cho MediaPipe, 1 cho Unity background
> 2. **Stream frame từ Python → Unity** — Python gửi cả frame qua UDP/shared memory (phức tạp hơn)
> 3. **Dùng MediaPipe native plugin** — Tránh 2-process hoàn toàn

---

### ═══════════════════════════════════════════════════
### PHASE 3: UI/UX HOLOGRAPHIC HUD (7-10 ngày)
### ═══════════════════════════════════════════════════

**Mục tiêu:** Giao diện Sci-Fi HUD theo Master Plan Chương VII.

#### Task List

| # | Task | File | Mô tả |
|---|---|---|---|
| 3.1 | **Cursor Visual** | `Assets/Scripts/UI/CursorEffects.cs` | Ring + pulse animation khi pinch, trail renderer |
| 3.2 | **Panel trượt** | `Assets/Scripts/UI/SlidePanel.cs` | Panel trái (tiến độ), phải (thông tin), auto-hide 5s |
| 3.3 | **Hover-to-click** | `Assets/Scripts/UI/HoverButton.cs` | Loading ring 0.3s, kích hoạt + beep |
| 3.4 | **Object Highlight** | `Assets/Shaders/OutlineShader.shader` | Outline emissive khi tay gần linh kiện |
| 3.5 | **URP Bloom** | URP Asset Settings | Post-processing Bloom cho emissive glow |
| 3.6 | **Step Instruction Panel** | `Assets/Scripts/UI/InstructionDisplay.cs` | Subscribe `SessionManager.OnStepChanged` → hiển thị text |
| 3.7 | **Error Flash Overlay** | `Assets/Scripts/UI/ErrorOverlay.cs` | Screen flash đỏ khi lắp sai |
| 3.8 | **Score Panel** | `Assets/Scripts/UI/ScoreDisplay.cs` | Hiển thị khi lesson complete |

#### Thiết kế màu sắc (theo Master Plan §7.1)
```
Primary:    Cyan        #00FFFF  → Cursor, UI borders
Success:    Neon Green  #39FF14  → Snap thành công
Error:      Neon Red    #FF073A  → Lắp sai
Background: Smoke Black rgba(0,0,0,0.4)  → Panel nền
Font:       Inter / Roboto (Google Fonts via TMP)
```

#### Definition of Done
- Cursor có visual effect (ring + pulse)
- Panel trượt ra/vào mượt mà khi swipe
- Hover-to-click hoạt động trên nút UI
- Linh kiện phát sáng outline khi tay gần
- Bloom post-processing cho Sci-Fi feel
- Instruction text tự cập nhật theo step

---

### ═══════════════════════════════════════════════════
### PHASE 4: TÍCH HỢP AI TUTOR (5-7 ngày)
### ═══════════════════════════════════════════════════

**Mục tiêu:** Người dùng hỏi AI bằng giọng nói, nhận câu trả lời + highlight hướng dẫn.

#### Task List

| # | Task | File | Mô tả |
|---|---|---|---|
| 4.1 | **Voice Input Manager** | `Assets/Scripts/AI/VoiceInputManager.cs` | `Microphone.Start/End`, gesture-triggered (Tap/Fist) |
| 4.2 | **Speech-to-Text** | `Assets/Scripts/AI/SpeechToText.cs` | Gemini multimodal hoặc Google STT API |
| 4.3 | **AI Chat Panel** | `Assets/Scripts/UI/AIChatPanel.cs` | Hiển thị câu hỏi + trả lời |
| 4.4 | **Context Integration** | Sửa `ContextBuilder.cs` | Thêm `heldPartId` thực tế từ PinchGrabber |
| 4.5 | **Highlight System** | `Assets/Scripts/AI/HighlightResponder.cs` | Parse AI response → highlight SnapZone đích |
| 4.6 | **TTS (Text-to-Speech)** | `Assets/Scripts/AI/TextToSpeech.cs` | Đọc câu trả lời AI (optional, dùng `SpeechSynthesizer`) |

#### Workflow Voice Input:
```
Tap/Fist gesture (ONNX) → Mic ON → Ghi âm →
Nhả tay → Mic OFF → Audio → STT → Text →
ContextBuilder.BuildContextJson() + Text →
GeminiApiConnector.AskGeminiAsync() → Response →
AIChatPanel hiển thị + HighlightResponder highlight
```

#### Cấu trúc Payload gửi Gemini (đã có trong ContextBuilder):
```json
{
  "lessonId": "engine_v1",
  "currentStep": 2,
  "totalSteps": 5,
  "currentInstruction": "Lắp bộ lọc dầu vào block máy",
  "heldPartId": "oil_filter",
  "mistakeCount": 1,
  "snapZonesStatus": [
    {"id": "sz_cylinder", "occupied": true, "partId": "piston_01"},
    {"id": "sz_oil_filter", "occupied": false, "partId": ""}
  ]
}
```

#### Definition of Done
- Gesture Tap → mic mở, visual indicator
- Nhả gesture → mic tắt, audio gửi đi
- Gemini trả lời hiển thị trên chat panel
- AI highlight đúng SnapZone cần lắp tiếp

---

### ═══════════════════════════════════════════════════
### PHASE 5: TỐI ƯU & BUILD (5-7 ngày)
### ═══════════════════════════════════════════════════

**Mục tiêu:** Ứng dụng chạy ≥30 FPS, build thành `.exe` standalone.

#### Task List

| # | Task | Mô tả |
|---|---|---|
| 5.1 | **Profiling** | Unity Profiler → xác định bottleneck (GPU/CPU) |
| 5.2 | **ONNX Inference Optimization** | Batch inference mỗi 2-3 frames thay vì mỗi frame |
| 5.3 | **Object Pooling** | Pool cho audio sources, UI elements |
| 5.4 | **LOD cho mô hình 3D** | Giảm poly count khi camera xa |
| 5.5 | **Dynamic Z-Axis Calibration** | Bounding box + push-pull gesture (Master Plan §5.1) |
| 5.6 | **Login Screen** | UI đăng nhập → `SQLiteConnector.GetOrCreateUserAsync()` |
| 5.7 | **Scoreboard** | Hiển thị lịch sử → `GetSessionHistoryAsync()` |
| 5.8 | **Audio Assets** | Snap click, error buzz, UI beep sounds |
| 5.9 | **IL2CPP Build** | Build Settings: IL2CPP, x86_64, Windows Desktop |
| 5.10 | **Installer** | Đóng gói .exe + data files |

#### Build Settings
```
Platform:           Windows
Architecture:       x86_64
Scripting Backend:  IL2CPP
API Compatibility:  .NET Standard 2.1
Compression Method: LZ4HC
```

#### Definition of Done
- FPS ≥ 30 trên Core i5, GPU tích hợp
- `.exe` chạy standalone không cần Unity Editor
- Login → Lesson → Practice → Score → History flow hoàn chỉnh

---

### ═══════════════════════════════════════════════════
### PHASE 6: MỞ RỘNG TƯƠNG LAI
### ═══════════════════════════════════════════════════

| Feature | Mô tả | Ưu tiên |
|---|---|---|
| Multi-webcam | Hỗ trợ 2+ webcam cho tracking chính xác hơn | P3 |
| Multiplayer | Nhiều người cùng thực hành qua mạng LAN | P3 |
| Export Report | Xuất PDF báo cáo kết quả đào tạo | P2 |
| Custom 3D Models | Import FBX/OBJ từ CAD software | P2 |
| Mobile Companion | App phụ trên điện thoại để review kết quả | P3 |
| Cloud Sync | Đồng bộ kết quả lên cloud cho quản lý | P3 |

---

## C. BACKLOG ƯU TIÊN

| ID | Mô tả | Phase | Ưu tiên | Est. (h) | Phụ thuộc | Ghi chú |
|---|---|---|---|---|---|---|
| **T-001** | Verify Unity 0 errors | 0 | P0 | 1 | — | Mở Unity, check Console |
| **T-002** | Export scaler_params.json | 1 | P0 | 1 | Python env | Chạy 8_export_scaler.py |
| **T-003** | Load scaler trong GestureDetector | 1 | P0 | 3 | T-002 | Code snippet ở Phase 1 |
| **T-004** | Tạo GrabbablePart.cs | 1 | P0 | 2 | — | Implement IGrabbable |
| **T-005** | Setup TestScene | 1 | P0 | 4 | T-004 | Hierarchy ở Phase 1 |
| **T-006** | Tạo LessonData mẫu | 1 | P1 | 1 | T-005 | ScriptableObject |
| **T-007** | TrainingFlowController.cs | 1 | P1 | 4 | T-005, T-006 | Orchestrate snap → advance |
| **T-008** | Test E2E MockProvider | 1 | P1 | 4 | T-003~007 | 8 test cases ở Phase 1 |
| **T-009** | Python MediaPipe server | 2 | P1 | 4 | Python env | UDP JSON protocol |
| **T-010** | MediaPipeUDPProvider.cs | 2 | P1 | 6 | T-009 | Background thread → Queue |
| **T-011** | WebcamBackground.cs | 2 | P1 | 3 | — | Mirror, RawImage |
| **T-012** | Test hand tracking thực | 2 | P1 | 4 | T-009~011 | Tay thật qua webcam |
| **T-013** | Fix Time.time thread issue | 2 | P0 | 2 | T-010 | Volatile timestamp |
| **T-014** | CursorEffects.cs | 3 | P2 | 4 | — | Ring + pulse + trail |
| **T-015** | SlidePanel.cs | 3 | P2 | 6 | — | DOTween hoặc custom lerp |
| **T-016** | HoverButton.cs | 3 | P2 | 4 | — | Loading ring 0.3s |
| **T-017** | Outline shader | 3 | P2 | 6 | — | URP compatible |
| **T-018** | URP Bloom setup | 3 | P2 | 2 | — | Post-processing volume |
| **T-019** | InstructionDisplay.cs | 3 | P2 | 3 | T-007 | Subscribe OnStepChanged |
| **T-020** | ErrorOverlay.cs | 3 | P2 | 2 | — | Screen flash red |
| **T-021** | ScoreDisplay.cs | 3 | P2 | 3 | — | Subscribe OnLessonCompleted |
| **T-022** | VoiceInputManager.cs | 4 | P2 | 6 | T-012 | Gesture-triggered mic |
| **T-023** | SpeechToText integration | 4 | P2 | 8 | T-022 | Gemini/Google STT |
| **T-024** | AIChatPanel.cs | 4 | P2 | 4 | T-023 | Display Q&A |
| **T-025** | HighlightResponder.cs | 4 | P2 | 4 | T-024 | Parse → highlight |
| **T-026** | Profiling & optimization | 5 | P2 | 8 | T-012 | Target ≥30 FPS |
| **T-027** | Z-Axis Calibration | 5 | P2 | 8 | T-012 | Bounding box + push-pull |
| **T-028** | Login Screen UI | 5 | P2 | 4 | T-021 | SQLite user management |
| **T-029** | Audio assets integration | 5 | P2 | 3 | — | Snap, error, UI sounds |
| **T-030** | IL2CPP Windows build | 5 | P1 | 4 | T-026 | .exe standalone |
| **T-031** | User testing | 5 | P1 | 8 | T-030 | 3-5 người dùng thực |
| **T-032** | Documentation update | 5 | P2 | 4 | T-031 | README, user guide |

---

## D. HƯỚNG DẪN XỬ LÝ LỖI THƯỜNG GẶP

### Lỗi 1: Mất tay (Tracking Lost)
```
Triệu chứng: Object dính lơ lửng trên không
Cơ chế bảo vệ đã có: HandTrackingReceiver._trackingTimeoutSeconds = 0.3s
                     → ForceReleaseObject() → PinchGrabber.HandleTrackingLost()
                     → GestureDetector._slidingWindow.Clear()
Kiểm tra: Console log "[HandTrackingReceiver] Tracking lost"
```

### Lỗi 2: Gemini API Timeout
```
Triệu chứng: UI không phản hồi khi hỏi AI
Cơ chế bảo vệ: GeminiApiConnector timeout = 5s (config.json)
               CancellationToken + linkedCts
               Trả về: "⚠ Kết nối AI thất bại. Vui lòng thử lại sau."
Kiểm tra: Console log "[GeminiApiConnector] Request timed out"
```

### Lỗi 3: Thiếu file model ONNX
```
Triệu chứng: "[GestureDetector] ONNX model not assigned"
Xử lý: Kiểm tra Inspector → GestureDetector → _gestureModel field
        Kéo gesture_model.onnx từ Models_ONNX/ vào field
        ĐẢM BẢO gesture_model.onnx.data nằm cùng thư mục
```

### Lỗi 4: Tensor shape mismatch
```
Triệu chứng: "[GestureDetector] Inference error: ..."
Nguyên nhân: Model expect [1,15,63] nhưng data khác shape
Kiểm tra: WINDOW_SIZE=15, TOTAL_FEATURES=63 trong GestureDetector.cs
          Đảm bảo scaler_params.json có đúng 63 mean và 63 scale
```

### Lỗi 5: SQLite database locked
```
Triệu chứng: "[SQLiteConnector] InsertSessionAsync failed"
Nguyên nhân: Concurrent access không đi qua lock
Xử lý: Đã có `lock (_lock)` trong SQLiteConnector — nếu vẫn lỗi,
        kiểm tra có instance khác đang truy cập database.db không
```

### Lỗi 6: UDP port already in use
```
Triệu chứng: MediaPipeUDPProvider crash on Start
Xử lý: Đổi port (default 5065), hoặc kill process cũ
        netstat -ano | findstr "5065"
        taskkill /PID <pid> /F
```

---

## E. MÔI TRƯỜNG & CẤU HÌNH

### Unity Project

| Thành phần | Version/Config |
|---|---|
| Unity Editor | **2022.3.x LTS** (KHÔNG dùng Unity 6) |
| Render Pipeline | URP 14.0.12 |
| Scripting Backend | IL2CPP (build), Mono (development) |
| Architecture | x86_64 (Windows Desktop) |
| .NET API | .NET Standard 2.1 |

### Packages (`manifest.json`)

| Package | Version | Vai trò |
|---|---|---|
| `com.unity.sentis` | 2.1.3 | ONNX inference (`Tensor<float>`) |
| `com.unity.render-pipelines.universal` | 14.0.12 | URP + post-processing |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | JSON serialization |
| `com.unity.textmeshpro` | 3.0.7 | UI text rendering |

### Plugins & NuGet

| Plugin | Source | Location |
|---|---|---|
| UniTask | Git URL hoặc .unitypackage | `Assets/Plugins/UniTask/` |
| sqlite-net-pcl | NuGet 1.9.172 | `Assets/Packages/sqlite-net-pcl.1.9.172/` |

### Files quan trọng

| File | Đường dẫn | Mô tả |
|---|---|---|
| ONNX model | `Assets/Models_ONNX/gesture_model.onnx` | 234KB metadata |
| ONNX weights | `Assets/Models_ONNX/gesture_model.onnx.data` | 1.9MB trọng số |
| Config | `Assets/StreamingAssets/config.json` | API key, model, timeout |
| Scaler | `Assets/StreamingAssets/scaler_params.json` | mean[63], scale[63] — **CẦN TẠO** |

### Biến môi trường

```powershell
# Tùy chọn — nếu không set, sẽ đọc từ config.json
[System.Environment]::SetEnvironmentVariable("GEMINI_API_KEY", "your-key", "User")
```

---

## F. THỜI GIAN DỰ KIẾN

| Phase | Mô tả | Est. Min (h) | Est. Max (h) | Ngày (8h/ngày) |
|---|---|---|---|---|
| 0 | Chuẩn bị & verify | 4 | 8 | 0.5 – 1 |
| 1 | Core Engine & Data | 20 | 32 | 2.5 – 4 |
| 2 | Hand Tracking | 24 | 40 | 3 – 5 |
| 3 | UI/UX HUD | 32 | 48 | 4 – 6 |
| 4 | AI Tutor | 24 | 40 | 3 – 5 |
| 5 | Optimize & Build | 32 | 48 | 4 – 6 |
| 6 | Extensions | — | — | TBD |
| **TỔNG** | **Phase 0–5** | **136** | **216** | **17 – 27** |

> **Ước tính: 4-6 tuần** cho 1 developer full-time (8h/ngày, 5 ngày/tuần)

---

## G. CÂU HỎI MỞ CẦN QUYẾT ĐỊNH

> [!IMPORTANT]
> Các câu hỏi dưới đây cần được quyết định trước khi bắt đầu Phase tương ứng.

| # | Câu hỏi | Phase | Options | Khuyến nghị |
|---|---|---|---|---|
| Q1 | **MediaPipe: Native plugin hay Python bridge?** | 2 | A: Native (.unitypackage) — nhanh hơn, 1 process<br>B: Python UDP — dễ hơn, 2 process | **B** cho MVP, **A** cho production |
| Q2 | **Camera sharing: 2 webcam hay stream từ Python?** | 2 | A: 2 webcam vật lý<br>B: Python gửi cả frame qua shared memory<br>C: Native MediaPipe (1 process) | **C** nếu native, **A** nếu Python |
| Q3 | **Speech-to-Text: Google Cloud STT hay Gemini multimodal?** | 4 | A: Google Cloud STT API (chính xác hơn)<br>B: Gemini 2.0 Flash audio input (đơn giản hơn) | **B** — giảm dependency |
| Q4 | **Mô hình 3D: Dùng asset store hay tự làm?** | 1 | A: Unity Asset Store (nhanh, professional)<br>B: Tự model trong Blender (tuỳ chỉnh cao) | **A** cho MVP |
| Q5 | **Test tự động: Có cần Unit Test không?** | 1 | A: Có — Unity Test Framework<br>B: Không — chỉ test thủ công | **A** cho SessionManager, SQLiteConnector. **B** cho UI |
