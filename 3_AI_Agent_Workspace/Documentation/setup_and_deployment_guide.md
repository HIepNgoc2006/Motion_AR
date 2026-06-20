# 🎯 Hướng dẫn Setup & Triển khai Motion AR trên Unity

Tài liệu này hướng dẫn chi tiết từng bước để thiết lập, chạy thử nghiệm và đóng gói (build) dự án **Motion AR** — hệ thống mô phỏng đào tạo kỹ thuật 3D tương tác bằng cử chỉ tay qua webcam và trợ lý AI Gemini.

---

## 💻 1. Yêu cầu Hệ thống & Phiên bản

Đảm bảo máy tính của bạn đáp ứng các cấu hình và phiên bản công cụ sau để tránh lỗi xung đột:

| Thành phần | Phiên bản yêu cầu | Ghi chú |
| :--- | :--- | :--- |
| **Unity Editor** | **2022.3.x LTS** (khuyên dùng 2022.3.20f1 trở lên) | **Không sử dụng Unity 6** để tránh lỗi Sentis/URP |
| **Render Pipeline** | **URP (Universal Render Pipeline)** | Sử dụng các Shader tương thích URP |
| **Python** | **3.9.x đến 3.11.x** (64-bit) | Đã được kiểm thử độ ổn định |
| **Hệ điều hành** | **Windows 10/11** | Chạy Scripting Backend: **IL2CPP** |
| **Thiết bị phụ trợ** | **01 Webcam** (720p @ 30fps) | Có micrô để hỏi đáp AI |

---

## 🛠️ 2. Hướng dẫn Thiết lập Môi trường

### Bước A: Clone mã nguồn dự án
Mở terminal (PowerShell hoặc Command Prompt) và clone dự án về máy:
```powershell
git clone https://github.com/HIepNgoc2006/motion-ar.git
cd motion-ar
```

### Bước B: Thiết lập môi trường Python & MediaPipe
Để sử dụng tính năng theo dõi cử chỉ tay qua camera thực tế, bạn cần cài đặt MediaPipe Server chạy dưới nền:

1. Tạo môi trường ảo Python (khuyên dùng để tránh xung đột thư viện):
   ```powershell
   python -m venv .venv
   .venv\Scripts\activate
   ```
2. Cài đặt các thư viện phụ thuộc:
   ```powershell
   pip install -r 1_AI_Model_Training/requirements.txt
   ```
   *(Các thư viện chính gồm: `opencv-python`, `mediapipe`, `numpy`, `protobuf`)*

### Bước C: Cấu hình Gemini API Key
Dự án sử dụng Gemini AI để hướng dẫn người học thời gian thực thông qua phân tích ngữ cảnh 3D. Bạn có hai cách cấu hình:

* **Cách 1: Đặt biến môi trường (Khuyên dùng)**
  Mở PowerShell và chạy lệnh sau (thay `your-api-key-here` bằng API key thật của bạn):
  ```powershell
  [System.Environment]::SetEnvironmentVariable("GEMINI_API_KEY", "your-api-key-here", "User")
  ```
  *Lưu ý: Sau khi set biến môi trường, hãy khởi động lại Unity Hub và Unity Editor để Unity nhận biến mới.*

* **Cách 2: Sử dụng file cấu hình (Không nên đẩy file này lên Git)**
  Chỉnh sửa hoặc tạo file `Assets/StreamingAssets/config.json` trong thư mục Unity với nội dung:
  ```json
  {
    "geminiApiKey": "your-api-key-here",
    "geminiModel": "gemini-2.0-flash",
    "geminiTimeoutSeconds": 5
  }
  ```

---

## ⚙️ 3. Các Bước Thiết lập Scene Unity (Tự động 1-Click)

Dự án đã tích hợp sẵn các công cụ Editor Script giúp thiết lập nhanh một Scene hoàn chỉnh mà không cần kéo thả thủ công:

1. **Mở dự án:**
   Mở Unity Hub → Chọn **Add** → Chọn thư mục `2_Unity_Project/Motion_AR/`. Đợi Unity import assets và compile xong.
2. **Khởi tạo cấu trúc Scene:**
   * Trong Unity Editor, tạo một Scene mới (hoặc mở Scene trống).
   * Trên thanh menu trên cùng, chọn: **Motion AR ➔ Setup Training Scene**.
   * Nhấn **Create Scene** ở hộp thoại hiện lên.
   * **Kết quả:** Unity sẽ tự động sinh toàn bộ Hierarchy cần thiết bao gồm:
     * Bộ điều khiển hệ thống (`HandTrackingSystem`, `PinchGrabSystem`, `SessionManager`, `GeminiConnector`, `TrainingFlowController`, `SceneBootstrapper`, `AppFlowManager`, `PerformanceMonitor`).
     * Con trỏ 3D có hiệu ứng phát sáng (`CursorVisual` & `CursorEffects`).
     * Linh kiện mẫu (`Part_A`, `Part_B`, `Part_C`) thuộc Layer `Grabbable`.
     * Các vùng lắp ráp tương ứng (`SnapZone_A`, `SnapZone_B`, `SnapZone_C`).
     * Hệ thống UI Canvas đầy đủ (Login, Selection, HUD, Score, History, Settings).
3. **Sinh bài học mẫu (ScriptableObjects):**
   * Trên thanh menu trên cùng, chọn: **Motion AR ➔ Create Sample Lessons**.
   * **Kết quả:** Unity sẽ tự tạo thư mục `Assets/Data/Lessons/` chứa 2 bài học mẫu bằng tiếng Việt:
     1. *Lắp ráp Máy bơm nước* (3 bước)
     2. *Bảo trì Động cơ điện* (4 bước)
4. **Gán giáo án vào Controller:**
   * Chọn GameObject **TrainingFlowController** trong Hierarchy.
   * Trong cửa sổ Inspector, gán file giáo án mẫu vừa tạo (ví dụ: `Lesson_PumpAssembly`) vào trường `Active Lesson`.
   * Gán layer `Grabbable` vào trường `Grabbable Layer Mask` trên component **PinchGrabber** (nếu chưa gán).
   * Nhập file model ONNX (`gesture_model.onnx`) từ thư mục dự án vào trường `Model Asset` của component **GestureDetector** để nhận diện cử chỉ swipe/tap/fist.

---

## 🧪 4. Chạy và Thử nghiệm Dự án

### Chế độ 1: Mock Mode (Dành cho nhà phát triển - Không cần webcam)
Hỗ trợ kiểm thử luồng game, UI, logic snap và lưu cơ sở dữ liệu nhanh chóng:

1. Mở Unity Editor và nhấn **Play**.
2. Nhập tên và đăng nhập tại màn hình đăng nhập (Dữ liệu sẽ tự tạo file SQLite `SessionDatabase.db` trong thư mục máy tính của bạn).
3. Chọn bài học và vào màn chơi.
4. **Cách điều khiển bằng chuột:**
   * Di chuột để di chuyển con trỏ 3D.
   * **Nhấn giữ chuột trái** để thực hiện cử chỉ chụm ngón tay (Pinch/Grab) để nhấc linh kiện (màu linh kiện đổi sang màu Cyan nhạt).
   * **Thả chuột trái** để thả linh kiện (Release) vào vùng SnapZone đích.
   * Kiểm tra log ở cửa sổ Console để xem log tiến trình học và cảnh báo lỗi.

### Chế độ 2: Real Hand Tracking Mode (Sử dụng Webcam + MediaPipe)
Để trải nghiệm tương tác không gian thực tế bằng tay:

1. **Khởi động MediaPipe Server (Python):**
   Chạy lệnh sau trong terminal (đã kích hoạt môi trường `.venv`):
   ```powershell
   python 1_AI_Model_Training/Scripts/mediapipe_server.py
   ```
   *Một cửa sổ camera sẽ hiện lên hiển thị hình ảnh webcam và lưới 21 điểm xương bàn tay màu xanh lá.*
2. **Khởi động Unity:**
   * Trong Unity Editor, nhấn **Play**.
   * Chuyển chế độ Input sang **MediaPipe** (trong menu cài đặt hoặc hot-key trong game).
   * Đưa bàn tay ra trước camera. Con trỏ 3D trong Unity sẽ di chuyển bám sát ngón trỏ của bạn.
   * **Cử chỉ Pinch:** Bóp ngón cái và ngón trỏ lại với nhau để gắp linh kiện.
   * **Cử chỉ Release:** Xòe bàn tay để thả linh kiện vào SnapZone.
   * **Cử chỉ Swipe:** Vuốt tay qua lại để chuyển trang.
   * **Cử chỉ Tap / Fist:** Chạm ngón tay hoặc nắm tay lại để mở Micro, nói câu hỏi (ví dụ: *"Linh kiện màu vàng lắp vào đâu?"*) để AI Gemini phản hồi và chiếu sáng (highlight) linh kiện đích.

### Chạy các Unit Tests tích hợp:
Dự án đi kèm 32 bài test tự động (EditMode unit tests) để kiểm tra tính đúng đắn của logic tính điểm, dữ liệu giáo án và định dạng gói tin bàn tay:
* Mở **Window ➔ General ➔ Test Runner** trong Unity.
* Chọn tab **EditMode**.
* Nhấp **Run All** để thực thi các test case. Tất cả các test case đều phải có màu xanh lục (pass) ✅.

---

## 📦 5. Hướng dẫn Đóng gói & Triển khai (Build EXE)

Khi dự án đã sẵn sàng, hãy đóng gói thành file chạy `.exe` độc lập cho Windows:

### Bước 1: Cấu hình Build Settings
1. Vào **File ➔ Build Settings...**
2. Thêm scene huấn luyện vào danh sách **Scenes In Build**.
3. Chọn nền tảng **Dedicated Server** hoặc **Windows, Mac, Linux** với Target Platform là **Windows**.
4. Chọn Scripting Backend là **IL2CPP** để có hiệu năng tối ưu nhất.
5. Đảm bảo Target Architecture chọn **x86_64**.

### Bước 2: Thiết lập Project Settings
1. Nhấn nút **Player Settings...** ở góc dưới bên trái của cửa sổ Build Settings.
2. Tại mục **Player ➔ Other Settings**:
   * Kiểm tra mục **Api Compatibility Level** đã chọn **.NET Standard 2.1** hoặc **.NET Framework**.
   * Đảm bảo tính năng **Allow 'unsafe' Code** được bật nếu thư viện SQLite yêu cầu.

### Bước 3: Đóng gói và chạy thử Standalone
1. Quay lại cửa sổ **Build Settings** và nhấn **Build**.
2. Tạo thư mục mới (ví dụ: `Build/`) và lưu game tại đây (ví dụ: `MotionAR.exe`).
3. Đợi Unity biên dịch mã C# sang C++ qua IL2CPP và tạo file exe.
4. **Quan trọng khi chạy exe:**
   * Thư mục `StreamingAssets` chứa file `config.json` sẽ tự động được sao chép vào thư mục `MotionAR_Data/StreamingAssets/` của bản build.
   * Hãy mở file `StreamingAssets/config.json` trong bản build và điền chính xác khóa API Gemini của bạn để hệ thống AI hoạt động độc lập.
   * Để chạy đầy đủ, bạn vẫn cần kích hoạt script `mediapipe_server.py` trên máy tính trước, sau đó khởi chạy file `MotionAR.exe` là có thể sử dụng bình thường.
