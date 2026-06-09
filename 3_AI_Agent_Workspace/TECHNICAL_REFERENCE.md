<![CDATA[# 📖 Motion AR — Tài liệu Tham khảo Kỹ thuật (Technical Reference)

> Tài liệu này mô tả chi tiết từng class, method, event và mối quan hệ giữa các module trong hệ thống **Monitor AR Technical Training Simulator**.

---

## Mục lục

- [1. Tổng quan Kiến trúc](#1-tổng-quan-kiến-trúc)
- [2. Module Interaction — Tương tác Không gian](#2-module-interaction--tương-tác-không-gian)
  - [2.1. HandData.cs](#21-handdatacs)
  - [2.2. HandTrackingReceiver.cs](#22-handtrackingreceiversc)
  - [2.3. PinchGrabber.cs](#23-pinchgrabbercs)
  - [2.4. SnapZone.cs](#24-snapzonecs)
  - [2.5. GestureDetector.cs](#25-gesturedetectorcs)
- [3. Module Core — Quản lý Trạng thái](#3-module-core--quản-lý-trạng-thái)
  - [3.1. SessionManager.cs](#31-sessionmanagercs)
- [4. Module Data — Dữ liệu Giáo án](#4-module-data--dữ-liệu-giáo-án)
  - [4.1. LessonData.cs](#41-lessondatacs)
  - [4.2. StepData.cs](#42-stepdatacs)
- [5. Module AI — Trợ giảng Gemini](#5-module-ai--trợ-giảng-gemini)
  - [5.1. ConfigLoader.cs](#51-configloadercs)
  - [5.2. ContextBuilder.cs](#52-contextbuildercs)
  - [5.3. GeminiApiConnector.cs](#53-geminiApiconnectorcs)
- [6. Module Database — Lưu trữ SQLite](#6-module-database--lưu-trữ-sqlite)
  - [6.1. SQLiteConnector.cs](#61-sqliteconnectorcs)
- [7. Design Patterns tổng hợp](#7-design-patterns-tổng-hợp)
- [8. Thuật toán lõi](#8-thuật-toán-lõi)

---

## 1. Tổng quan Kiến trúc

Hệ thống được chia thành **5 module** với **5 namespace** tách biệt:

| Namespace | Module | Vai trò | Số file |
|---|---|---|---|
| `MotionAR.Interaction` | Interaction | Tracking tay, cử chỉ, gắp/thả, snap | 5 |
| `MotionAR.Core` | Core | Quản lý trạng thái phiên học (Singleton) | 1 |
| `MotionAR.Data` | Data | ScriptableObject giáo án | 2 |
| `MotionAR.AI` | AI | Kết nối Gemini API, sinh context JSON | 3 |
| `MotionAR.Database` | Database | CRUD SQLite cục bộ | 1 |

### Quy tắc ràng buộc kỹ thuật

| # | Ràng buộc | Chi tiết |
|---|---|---|
| 1 | **Producer-Consumer bắt buộc** | KHÔNG gán `Transform.position` từ callback MediaPipe. Phải dùng `ConcurrentQueue` |
| 2 | **UniTask thay Coroutine** | Mọi API/DB call dùng `UniTask` + `CancellationToken`. KHÔNG dùng `Coroutine` hay `Thread.Sleep()` |
| 3 | **Loose Coupling** | KHÔNG dùng `GameObject.Find()`. Tham chiếu qua `[SerializeField]` |
| 4 | **Data-Driven** | Giáo án đọc từ `ScriptableObject`, không hard-code |
| 5 | **Bảo mật API Key** | Đọc từ biến môi trường hoặc `config.json` — KHÔNG viết cứng trong mã nguồn |
| 6 | **XML Documentation** | Mọi hàm public phải có `/// <summary>` |

---

## 2. Module Interaction — Tương tác Không gian

> **Namespace:** `MotionAR.Interaction`
> **Đường dẫn:** `Assets/Scripts/Interaction/`

Đây là module phức tạp nhất, chứa toàn bộ logic **Producer-Consumer**, **nhận diện cử chỉ lai**, và **tương tác vật lý 3D**.

---

### 2.1. HandData.cs

> **Vai trò:** Định nghĩa tất cả kiểu dữ liệu nền tảng — struct, enum, interface — được các file khác sử dụng.

#### `readonly struct HandData`

Đối tượng bất biến (immutable) chứa snapshot dữ liệu tay tại một thời điểm.

| Field | Kiểu | Mô tả |
|---|---|---|
| `Landmarks` | `Vector3[]` | 21 điểm khớp xương tay (MediaPipe model) |
| `Timestamp` | `float` | `Time.time` khi capture |
| `IsTracking` | `bool` | Tay đang được tracking hay không |

**Constructor:** Thực hiện **deep copy** mảng `landmarks` để đảm bảo immutability. Null input → `Array.Empty<Vector3>()`.

**Bảng chỉ số Landmark quan trọng:**

| Index | Vị trí | Sử dụng trong |
|---|---|---|
| 0 | Cổ tay (Wrist) | Điểm gốc tọa độ |
| 4 | Đầu ngón cái (Thumb Tip) | Tính khoảng cách Pinch |
| 8 | Đầu ngón trỏ (Index Tip) | Raycasting cursor + Pinch |
| 12 | Đầu ngón giữa (Middle Tip) | ONNX gesture input |
| 20 | Đầu ngón út (Pinky Tip) | ONNX gesture input |

#### `enum GestureType` — 14 loại cử chỉ

```csharp
None = 0,        SwipeLeft = 1,    SwipeRight = 2,   SwipeUp = 3,
SwipeDown = 4,   Tap = 5,          DoubleTap = 6,    Fist = 7,
OpenPalm = 8,    ThumbsUp = 9,     ThumbsDown = 10,  Peace = 11,
PointUp = 12,    Grab = 13
```

#### `enum PinchState`

```csharp
Released = 0,    Grabbed = 1
```

#### `interface IHandTrackingProvider`

Trừu tượng hóa nguồn dữ liệu tracking tay — cho phép plug bất kỳ MediaPipe plugin nào.

| Member | Kiểu | Mô tả |
|---|---|---|
| `StartTracking()` | method | Bắt đầu tracking |
| `StopTracking()` | method | Dừng tracking |
| `IsAvailable` | property | Plugin có sẵn sàng không |
| `OnHandDataReceived` | event | Phát dữ liệu `HandData` từ background thread |

#### `interface IGrabbable`

Interface cho các linh kiện 3D có thể gắp/thả.

| Member | Kiểu | Mô tả |
|---|---|---|
| `PartID` | `string` | ID duy nhất (vd: `"piston_01"`) |
| `CanGrab()` | `bool` | Linh kiện có thể gắp không |
| `OnGrab()` | void | Callback khi bị gắp |
| `OnRelease()` | void | Callback khi được thả |

#### `class MockHandTrackingProvider : MonoBehaviour, IHandTrackingProvider`

**Test Double** — giả lập tracking bằng chuột để test trong Editor không cần Webcam.
- Di chuyển chuột → di chuyển tay ảo (21 landmarks dựa trên offset cố định)
- Click chuột trái → mô phỏng Pinch (đưa ngón cái sát ngón trỏ)

---

### 2.2. HandTrackingReceiver.cs

> **Vai trò:** Consumer trong mô hình Producer-Consumer — nhận dữ liệu tay từ `ConcurrentQueue` thread-safe, xử lý trên Main Thread Unity.

#### `class HandTrackingReceiver : MonoBehaviour`

**Đây là trung tâm dữ liệu** — mọi module khác lấy dữ liệu tay từ đây thông qua events.

##### Serialized Fields (Inspector)

| Field | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `cursorVisual` | `Transform` | — | GameObject hiển thị vị trí tay 3D |
| `_virtualPlaneZ` | `float` | `5.0` | Khoảng cách mặt phẳng ảo (trục Z) |
| `_lerpSpeed` | `float` | `15.0` | Tốc độ nội suy vị trí (Smoothing) |
| `_trackingTimeoutSeconds` | `float` | `0.3` | Timeout mất tracking |
| `_mainCamera` | `Camera` | `Camera.main` | Camera chính cho Raycasting |

##### Public API

| Method / Property | Kiểu trả về | Mô tả |
|---|---|---|
| `EnqueueHandData(HandData)` | `static void` | **Producer gọi từ BẤT KỲ thread nào** — đẩy dữ liệu vào queue |
| `SmoothedCursorPosition` | `Vector3` | Vị trí cursor 3D đã làm mượt |
| `LatestHandData` | `HandData` | Frame dữ liệu mới nhất |
| `IsTrackingActive` | `bool` | Đang nhận dữ liệu (chưa timeout) |
| `ForceReleaseObject()` | `void` | Buộc giải phóng linh kiện (gọi thủ công) |

##### Events

| Event | Signature | Khi nào |
|---|---|---|
| `OnHandUpdate` | `Action<HandData>` | Mỗi frame có dữ liệu mới (trên Main Thread) |
| `OnTrackingLost` | `Action` | Queue rỗng > 0.3 giây |

##### Luồng xử lý trong `Update()`

```
1. TryDequeue TẤT CẢ dữ liệu mới (drain queue)
2. Giữ lại frame MỚI NHẤT
3. Ánh xạ Landmark[8] (ngón trỏ):
   a. Chuyển từ tọa độ chuẩn hóa → Screen Space
   b. Camera.ScreenPointToRay() → tia
   c. Tia cắt Virtual Touch Plane tại Z = _virtualPlaneZ
   d. → Thu được _cursor3DPosition
4. Lerp smoothing: _smoothedPosition = Lerp(current, target, _lerpSpeed * deltaTime)
5. Di chuyển cursorVisual
6. Fire OnHandUpdate(latestData)
7. Nếu timeout > 0.3s → Fire OnTrackingLost → ForceReleaseObject()
```

---

### 2.3. PinchGrabber.cs

> **Vai trò:** Xử lý gắp/thả linh kiện bằng cử chỉ Pinch với thuật toán Hysteresis chống rung.

#### `class PinchGrabber : MonoBehaviour`

##### Serialized Fields

| Field | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `_handTrackingReceiver` | `HandTrackingReceiver` | — | Nguồn dữ liệu tay |
| `_pinchStartThreshold` | `float` | `0.04` | Ngưỡng kích hoạt gắp (khoảng cách chuẩn hóa) |
| `_pinchEndThreshold` | `float` | `0.06` | Ngưỡng nhả (phải > start để tạo vùng chết) |
| `_grabRadius` | `float` | `0.5` | Bán kính tìm kiếm linh kiện (`OverlapSphere`) |
| `_scaleReduction` | `float` | `0.05` | Giảm scale 5% khi gắp (phản hồi thị giác) |
| `_grabbableLayer` | `LayerMask` | — | Layer filter cho Physics |

##### Events

| Event | Signature | Mô tả |
|---|---|---|
| `OnObjectGrabbed` | `Action<GameObject>` | Gắp thành công |
| `OnObjectReleased` | `Action<GameObject, Vector3>` | Thả (kèm vị trí thả — cho SnapZone) |

##### Máy trạng thái Hysteresis

```
                    d < 0.04 (pinchStart)
    ┌─────────┐  ──────────────────────►  ┌─────────┐
    │ RELEASED │                           │ GRABBED │
    └─────────┘  ◄──────────────────────  └─────────┘
                    d > 0.06 (pinchEnd)

    Vùng [0.04 → 0.06]: GIỮ NGUYÊN trạng thái cũ (Dead Zone)
```

##### Quy trình gắp (`TransitionToGrabbed`)
1. `Physics.OverlapSphere()` tìm collider trong `_grabRadius`
2. Lọc collider có component `IGrabbable`
3. Kiểm tra `IGrabbable.CanGrab()`
4. Parent object theo cursor transform
5. Scale down 5% (phản hồi thị giác "đã nắm")
6. Fire `OnObjectGrabbed`

##### Quy trình thả (`TransitionToReleased`)
1. Unparent object khỏi cursor
2. Khôi phục scale gốc
3. Gọi `IGrabbable.OnRelease()`
4. Fire `OnObjectReleased(object, releasePosition)`
5. *(Bên ngoài)* SnapZone kiểm tra vị trí thả

---

### 2.4. SnapZone.cs

> **Vai trò:** Hệ thống bắt dính (Magnetic Snap) — hút linh kiện vào khớp nối kỹ thuật đúng vị trí.

#### `class SnapZone : MonoBehaviour`

##### Serialized Fields

| Field | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `_snapZoneID` | `string` | — | ID duy nhất của vùng snap (vd: `"sz_cylinder"`) |
| `_expectedPartID` | `string` | — | PartID mong đợi (vd: `"piston_01"`) |
| `_snapRadius` | `float` | `0.3` | Bán kính dung sai snap (R_snap) |
| `_snapPoint` | `Transform` | — | Vị trí/hướng chính xác khi snap |
| `_snapSuccessSound` | `AudioClip` | — | Âm thanh snap thành công |
| `_snapErrorSound` | `AudioClip` | — | Âm thanh lắp sai |
| `_errorMaterial` | `Material` | — | Material đỏ (error flash) |
| `_errorFlashDuration` | `float` | `1.0` | Thời gian flash lỗi |

##### Public API

| Method / Property | Kiểu | Mô tả |
|---|---|---|
| `TrySnap(GameObject part)` | `bool` | Kiểm tra và thực hiện snap |
| `ResetZone()` | `void` | Reset trạng thái vùng snap |
| `FindNearestSnapZone(Vector3, float)` | `static SnapZone` | Tìm vùng snap gần nhất chưa occupied |
| `SnapZoneID` | `string` | ID (read-only) |
| `IsOccupied` | `bool` | Đã có linh kiện chưa |
| `OccupiedPartID` | `string` | ID linh kiện đang nằm trong |

##### Luồng xử lý `TrySnap()`

```
Input: GameObject part
  │
  ├─ Kiểm tra khoảng cách ≤ snapRadius?
  │   └─ KHÔNG → return false
  │
  ├─ Lấy IGrabbable.PartID từ part
  │   └─ NULL → return false
  │
  ├─ PartID == _expectedPartID?
  │   ├─ ĐÚNG → PerformSnap()
  │   │   ├── part.position = snapPoint.position
  │   │   ├── part.rotation = snapPoint.rotation
  │   │   ├── Rigidbody.isKinematic = true
  │   │   ├── Play snapSuccessSound 🔊
  │   │   ├── IsOccupied = true
  │   │   └── return true
  │   │
  │   └─ SAI → PerformErrorFeedback()
  │       ├── Play snapErrorSound 🔊
  │       ├── Flash errorMaterial (1 giây, async UniTask)
  │       ├── SessionManager.RecordMistake()
  │       └── return false
```

##### Self-Registering Registry (Pattern đặc biệt)

Tất cả `SnapZone` tự đăng ký vào `static List<SnapZone> _allZones` khi `OnEnable()` và tự hủy khi `OnDisable()`. Phương thức `FindNearestSnapZone()` duyệt danh sách này — không cần `FindObjectsOfType()`.

---

### 2.5. GestureDetector.cs

> **Vai trò:** Bộ nhận diện cử chỉ lai (Hybrid) — chạy song song 2 nhiệm vụ với độ trễ khác nhau.

#### `class GestureDetector : MonoBehaviour`

##### Serialized Fields

| Field | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `_handTrackingReceiver` | `HandTrackingReceiver` | — | Nguồn dữ liệu |
| `_gestureModel` | `ModelAsset` | `null` | File ONNX (nullable — tắt nếu null) |
| `_pinchStartThreshold` | `float` | `0.04` | Ngưỡng Pinch bắt đầu |
| `_pinchEndThreshold` | `float` | `0.06` | Ngưỡng Pinch kết thúc |
| `_confidenceThreshold` | `float` | `0.8` | Ngưỡng tin cậy ONNX (80%) |
| `_cooldownDuration` | `float` | `1.0` | Khóa 1 giây sau mỗi nhận diện |

##### Constants

```csharp
WINDOW_SIZE = 15           // Số frame trong cửa sổ trượt
LANDMARKS_COUNT = 21       // Số điểm landmark
FEATURES_PER_LANDMARK = 3  // X, Y, Z
TOTAL_FEATURES = 63        // 21 × 3
```

##### Events

| Event | Signature | Mô tả |
|---|---|---|
| `OnPinchUpdate` | `Action<PinchState, float>` | Mỗi frame — trạng thái Pinch + khoảng cách |
| `OnDynamicGestureDetected` | `Action<GestureType, float>` | Cử chỉ động phát hiện + confidence |

##### Kiến trúc Hybrid — 2 pipeline song song

```
Mỗi frame HandData đến:
  │
  ├── [TASK 1: Pinch — 0ms latency]
  │   ├── d = Euclidean(Landmark[4], Landmark[8])
  │   ├── Hysteresis: Released ↔ Grabbed
  │   └── Fire OnPinchUpdate(state, d)
  │
  └── [TASK 2: Dynamic Gesture — 15 frames latency]
      ├── Flatten 21 landmarks → float[63]
      ├── Push vào Sliding Window (FIFO, max 15)
      ├── Nếu đủ 15 frames VÀ cooldown hết:
      │   ├── Tạo TensorFloat shape [1, 15, 63]
      │   ├── Worker.Schedule(tensor)
      │   ├── Output = argmax(predictions)
      │   ├── Nếu confidence > 0.8:
      │   │   ├── Fire OnDynamicGestureDetected(type, confidence)
      │   │   └── Khóa cooldown 1 giây
      │   └── Dispose input tensor
      └── (else: chờ thêm frame)
```

##### Graceful Degradation

| Điều kiện | Hành vi |
|---|---|
| `_gestureModel == null` | Log warning, chỉ chạy Pinch detection |
| GPU backend lỗi | Tự động fallback sang CPU |
| Mất tracking | Xóa sạch Sliding Window |

---

## 3. Module Core — Quản lý Trạng thái

> **Namespace:** `MotionAR.Core`
> **Đường dẫn:** `Assets/Scripts/Core/`

---

### 3.1. SessionManager.cs

> **Vai trò:** Singleton quản lý toàn bộ trạng thái phiên học trên RAM. Thay thế Redis bằng C# in-memory.

#### `class SessionManager : MonoBehaviour`

**Pattern:** Singleton + `DontDestroyOnLoad`

##### State Fields

| Field | Kiểu | Mô tả |
|---|---|---|
| `_currentLesson` | `LessonData` | Bài học đang hoạt động |
| `_currentStepIndex` | `int` | Bước hiện tại (zero-based) |
| `_mistakeCount` | `int` | Số lỗi tích lũy |
| `_sessionStartTime` | `float` | Thời điểm bắt đầu (`Time.time`) |
| `_currentUserId` | `string` | ID người dùng đăng nhập |
| `_isLessonActive` | `bool` | Phiên đang hoạt động |

##### Public Properties

| Property | Kiểu | Quyền | Mô tả |
|---|---|---|---|
| `Instance` | `SessionManager` | `static get` | Singleton instance |
| `CurrentLesson` | `LessonData` | `get` | Bài học hiện tại |
| `CurrentStepIndex` | `int` | `get` | Bước hiện tại |
| `MistakeCount` | `int` | `get` | Số lỗi |
| `IsLessonActive` | `bool` | `get` | Đang trong phiên |
| `CurrentUserId` | `string` | `get/set` | User ID |
| `ElapsedTime` | `float` | `get` | Thời gian đã trôi qua (computed) |

##### Events

| Event | Signature | Khi nào |
|---|---|---|
| `OnLessonStarted` | `Action<LessonData>` | `StartLesson()` thành công |
| `OnStepChanged` | `Action<StepData>` | Chuyển sang bước mới |
| `OnMistakeRecorded` | `Action` | Ghi nhận lỗi |
| `OnLessonCompleted` | `Action<float>` | Hoàn thành (float = score) |

##### Public Methods

| Method | Mô tả |
|---|---|
| `StartLesson(LessonData lesson)` | Bắt đầu bài học — reset counters, fire events |
| `GetCurrentStep() → StepData` | Lấy step hiện tại (null-safe) |
| `AdvanceStep()` | Tăng bước — nếu hết bước → hoàn thành bài |
| `RecordMistake()` | Tăng `_mistakeCount` + fire event |
| `ResetSession()` | Xóa state mà không lưu DB |

##### Công thức tính điểm

```
score = max(0, 100 - mistakeCount × 10)
```

| Số lỗi | Điểm |
|---|---|
| 0 | 100 |
| 3 | 70 |
| 7 | 30 |
| ≥ 10 | 0 |

##### Lưu DB (Fire-and-Forget)

Khi hoàn thành bài, `CompleteLessonInternal()` gọi `SaveSessionToDatabase()` kiểu **fire-and-forget** (`UniTaskVoid`) — không block UI.

---

## 4. Module Data — Dữ liệu Giáo án

> **Namespace:** `MotionAR.Data`
> **Đường dẫn:** `Assets/Scripts/Data/`

---

### 4.1. LessonData.cs

> **Vai trò:** ScriptableObject chứa kịch bản bài học. Tạo qua Unity menu: **Create → AR_Training → LessonData**

#### `class LessonData : ScriptableObject`

##### Fields

| Field | Kiểu | Mô tả |
|---|---|---|
| `lessonID` | `string` | ID duy nhất (vd: `"engine_v1"`) |
| `lessonTitle` | `string` | Tên hiển thị |
| `description` | `string` | Mô tả chi tiết `[TextArea(2,4)]` |
| `assemblySteps` | `List<StepData>` | Danh sách bước lắp ráp |

##### Properties & Methods

| Member | Kiểu | Mô tả |
|---|---|---|
| `TotalSteps` | `int` | Tổng số bước (computed) |
| `GetStep(int index)` | `StepData` | Lấy bước theo index (null nếu out-of-range) |
| `OnValidate()` | `void` | Tự sắp xếp steps theo `stepIndex` trong Editor |

---

### 4.2. StepData.cs

> **Vai trò:** Data container (POCO) cho từng bước lắp ráp.

#### `[Serializable] class StepData`

| Field | Kiểu | Mô tả |
|---|---|---|
| `stepIndex` | `int` | Thứ tự bước (zero-based) |
| `requiredPartID` | `string` | ID linh kiện cần lắp |
| `snapZoneID` | `string` | ID vùng snap đích |
| `instructionText` | `string` | Hướng dẫn hiển thị `[TextArea(2,5)]` |
| `hintText` | `string` | Gợi ý cho AI `[TextArea(1,3)]` |

---

## 5. Module AI — Trợ giảng Gemini

> **Namespace:** `MotionAR.AI`
> **Đường dẫn:** `Assets/Scripts/AI/`

---

### 5.1. ConfigLoader.cs

> **Vai trò:** Đọc và cache cấu hình Gemini API theo thứ tự ưu tiên.

#### `static class ConfigLoader`

##### Thứ tự ưu tiên đọc API Key

```
1. Biến môi trường: GEMINI_API_KEY     ← Ưu tiên cao nhất
2. File: StreamingAssets/config.json    ← Fallback
3. Giá trị mặc định (model, timeout)   ← Luôn áp dụng nếu không tìm thấy
```

##### Public Properties (static)

| Property | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `GeminiApiKey` | `string` | `""` | API key |
| `GeminiModel` | `string` | `"gemini-2.0-flash"` | Model name |
| `GeminiTimeoutSeconds` | `int` | `30` | Timeout (giây) |

##### Methods

| Method | Mô tả |
|---|---|
| `LoadConfig()` | Idempotent — chỉ load 1 lần. Log error nếu không tìm thấy API key |

---

### 5.2. ContextBuilder.cs

> **Vai trò:** Sinh JSON payload mô tả trạng thái 3D hiện tại để gửi lên Gemini.

#### `static class ContextBuilder`

#### `struct SnapZoneStatus`

| Field | Kiểu | Mô tả |
|---|---|---|
| `id` | `string` | ID vùng snap |
| `occupied` | `bool` | Đã có linh kiện |
| `partId` | `string` | ID linh kiện (nếu có) |

##### Method

```csharp
public static string BuildContextJson(
    string lessonId,
    int currentStep,
    int totalSteps,
    string currentInstruction,
    string heldPartId,
    int mistakeCount,
    List<SnapZoneStatus> snapZones
) → string (JSON)
```

##### Output JSON mẫu

```json
{
  "lessonId": "engine_v1",
  "currentStep": 3,
  "totalSteps": 8,
  "currentInstruction": "Lắp piston vào xi-lanh",
  "heldPartId": "piston_01",
  "mistakeCount": 2,
  "snapZonesStatus": [
    { "id": "sz_cylinder", "occupied": false, "partId": "" }
  ]
}
```

---

### 5.3. GeminiApiConnector.cs

> **Vai trò:** Giao tiếp bất đồng bộ với Google Gemini API qua UniTask.

#### `class GeminiApiConnector : MonoBehaviour`

**Pattern:** Singleton + `DontDestroyOnLoad`

##### Public API

```csharp
public async UniTask<string> AskGeminiAsync(
    string userQuestion,    // Câu hỏi người dùng
    string contextJson,     // JSON từ ContextBuilder
    CancellationToken ct    // Token hủy
) → string                  // Câu trả lời hoặc thông báo lỗi
```

##### Luồng xử lý

```
1. Validate API key (nếu rỗng → return lỗi)
2. Build URL: {ApiBaseUrl}/{model}:generateContent?key={apiKey}
3. Build body:
   ├── system_instruction: "Bạn là trợ giảng kỹ thuật AR..."
   └── contents: [{ role: "user", parts: [{ text: "[Context]\n...\n[Question]\n..." }] }]
4. POST via UnityWebRequest + UniTask
5. Timeout: CancellationTokenSource liên kết (5 giây mặc định)
6. Parse response: candidates[0].content.parts[0].text
7. Return text (hoặc thông báo lỗi tiếng Việt)
```

##### Xử lý lỗi (KHÔNG throw exception)

| Tình huống | Hành vi |
|---|---|
| API key rỗng | Return `"⚠ Chưa cấu hình API Key..."` |
| Timeout (5s) | Return `"⚠ Kết nối AI thất bại..."` |
| Network error | Log error + return thông báo lỗi |
| Content blocked | Return `"⚠ Nội dung bị chặn..."` |
| JSON parse lỗi | Return `"⚠ Không đọc được phản hồi..."` |

---

## 6. Module Database — Lưu trữ SQLite

> **Namespace:** `MotionAR.Database`
> **Đường dẫn:** `Assets/Scripts/Database/`

---

### 6.1. SQLiteConnector.cs

> **Vai trò:** Singleton quản lý cơ sở dữ liệu SQLite cục bộ. Lưu tại `Application.persistentDataPath/database.db`.

#### `sealed class SQLiteConnector` (Pure C# — không phải MonoBehaviour)

**Pattern:** Lazy Singleton (`Lazy<T>` — thread-safe)

##### ORM Records

**Bảng `Users`:**

| Column | Kiểu C# | Attribute | Mô tả |
|---|---|---|---|
| `UserId` | `string` | `[PrimaryKey]` | GUID |
| `Username` | `string` | `[Unique]` | Tên đăng nhập |
| `FullName` | `string` | — | Họ tên |
| `CreatedAt` | `DateTime` | — | Thời điểm tạo (UTC) |

**Bảng `TrainingSessions`:**

| Column | Kiểu C# | Attribute | Mô tả |
|---|---|---|---|
| `SessionId` | `string` | `[PrimaryKey]` | GUID |
| `UserId` | `string` | `[Indexed]` | FK → Users |
| `LessonId` | `string` | — | ID bài học |
| `TotalTimeSeconds` | `float` | — | Thời gian (giây) |
| `MistakeCount` | `int` | — | Số lỗi |
| `Score` | `float` | — | Điểm (0–100) |
| `CompletedAt` | `DateTime` | — | Thời điểm hoàn thành (UTC) |

##### Public Methods (tất cả async UniTask)

| Method | Mô tả |
|---|---|
| `InitializeAsync(CancellationToken)` | Tạo DB file + tables (idempotent) |
| `GetOrCreateUserAsync(username, fullName, ct)` | Tìm hoặc tạo user mới |
| `InsertSessionAsync(userId, lessonId, totalTime, mistakes, score, ct)` | Lưu kết quả phiên học |
| `GetSessionHistoryAsync(userId, ct)` | Lấy lịch sử (mới nhất trước) |

---

## 7. Design Patterns tổng hợp

| Pattern | Nơi sử dụng | Mục đích |
|---|---|---|
| **Producer-Consumer** | `HandTrackingReceiver` | Tách biệt thread MediaPipe ↔ Unity Main Thread |
| **Singleton (MonoBehaviour)** | `SessionManager`, `GeminiApiConnector` | Quản lý state global + DontDestroyOnLoad |
| **Singleton (Lazy\<T\>)** | `SQLiteConnector` | Thread-safe singleton cho pure C# class |
| **State Machine + Hysteresis** | `PinchGrabber`, `GestureDetector` | Chống rung giật (jitter) khi nhận diện Pinch |
| **Observer (C# Events)** | Tất cả module | Loose coupling giữa các component |
| **Strategy / Provider** | `IHandTrackingProvider` | Cho phép swap plugin MediaPipe |
| **Self-Registering Registry** | `SnapZone._allZones` | Tự đăng ký/hủy, không cần FindObjectsOfType |
| **Builder** | `ContextBuilder` | Sinh JSON payload phức tạp |
| **Repository** | `SQLiteConnector` | Trừu tượng hóa CRUD database |
| **Null Object / Error Absorption** | `GeminiApiConnector` | Không throw — trả message lỗi thân thiện |
| **Fire-and-Forget Async** | `SessionManager.SaveSessionToDatabase` | Lưu DB không block UI |
| **Graceful Degradation** | `GestureDetector` | GPU → CPU fallback; null model = chỉ Pinch |
| **Immutable Value Object** | `HandData` (readonly struct) | Thread-safe snapshot, zero-allocation |
| **Data-Driven Config** | `LessonData` (ScriptableObject) | Không hard-code giáo án |
| **Configuration Hierarchy** | `ConfigLoader` | Env var → File → Defaults |

---

## 8. Thuật toán lõi

### 8.1. Ánh xạ Raycasting (2D → 3D)

```
Landmark[8] (Index Finger Tip)
    ↓ Normalize → Screen Space (pixel)
    ↓ Camera.ScreenPointToRay()
    ↓ Ray intersects Virtual Touch Plane (Z = _virtualPlaneZ)
    ↓ = Point3D (Vị trí tay trong không gian 3D)
    ↓ Lerp smoothing (speed = 15)
    ↓ = cursorVisual.position
```

### 8.2. Pinch Detection với Hysteresis

```
d = ||Landmark[4] - Landmark[8]||    (Euclidean distance)

τ_start = 0.04    (ngưỡng gắp)
τ_end   = 0.06    (ngưỡng nhả)

if State == RELEASED && d < τ_start  →  State = GRABBED
if State == GRABBED  && d > τ_end    →  State = RELEASED
else                                  →  Giữ nguyên (Dead Zone)
```

### 8.3. ONNX Sliding Window Inference

```
Input: 21 landmarks × 3 axes = 63 features/frame

Buffer: List<float[]> sliding_window  (max 15 frames, FIFO)

Mỗi frame:
  1. Flatten landmarks → float[63]
  2. Push vào sliding_window, nếu > 15 thì xóa đầu (FIFO)
  3. Nếu đủ 15 frames VÀ cooldown == 0:
     a. Flatten thành float[15 × 63 = 945]
     b. Tạo TensorFloat(shape: [1, 15, 63], data: float[945])
     c. Worker.Schedule(tensor)
     d. Output = PeekOutput("output")
     e. predicted_class = argmax(output)
     f. Nếu max_confidence > 0.8:
        → Fire OnDynamicGestureDetected
        → Cooldown = 1.0 giây
```

### 8.4. Snap Validation

```
Input: GameObject part, tại vị trí P_release

Với mỗi SnapZone Z trong _allZones:
  if ||P_release - Z.snapPoint|| ≤ Z.snapRadius:
    if part.PartID == Z.expectedPartID:
      → SNAP SUCCESS (hút vào, khóa kinematic)
    else:
      → SNAP ERROR (flash đỏ, ghi lỗi)
      
Nếu không gần SnapZone nào → Linh kiện rơi tự do
```

### 8.5. Hiệu chuẩn chiều sâu Z (Dynamic Z-Axis Calibration)

```
Bước 1: Bounding Box
  → Người dùng xòe tay vừa khung hiển thị
  → Hệ thống tính tỷ lệ tương đối kích thước tay

Bước 2: Push-Pull Gesture
  → Bóp hai ngón + đẩy tới/kéo lui
  → Virtual Touch Plane trượt theo trục Z
  → Nhả tay = Khóa mặt phẳng
```

---

> **Tài liệu này được tạo tự động từ phân tích mã nguồn dự án Motion AR.**
> **Phiên bản:** V3.1 | **Ngày cập nhật:** 29/05/2026
]]>
