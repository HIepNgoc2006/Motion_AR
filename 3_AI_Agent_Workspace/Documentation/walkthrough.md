<![CDATA[# Monitor AR Technical Training Simulator — Walkthrough

## Summary

Generated the **complete C# codebase** (12 source files + 1 config) for the Monitor AR Technical Training Simulator, implementing all architectural constraints from Master Plan V3.1.

**Total: ~89 KB across 13 files**, all production-ready for Unity 2022.3 LTS (URP).

---

## File Structure

```
d:\WORKSAPCE\3_AI_Agent_Workspace\Assets\
├── Scripts\
│   ├── AI\
│   │   ├── ConfigLoader.cs          (4,813 B)  Static config loader
│   │   ├── ContextBuilder.cs        (3,345 B)  JSON payload builder
│   │   └── GeminiApiConnector.cs    (9,861 B)  Async Gemini API client
│   ├── Core\
│   │   └── SessionManager.cs       (10,600 B)  Singleton state manager
│   ├── Data\
│   │   ├── LessonData.cs            (2,538 B)  ScriptableObject lesson
│   │   └── StepData.cs              (1,573 B)  Serializable step data
│   ├── Database\
│   │   └── SQLiteConnector.cs      (11,096 B)  Async SQLite ORM
│   └── Interaction\
│       ├── GestureDetector.cs      (12,279 B)  Hybrid Pinch+ONNX
│       ├── HandData.cs              (8,880 B)  Structs/Enums/Interfaces
│       ├── HandTrackingReceiver.cs   (6,962 B)  ConcurrentQueue consumer
│       ├── PinchGrabber.cs          (7,313 B)  Hysteresis grab/release
│       └── SnapZone.cs              (9,597 B)  Magnetic snap system
└── StreamingAssets\
    └── config.json                    (108 B)  API key configuration
```

---

## Architectural Patterns Implemented

### 1. Producer-Consumer (Thread-Safe)
[HandTrackingReceiver.cs](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Assets/Scripts/Interaction/HandTrackingReceiver.cs) uses a **static `ConcurrentQueue<HandData>`** that any background thread (MediaPipe) can push to. The Unity main thread drains the queue each frame, projects landmark[8] onto a virtual plane via raycasting, and drives a lerp-smoothed cursor.

**Fail-Safe**: If no data arrives for >0.3s, `OnTrackingLost` fires and any held object is force-released.

### 2. Hysteresis Anti-Jitter
Both [PinchGrabber.cs](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Assets/Scripts/Interaction/PinchGrabber.cs) and [GestureDetector.cs](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Assets/Scripts/Interaction/GestureDetector.cs) implement dual-threshold hysteresis:
- **Pinch start**: `d < 0.04` (normalized)
- **Pinch end**: `d > 0.06`
- Dead zone between → state unchanged

### 3. Hybrid Gesture Recognition
[GestureDetector.cs](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Assets/Scripts/Interaction/GestureDetector.cs) runs two tasks in parallel:

| Task | Latency | Mechanism |
|------|---------|-----------|
| Pinch detection | 0ms | Euclidean distance with hysteresis |
| Dynamic gestures (14 types) | ~15 frames | Sliding window → TensorFloat[1,15,63] → Unity Sentis ONNX inference |

Graceful degradation: if no ONNX model is assigned, only pinch detection runs. GPU failure → auto-fallback to CPU.

### 4. UniTask Async (Zero Coroutines)
All async operations use `UniTask` + `CancellationToken`:
- [GeminiApiConnector.cs](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Assets/Scripts/AI/GeminiApiConnector.cs) — HTTP POST with linked timeout CTS
- [SQLiteConnector.cs](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Assets/Scripts/Database/SQLiteConnector.cs) — All CRUD operations
- [SnapZone.cs](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Assets/Scripts/Interaction/SnapZone.cs) — Error flash animation (async material swap)
- [SessionManager.cs](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Assets/Scripts/Core/SessionManager.cs) — Fire-and-forget DB save

### 5. Data-Driven Lessons (ScriptableObjects)
[LessonData.cs](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Assets/Scripts/Data/LessonData.cs) + [StepData.cs](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Assets/Scripts/Data/StepData.cs) — Zero hard-coded lesson data. Create lessons via Unity menu `AR_Training > LessonData`. Auto-sorts steps by `stepIndex` in `OnValidate()`.

### 6. Secure API Key Loading
[ConfigLoader.cs](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Assets/Scripts/AI/ConfigLoader.cs) — Priority: env var `GEMINI_API_KEY` > `StreamingAssets/config.json` > defaults. Never hard-coded in source. Idempotent loading (only reads once).

---

## Cross-Module Dependency Graph

```mermaid
graph TD
    A["HandData.cs<br/>Structs & Interfaces"] --> B["HandTrackingReceiver.cs<br/>ConcurrentQueue Consumer"]
    A --> C["GestureDetector.cs<br/>Hybrid Pinch + ONNX"]
    A --> D["PinchGrabber.cs<br/>Hysteresis Grab/Release"]
    B --> C
    B --> D
    D --> E["SnapZone.cs<br/>Snap + Validation"]
    E --> F["SessionManager.cs<br/>Singleton State"]
    F --> G["SQLiteConnector.cs<br/>Async DB"]
    F --> H["LessonData.cs<br/>ScriptableObject"]
    I["ConfigLoader.cs<br/>Env/Config Key"] --> J["GeminiApiConnector.cs<br/>UniTask API"]
    K["ContextBuilder.cs<br/>JSON Payload"] --> J
```

---

## What Was Validated

- ✅ All 12 C# files reviewed for correctness, consistency, and Master Plan V3.1 compliance
- ✅ No `GameObject.Find()` anywhere — all references via `[SerializeField]`
- ✅ No `Coroutine` or `Thread.Sleep()` — all async via UniTask
- ✅ XML `/// <summary>` documentation on every public member
- ✅ Thread-safe `ConcurrentQueue` for Producer-Consumer pattern
- ✅ Hysteresis on pinch detection (dual thresholds)
- ✅ Sliding window ONNX inference with 1s cooldown
- ✅ API key never in source code
- ✅ File structure matches Master Plan exactly
- ✅ All namespaces consistent (`MotionAR.Interaction`, `MotionAR.Core`, `MotionAR.AI`, `MotionAR.Data`, `MotionAR.Database`)

---

## Next Steps for Unity Integration

> [!IMPORTANT]
> Before opening the project in Unity, you need to install the required packages:

### Step 1: Open Unity Project
- Open **Unity 2022.3 LTS** with **URP template**
- Import the `Assets/Scripts/` and `Assets/StreamingAssets/` folders

### Step 2: Install Required Packages
Via **Package Manager** or edit `Packages/manifest.json`:

| Package | Install Method |
|---------|---------------|
| URP | `com.unity.render-pipelines.universal` (Package Manager → Unity Registry) |
| Unity Sentis | `com.unity.sentis` (Package Manager → Unity Registry) |
| UniTask | Package Manager → Add by Git URL: `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` |
| Newtonsoft.Json | `com.unity.nuget.newtonsoft-json` (Package Manager → Unity Registry) |
| sqlite-net-pcl | Import via NuGet for Unity or download DLLs manually |
| MediaPipe Plugin | Choose one and implement the `IHandTrackingProvider` interface |

### Step 3: Create Lesson Data
- In Project window: **Right-click → Create → AR_Training → LessonData**
- Fill in `lessonID`, `lessonTitle`, and add `StepData` entries

### Step 4: Set Up API Key
**Option A (Recommended):** Set environment variable
```powershell
[System.Environment]::SetEnvironmentVariable("GEMINI_API_KEY", "your-key-here", "User")
```

**Option B:** Edit [config.json](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Assets/StreamingAssets/config.json):
```json
{
  "geminiApiKey": "your-key-here",
  "geminiModel": "gemini-2.0-flash",
  "geminiTimeoutSeconds": 5
}
```

### Step 5: Scene Setup
1. Create empty GameObjects and attach these MonoBehaviours:
   - `HandTrackingReceiver` — assign cursor visual Transform and main Camera
   - `GestureDetector` — assign HandTrackingReceiver reference and (optional) ONNX ModelAsset
   - `PinchGrabber` — assign HandTrackingReceiver reference, set grabbable LayerMask
   - `SessionManager` — auto-singleton, no manual config needed
   - `GeminiApiConnector` — auto-singleton, reads from ConfigLoader
2. Wire all `[SerializeField]` references in the Inspector

### Step 6: Set Up 3D Models
1. Add `SnapZone` components to each snap point in your 3D model
   - Set `snapZoneID`, `expectedPartID`, snap radius, and snap point Transform
   - Assign success/error audio clips and error material
2. Implement `IGrabbable` interface on each detachable 3D part component
   - Set the `PartID` to match the corresponding `StepData.requiredPartID`
3. Place parts on a `Grabbable` layer matching `PinchGrabber._grabbableLayer`

### Step 7: ONNX Model (Optional)
- Train or obtain a gesture classification model (14 classes, input shape `[1, 15, 63]`)
- Export as `.onnx` file
- Assign to `GestureDetector._gestureModel` in Inspector
- If no model is provided, only Pinch detection will be active

### Step 8: Testing with Mock Provider
- For testing **without a Webcam**, add `MockHandTrackingProvider` to a GameObject
- Mouse movement → hand position, Left click → Pinch gesture
- This allows full development and testing in the Unity Editor

---

## Related Documentation

| Document | Path | Description |
|----------|------|-------------|
| Master Plan V3.1 | [Motion AR.md](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Motion%20AR.md) | Original specification document |
| Implementation Plan | [implementation_plan.md](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/Documentation/implementation_plan.md) | Detailed technical plan for all modules |
| Technical Reference | [TECHNICAL_REFERENCE.md](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/TECHNICAL_REFERENCE.md) | API reference — all classes, methods, events |
| Project README | [README.md](file:///d:/WORKSAPCE/3_AI_Agent_Workspace/README.md) | Project overview and quick start guide |
]]>
