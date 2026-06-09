# TÀI LIỆU ĐẶC TẢ VÀ QUY HOẠCH HỆ THỐNG MASTER PLAN (PHIÊN BẢN V3.1 - BẢN HOÀN CHỈNH)
## DỰ ÁN: GIẢ LẬP ĐÀO TẠO KỸ THUẬT MONITOR AR (MONITOR AR TECHNICAL TRAINING SIMULATOR)

---

## MỤC LỤC
1. CHƯƠNG I: TỔNG QUAN DỰ ÁN & TƯ DUY LOGIC CỐT LÕI
2. CHƯƠNG II: ĐẶC TẢ YÊU CẦU HỆ THỐNG (SRS)
3. CHƯƠNG III: KIẾN TRÚC HỆ THỐNG VÀ XỬ LÝ ĐA LUỒNG (PRODUCER - CONSUMER)
4. CHƯƠNG IV: THIẾT KẾ CƠ SỞ DỮ LIỆU CỤC BỘ VÀ KIẾN TRÚC DATA-DRIVEN
5. CHƯƠNG V: GIẢ LẬP TOÁN HỌC KHÔNG GIAN VÀ CÁC THUẬT TOÁN LÕI 
6. CHƯƠNG VI: HÀNH TRÌNH NGƯỜI DÙNG VÀ KỊCH BẢN CHUẨN AGILE (USER STORIES)
7. CHƯƠNG VII: ĐẶC TẢ THIẾT KẾ GIAO DIỆN HOLOGRAPHIC HUD (UI/UX)
8. CHƯƠNG VIII: TIÊU CHUẨN MÔI TRƯỜNG PHÁT TRIỂN VÀ QUẢN LÝ MÃ NGUỒN (GIT)
9. CHƯƠNG IX: DANH MỤC CÔNG NGHỆ VÀ PHẦN MỀM ĐƯỢC PHÊ DUYỆT
10. CHƯƠNG X: CHỈ THỊ TỐI CAO DÀNH CHO AI CODING ASSISTANT (MASTER PROMPT)

---

## 1. CHƯƠNG I: TỔNG QUAN DỰ ÁN & TƯ DUY LOGIC CỐT LÕI

### 1.1. Bối cảnh và Mục tiêu sản phẩm
Hệ thống **Monitor AR Technical Training Simulator** giải quyết rào cản tài chính trong đào tạo bằng cách "bình dân hóa" công nghệ tương tác 3D. Ứng dụng tận dụng phần cứng Laptop/PC tầm trung và 01 Webcam thông thường để biến màn hình máy tính thành "tấm gương ma thuật". Người học có thể tương tác, bóc tách các mô hình kỹ thuật (động cơ, bo mạch) lơ lửng trước mặt bằng cử chỉ tay thật và hỏi đáp với AI, mang lại cảm giác điều khiển viễn tưởng như nhân vật Iron Man.

### 1.2. Tư duy thiết kế hệ thống (Core Mindset)
1. **Hardware Agnostic (Tối ưu phần cứng):** Không yêu cầu thiết bị ngoại vi đắt tiền. Mọi tương tác không gian được giải quyết qua thuật toán Computer Vision trên camera 2D tiêu chuẩn.
2. **Thread-Safe & Real-time Performance (An toàn đa luồng):** Tốc độ phản hồi phải mượt mà. Tuyệt đối tuân thủ mô hình **Producer - Consumer** khi giao tiếp giữa hệ thống nhận diện (chạy ngầm) và Main Thread của Unity để triệt tiêu lỗi `SynchronizationContext` và tụt FPS.
3. **Modular & Offline-First (Ưu tiên ngoại tuyến):** Xử lý điều khiển vật lý (gắp, thả, menu) hoàn toàn ở máy cục bộ với độ trễ 0ms. Các tác vụ nặng (như Trợ giảng AI) mới gọi API đám mây bất đồng bộ.
4. **Fail-Safe UX (Tương tác chống nhiễu):** Hệ thống sở hữu bộ lọc làm mượt (Smoothing), nhận diện trễ (Hysteresis) và tự động "giải phóng linh kiện" nếu mất tracking do thiếu sáng hoặc bị che khuất.

---

## 2. CHƯƠNG II: ĐẶC TẢ YÊU CẦU HỆ THỐNG (SRS)

### 2.1. Yêu cầu chức năng (Functional Requirements)

#### 2.1.1. Module Tương tác Không gian (Spatial Interaction)
- **FR 1.1. Khởi tạo luồng Video:** Thu tín hiệu từ Webcam (tối thiểu 720p@30fps), render làm Background Layer. Nếu độ sáng môi trường quá thấp, hệ thống phải hiển thị cảnh báo.
- **FR 1.2. Trích xuất khớp xương tay:** Nhận diện 21 điểm Landmarks của bàn tay. Xử lý an toàn khi mất tracking đột ngột.
- **FR 1.3. Cử chỉ hình học:** Nhận diện Bóp/Chụm ngón tay (Pinch) để gắp linh kiện, Xòe tay (Release) để thả linh kiện.
- **FR 1.4. Hệ thống bắt dính (Snap System):** Khi đưa linh kiện vào bán kính dung sai $R_{snap}$ của vị trí đích, hệ thống hút chặt và khóa tọa độ (isKinematic = true) vào khớp nối kỹ thuật.

#### 2.1.2. Module Trợ lý và Điều khiển (Menu & AI Assistant)
- **FR 2.1. Menu Cổ tay Không gian (Wrist Menu):** Vuốt tay sang trái/phải (Swipe Left/Right) để lật các trang giáo án kỹ thuật.
- **FR 2.2. Trợ lý đàm thoại GenAI Tutor:** Lắng nghe câu hỏi tự do, tự đóng gói trạng thái 3D hiện tại thành JSON gửi lên LLM (Gemini API), trả về Text và Audio.
- **FR 2.3. Cử chỉ Kích hoạt Mic (Gesture-triggered Mic):** Thực hiện cử chỉ Chạm ngón tay (Tap) hoặc Nắm đấm (Fist) để mở Micro. Nhả tay ra để kết thúc ghi âm và gửi lệnh. Tuyệt đối không dùng cơ chế luôn lắng nghe (Always Listening).

#### 2.1.3. Module Nghiệp vụ Đào tạo (Training Management)
- **FR 3.1. Hướng dẫn tuần tự:** Đọc dữ liệu từ file để hiển thị hướng dẫn và highlight linh kiện cần lắp tương ứng với từng bước.
- **FR 3.2. Cảnh báo lỗi:** Nếu lắp sai linh kiện hoặc sai vị trí, hệ thống đổi màu mô hình sang đỏ và phát âm thanh cảnh báo lỗi.

### 2.2. Yêu cầu phi chức năng (Non-Functional Requirements)
- **NFR 1. Hiệu năng đồ họa:** Duy trì $\ge 30\text{ FPS}$ trên máy tính phổ thông (Core i3/i5, GPU tích hợp).
- **NFR 2. Độ trễ tracking:** Tương tác tay phải phản hồi $< 50\text{ ms}$.
- **NFR 3. An toàn mạng (API Timeout):** Gemini API Timeout ở $5\text{ giây}$. Nếu quá thời gian, hiển thị lỗi kết nối thay vì làm treo ứng dụng.
- **NFR 4. Đóng gói:** Tệp thực thi độc lập `.exe` cho Windows, không yêu cầu cài Server SQL/Redis/NodeJS ở máy trạm.

---

## 3. CHƯƠNG III: KIẾN TRÚC HỆ THỐNG VÀ XỬ LÝ ĐA LUỒNG (PRODUCER-CONSUMER)

Để giải quyết tận gốc lỗi nghẽn cổ chai và vi phạm `SynchronizationContext` của Unity Engine (Unity không cho phép các luồng ngầm truy cập vào GameObject), hệ thống áp dụng kiến trúc **Thread-Safe Queue** kết hợp với **Sliding Window Buffer**.

### Sơ đồ Luồng Dữ liệu Lai (Hybrid Core Data Flow)

```text
[ BACKGROUND THREAD ]             [ THREAD-SAFE QUEUE & BUFFER ]               [ MAIN THREAD UNITY ]
(MediaPipe C# Plugin)                                                          (Unity Update Loop)

 1. Đọc Frame từ Webcam.           Hàng đợi ConcurrentQueue             1. Lấy Landmark gán cho Cursor 3D (0ms).
 2. Suy luận 21 Landmarks.   ==>   chứa struct HandData           ==>   2. Kiểm tra Pinch/Release bằng Toán học.
 3. Gửi vào Queue.                 -------------------------            3. Đẩy HandData vào Sliding Window Buffer (Cửa sổ trượt).
                                   (Sliding Window Buffer)              4. Khi Buffer đủ 15 frames, gom thành Tensor [1, 15, 63].
                                   Lưu trữ 15 frames gần nhất           5. Gửi Tensor vào Unity Sentis chạy suy luận ONNX.
                                                                        6. Kích hoạt sự kiện UI (Swipe, Tap, Fist).

```

* **Cơ chế Fail-Safe (Chống kẹt):** Nếu Main Thread Unity kiểm tra thấy Queue rỗng trong **> 0.3 giây** (Người dùng rụt tay lại hoặc tay bị che khuất), hệ thống tự động kích hoạt hàm `ForceReleaseObject()` để chống lỗi linh kiện dính lơ lửng trên không gian, đồng thời xóa sạch Sliding Window Buffer.

---

## 4. CHƯƠNG IV: THIẾT KẾ CƠ SỞ DỮ LIỆU CỤC BỘ VÀ KIẾN TRÚC DATA-DRIVEN

### 4.1. Kiến trúc Cấu hình Dữ liệu tĩnh (ScriptableObjects)

Tuyệt đối không hard-code (viết cứng) thứ tự lắp ráp. Toàn bộ kịch bản giáo án được cấu hình bằng `ScriptableObject`.

```csharp
[CreateAssetMenu(fileName = "NewLesson", menuName = "AR_Training/LessonData")]
public class LessonData : ScriptableObject {
    public string lessonID;
    public List<StepData> assemblySteps;
}

[System.Serializable]
public class StepData {
    public int stepIndex;
    public string requiredPartID;       
    public string snapZoneID;          
    public string instructionText;     
}

```

### 4.2. Lược đồ Cơ sở dữ liệu Cục bộ (SQLite Schema)

Sử dụng thư viện `sqlite-net-pcl` sinh ra tệp `database.db` tại thư mục cài đặt nội bộ.

* **Bảng `Users`:** `user_id` (PK, TEXT/GUID), `username` (UNIQUE), `full_name`, `created_at`.
* **Bảng `TrainingSessions`:** `session_id` (PK, TEXT/GUID), `user_id` (FK), `lesson_id`, `total_time_seconds`, `mistake_count`, `score`, `completed_at`.

### 4.3. Quản lý trạng thái trên RAM (In-Memory Session Manager)

Thay vì dùng Redis, trạng thái bài học lưu trên RAM thông qua C# Singleton `SessionManager`. Mỗi khi có thao tác lỗi, biến đếm tự tăng. Khi hoàn thành bài học, thực hiện đúng 1 lệnh `INSERT` luồng dữ liệu xuống SQLite để bảo vệ ổ cứng.

---

## 5. CHƯƠNG V: GIẢ LẬP TOÁN HỌC KHÔNG GIAN VÀ CÁC THUẬT TOÁN LÕI

### 5.1. Thuật toán Hiệu chuẩn chiều sâu qua UX (Dynamic Z-Axis Calibration)

Loại bỏ phương pháp đo đạc vật lý bằng công thức Pixel (do dễ sai lệch tiêu cự Webcam), thay bằng cơ chế hiệu chuẩn thị giác:

1. **Định cỡ tương đối (Bounding Box):** Màn hình hiển thị một khung (Box). Người dùng xòe tay vừa khít khung đó. Hệ thống lấy tỷ lệ tương đối.
2. **Tinh chỉnh thủ công (Push-Pull Gesture):** Người dùng bóp hai ngón tay và đẩy tới/kéo lui. Mặt phẳng `Virtual Touch Plane` sẽ trượt theo trục Z. Khi Cursor 3D chạm chuẩn vào linh kiện mong muốn, người dùng nhả tay để Khóa (Lock) mặt phẳng tương tác lại.

### 5.2. Thuật toán Ánh xạ điểm trỏ không gian (Raycasting)

Tọa độ ngón trỏ (Landmark 8) trên màn hình ($P_{screen}$) được ánh xạ bằng hàm API `Camera.main.ScreenPointToRay()`. Tia này giao cắt với mặt phẳng `Virtual Touch Plane` tại $P_{3D}$. Vị trí này chính là tọa độ 3D của tay.

### 5.3. Thuật toán Nhận diện cử chỉ Pinch có độ trễ (Hysteresis)

Chống hiện tượng rung giật (Flickering/Jitter) do tay người dùng không ổn định:

* Khoảng cách $d$ = Euclidean(Ngón cái, Ngón trỏ).
* Ngưỡng kích hoạt gắp $\tau_{start} = 0.04$ / Ngưỡng nhả $\tau_{end} = 0.06$.
* Máy trạng thái Logic:
* Nếu `State == RELEASED` & $d < \tau_{start}$ => Chuyển sang `GRABBED`.
* Nếu `State == GRABBED` & $d > \tau_{end}$ => Chuyển sang `RELEASED`.
* Khoảng giữa: Giữ nguyên trạng thái cũ.



### 5.4. Thuật toán Suy luận Cử chỉ Động bằng Deep Learning (ONNX Inference)

Hệ thống sử dụng mô hình Mạng nơ-ron (Transformer/LSTM) để nhận diện 14 cử chỉ động phức tạp (Swipe, Tap, Fist...).

* **Cấu trúc dữ liệu đầu vào (Input Tensor):** Bàn tay có 21 landmarks, mỗi landmark có 3 trục (X,Y,Z) => `63 features`. Mô hình yêu cầu chuỗi thời gian `15 frames`. Suy ra shape của Tensor đầu vào là `[1, 15, 63]`.
* **Cửa sổ trượt (Sliding Window):** Main Thread duy trì một `List<float[]>` độ dài tối đa 15. Mỗi frame mới đến, xóa frame cũ nhất (FIFO).
* **Thực thi (Inference):** Sử dụng thư viện `Unity.Sentis`. Khi nhận diện được một cử chỉ có độ tự tin (Confidence) > 80%, hệ thống sẽ khóa (Cooldown) bộ nhận diện trong 1 giây để tránh kích hoạt liên tục (Spam triggers).

---

## 6. CHƯƠNG VI: HÀNH TRÌNH NGƯỜI DÙNG VÀ KỊCH BẢN CHUẨN AGILE (USER STORIES)

### 6.1. Bản đồ Hành trình Người học

1. **Khởi động & Hiệu chuẩn:** Đăng nhập. Xòe tay vừa khung Bounding Box và kéo/đẩy mặt phẳng Z để tạo độ sâu thao tác thoải mái nhất.
2. **Chọn Bài học:** Vuốt tay (Swipe) để mở Panel Menu. Rê ngón tay vào nút bài học trong 0.3s (Hover-to-click) để bắt đầu.
3. **Thực hành:** Gắp linh kiện di chuyển vào mô hình 3D trong suốt. Hút đúng vị trí (Snap) phát tiếng Click.
4. **Cứu trợ AI:** Thực hiện cử chỉ Tap, micro mở, hỏi: "Linh kiện này lắp vào đâu?". Gemini API trả lời và highlight vị trí đích.
5. **Hoàn thành:** Lưu kết quả và hiển thị bảng điểm.

### 6.2. Kịch bản người dùng (User Stories Backlog)

* **US 1.1:** Tôi muốn thao tác gắp thả (Pinch/Release) đầm chắc, không bị giật lùi vị trí.
* **US 1.2:** Tôi muốn linh kiện tự bắt dính (Snap) vào khớp khi đưa lại gần để thao tác dễ dàng trên màn hình phẳng 2D.
* **US 2.1:** Tôi muốn mở Menu cài đặt và lướt các lựa chọn bằng cử chỉ động (Vuốt/Swipe) thông qua hệ thống Deep Learning.
* **US 2.2:** Tôi muốn hỏi trợ lý AI bằng giọng nói nhưng AI phải hiểu tôi đang cầm linh kiện gì trên tay (Yêu cầu đóng gói Payload JSON chuẩn xác).

---

## 7. CHƯƠNG VII: ĐẶC TẢ THIẾT KẾ GIAO DIỆN HOLOGRAPHIC HUD (UI/UX)

### 7.1. Nguyên lý Thiết kế (Sci-Fi HUD)

* **Màu sắc:** Cyan (`#00FFFF`), Neon Green (Success), Neon Red (Error). Nền Panel đen khói trong suốt 40%.
* **Hiệu ứng đồ họa (URP):** Tích hợp Post-Processing Bloom để tạo độ chói sáng viền (Emissive Glow).

### 7.2. Giao diện động (Dynamic Canvas) để bảo vệ Workspace

* Panel Trái (Tiến độ) và Panel Phải (Thông tin chi tiết) mặc định **ẨN** ở mép màn hình.
* Người dùng **Vuốt (Swipe)** sang để kéo ra. Panel tự động **Auto-hide** sau 5 giây không tương tác.
* Mục đích: Dành 100% vùng không gian trung tâm (Center Zone) cho thao tác lắp ráp 3D.

### 7.3. Tiêu chuẩn Phản hồi Trải nghiệm (UX Feedback)

* **Tương tác Nút (Hover-to-click):** Rê tay vào nút. Một vòng Loading Ring chạy quanh ngón tay trong `0.3 giây`. Khi đầy, kích hoạt lệnh kèm tiếng Píp.
* **Tương tác Linh kiện:** Khi đưa tay gần, linh kiện phát sáng Outline. Khi Bóp (Pinch), linh kiện co nhẹ (Scale -5%) để tạo cảm giác đã cầm nắm thành công.

---

## 8. CHƯƠNG VIII: TIÊU CHUẨN MÔI TRƯỜNG PHÁT TRIỂN VÀ QUẢN LÝ MÃ NGUỒN (GIT)

### 8.1. Môi trường Engine

* Unity **2022.3.x LTS** (Tuyệt đối không dùng Unity 6 để tránh lỗi tương thích MediaPipe).
* **URP (Universal Render Pipeline)**.
* Backend: **IL2CPP**, Architecture: **x86_64** (Windows Desktop).

### 8.2. Danh mục Gói Thư viện Bắt buộc

1. `com.unity.render-pipelines.universal` & `postprocessing`.
2. `UniTask`: Xử lý Async/Await tối ưu thay thế hoàn toàn Coroutine.
3. `Newtonsoft.Json`: Phân tích dữ liệu JSON cho LLM và DB.
4. `sqlite-net-pcl` (Nuget): Database cục bộ.
5. Plugin MediaPipe Unity Hands.
6. `com.unity.sentis` (Cài qua Package Manager, **version 2.1.3**): Engine suy luận Mạng nơ-ron cục bộ (Local Neural Network Inference). Dùng để đọc file `gesture_model.onnx` và chạy hàm Predict trực tiếp trên RAM/GPU. **LƯU Ý:** Sentis 2.1+ sử dụng API `Tensor<float>` thay cho `TensorFloat` (deprecated), và `ReadbackAndClone()` thay cho `CompleteOperationsAndDownload()` (deprecated).

### 8.3. Quản lý Git Flow

* Branching: `main` (Release), `develop` (Tích hợp), `feature/spatial-interaction` (Gắp thả), `feature/ai-tutor` (Gemini API), `feature/database` (SQLite).
* Message chuẩn Convention: `feat: add concurrent queue for tracking data`, `fix: resolve object flickering`.

---

## 9. CHƯƠNG IX: DANH MỤC CÔNG NGHỆ (TECH STACK)

| Phân hệ hệ thống | Tên Công nghệ | Vai trò & Giải pháp |
| --- | --- | --- |
| **Logic & Đồ họa** | Unity 2022.3 LTS (URP, C#) | Engine chính xử lý tương tác vật lý. |
| **Thị giác (CV)** | MediaPipe Hands Unity | Tracking ngón tay, chạy trên luồng ngầm độc lập. |
| **Đồng bộ luồng** | `System.Collections.Concurrent` | Dùng Queue Thread-safe kết nối CV Thread và Main Thread. |
| **Nhận diện Cử chỉ** | Unity Sentis 2.1.3 (.ONNX) | Load mô hình Transformer/LSTM, API: `Tensor<float>`, `ReadbackAndClone()`. |
| **Bất đồng bộ** | UniTask | Quản lý vòng đời gọi API, không gây nghẽn màn hình. |
| **AI Đám mây** | Gemini 3.5 Flash API | Nhận Context JSON, phản hồi tư vấn lắp ráp. |
| **Cơ sở dữ liệu** | SQLite (`sqlite-net-pcl`) | Lưu lịch sử cục bộ tại máy tính (Không cần server). |

---

## 10. CHƯƠNG X: CHỈ THỊ TỐI CAO DÀNH CHO AI CODING ASSISTANT (MASTER PROMPT)

VAI TRÒ CỦA AI AGENT:
Bạn là Chuyên gia Lập trình Unity C# cấp bậc Senior Architect. Nhiệm vụ của bạn là sinh mã nguồn C# để hiện thực hóa "Monitor AR Technical Training Simulator" theo sát Master Plan (V3.1).

RÀNG BUỘC KỸ THUẬT NGHIÊM NGẶT KHI SINH CODE (CRITICAL CONSTRAINTS):
1. KIẾN TRÚC PRODUCER-CONSUMER (BẮT BUỘC): Tuyệt đối không được gán Transform.position trực tiếp từ luồng MediaPipe Callback. Bạn phải sinh mã sử dụng 'ConcurrentQueue' để chứa tọa độ. Main Thread Unity (Update) sẽ Dequeue và thực thi lerp nội suy. Bắt buộc viết thêm logic 'Timeout (0.3s)' nếu Queue rỗng sẽ thả rơi linh kiện.
2. XỬ LÝ BẤT ĐỒNG BỘ: Mọi kết nối API (Gemini, DB) phải dùng 'UniTask' kèm 'CancellationToken'. KHÔNG dùng Coroutine hay Thread.Sleep(). 
3. LOOSE COUPLING & SCRIPTABLEOBJECTS: Không dùng 'GameObject.Find()'. Cấu trúc dữ liệu bài học bắt buộc đọc từ ScriptableObject 'LessonData'.
4. CODE SẴN SÀNG BIÊN DỊCH: Viết full Code (100%), không viết comment ẩn logic (//... implement here). Gắn tag XML Documentation '/// <summary>' cho hàm public.
5. KIẾN TRÚC NHẬN DIỆN LAI (HYBRID GESTURE RECOGNITION): Bạn phải thiết kế Class 'GestureDetector.cs' thực hiện song song 2 nhiệm vụ:
   - Nhiệm vụ 1: Tính khoảng cách Euclidean giữa Ngón cái và Ngón trỏ ngay lập tức để thực thi 'PinchGrabber' (độ trễ 0ms).
   - Nhiệm vụ 2: Gom tọa độ (x,y,z) của 21 landmarks vào Buffer lưu 15 frames (Sliding Window). Khi đủ 15 frames, biến đổi thành `Tensor<float>` có shape [1, 15, 63] và nạp vào mô hình ONNX thông qua `Unity.Sentis.Worker`. **Bắt buộc dùng API Sentis 2.1+:** `new Tensor<float>(shape, data)` thay vì `new TensorFloat(...)`, và `ReadbackAndClone()` thay vì `CompleteOperationsAndDownload()`. Áp dụng Cooldown 1 giây sau mỗi lần nhận diện thành công.
6. BẢO MẬT API KEY (GEMINI): Tuyệt đối không hard-code (viết cứng) API Key vào mã nguồn C#. Hệ thống phải đọc API Key từ biến môi trường (Environment Variable) 'GEMINI_API_KEY', hoặc từ một file text ẩn (như '.env' hoặc 'config.json') nằm ngoài thư mục Assets.

CẤU TRÚC THƯ MỤC CẦN SINH RA:
/Assets
  /Scripts
    /Core        <-- SessionManager.cs (In-memory singleton)
    /Interaction <-- HandTrackingReceiver.cs (Queue logic), PinchGrabber.cs, SnapZone.cs, GestureDetector.cs (Sentis logic)
    /AI          <-- GeminiApiConnector.cs, ContextBuilder.cs (Sinh chuỗi JSON), ConfigLoader.cs
    /Data        <-- LessonData.cs, StepData.cs (ScriptableObjects)
    /Database    <-- SQLiteConnector.cs

---

## 11. PHỤ LỤC: TRẠNG THÁI DỰ ÁN (CẬP NHẬT 2026-05-30)

### Lỗi đã sửa
| # | Lỗi | File | Trạng thái |
|---|---|---|---|
| 1 | Sentis API incompatible (TensorFloat→Tensor\<float\>) | GestureDetector.cs | ✅ |
| 2 | SQLiteAsyncConnection không tồn tại | SQLiteConnector.cs | ✅ |
| 3 | Thiếu gesture_model.onnx.data | Models_ONNX/ | ✅ |
| 4 | Thiếu Softmax trên logits output | GestureDetector.cs | ✅ |
| 5 | MockProvider không enqueue vào ConcurrentQueue | HandData.cs | ✅ |
| 6 | PinchGrabber duplicate state machine | PinchGrabber.cs | ✅ |

### Việc cần làm tiếp
| Ưu tiên | Hành động |
|---|---|
| 🔴 P0 | Export StandardScaler params + load trong Unity |
| 🟡 P1 | Setup Scene cơ bản + test end-to-end |
| 🟡 P1 | Tích hợp MediaPipe Unity Plugin |
| 🟢 P2 | Thiết kế UI HUD Holographic |
| 🔵 P3 | Z-Axis Calibration + Login + Build .exe |