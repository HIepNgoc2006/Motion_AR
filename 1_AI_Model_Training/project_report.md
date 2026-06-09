# Báo cáo Phân tích Dự án: Dynamic Spatial Hand Gesture Recognition

## 1. Tổng quan cấu trúc project (Các thư mục, file chính)
Dự án được tổ chức rất rõ ràng, logic và phân tách tốt giữa dữ liệu, mô hình, kịch bản huấn luyện (notebook) và các kịch bản thực thi. 
Cấu trúc hiện tại bao gồm:

*   **`app.py`**: Mã nguồn của ứng dụng Streamlit dùng để trình bày (showcase) tổng quan dự án, so sánh các mô hình và hướng dẫn demo.
*   **`README.md`**: Tài liệu hướng dẫn chi tiết của dự án, mô tả bài toán, lộ trình 8 tuần và cách cài đặt.
*   **Thư mục `Scripts/`**: Chứa các kịch bản Python độc lập (pipeline):
    *   `1_collect_data.py`: Thu thập dữ liệu từ Webcam.
    *   `1b_extract_kinematics.py`: Trích xuất đặc trưng động học (vận tốc, gia tốc, góc ngón tay).
    *   `2_generate_synthetic_data.py`: Sinh dữ liệu tổng hợp giả lập.
    *   `3_convert_dhg_dataset.py`: Chuyển đổi dữ liệu DHG-14/28 thành định dạng chuẩn.
    *   `4_realtime_inference.py`: Chạy nhận diện cử chỉ theo thời gian thực.
    *   `5_os_controller.py`: Biến cử chỉ tay thành thao tác điều khiển hệ điều hành.
    *   `6_universal_dataset_builder.py`: Pipeline gộp nhiều dataset (DHG, FPHA, Briareo, Video...) thành file chuẩn.
    *   `7_export_onnx.py`: (**MỚI**) Xuất mô hình Transformer đã huấn luyện ra định dạng ONNX cho Unity Sentis.
    *   `run_training.py`: (**MỚI**) Script huấn luyện Transformer hoàn chỉnh, trích xuất từ notebook Tuần 7.
*   **Thư mục `Notebooks/`**: Các Jupyter Notebook dùng để huấn luyện và phân tích mô hình (`Tuan2_3_Baseline_MLP.ipynb`, `Tuan4_CNN.ipynb`, `Tuan6_LSTM.ipynb`, `Tuan7_Transformer.ipynb`, `Tuan8_Failure_Analysis.ipynb`, `Tuan9_ST_GCN_Concept.ipynb`).
*   **Thư mục `Dataset/`**: Chứa dữ liệu chuỗi cử chỉ tay.
*   **Thư mục `Models/`**: Chứa trọng số mô hình đã huấn luyện và file ONNX cho Unity.

---

## 2. Danh sách các chức năng đã hoàn thành

### Mã nguồn (Code) — 100% hoàn thành
*   **Thu thập và Xử lý dữ liệu**: Đã hoàn thành kịch bản thu thập dữ liệu bằng webcam (MediaPipe), bộ sinh dữ liệu giả (synthetic), và công cụ builder vạn năng để gộp nhiều nguồn dataset phức tạp.
*   **Trích xuất đặc trưng (Feature Engineering)**: Đã hoàn thiện mã nguồn trích xuất đặc trưng không gian-thời gian (velocity, acceleration, angles, distances).
*   **Kiến trúc Mô hình (Deep Learning)**: Đã thiết lập xong các notebook mô tả các kiến trúc từ cơ bản (MLP) đến phức tạp (CNN 1D, LSTM, Transformer).
*   **Nhận diện Thời gian thực (Real-time Inference)**: Mã nguồn kết nối Webcam với mô hình Transformer/LSTM, kèm theo UI overlay rất chuyên nghiệp (vẽ bằng OpenCV).
*   **OS Controller (HCI)**: Tính năng điều khiển OS bằng cử chỉ tay (điều chỉnh âm lượng, next/prev slide, drag & drop) đã được lập trình hoàn chỉnh với `pyautogui` và `pycaw`.
*   **Web App Trình diễn**: Dashboard Streamlit (`app.py`) đã code xong toàn bộ UI tĩnh và số liệu thống kê.

### Dữ liệu (Dataset) — ĐÃ HOÀN THÀNH
*   ✅ File `raw_data.csv` (53 MB) đã tồn tại trong thư mục `Dataset/`.

### Mô hình (Models) — ĐÃ HOÀN THÀNH
*   ✅ `best_mlp.pth` (736 KB) — Mô hình MLP baseline.
*   ✅ `best_transformer.pth` (1.9 MB) — Mô hình Transformer chính thức.
*   ✅ `gesture_model.onnx` (234 KB) + `gesture_model.onnx.data` (1.9 MB) — Mô hình đã xuất sang ONNX cho Unity Sentis.

---

## 3. Danh sách các chức năng chưa hoàn thành hoặc đang phát triển dở dang

*   **Thiếu file `requirements.txt`**: Mặc dù file `README.md` có đề cập đến `requirements.txt` trong phần Cấu trúc thư mục, nhưng file này hoàn toàn chưa tồn tại ở thư mục gốc, gây khó khăn cho việc cài đặt môi trường.
*   **Thiếu export StandardScaler parameters**: Script `run_training.py` sử dụng `StandardScaler` để chuẩn hóa dữ liệu trước khi huấn luyện, nhưng các tham số (mean, std) chưa được lưu ra file. Khi chạy inference trong Unity, dữ liệu landmark thô sẽ không được normalize → giảm độ chính xác nghiêm trọng.

---

## 4. Các lỗi, vấn đề tiềm ẩn hoặc code không tối ưu

> [!WARNING]
> **StandardScaler không đồng bộ giữa Train và Inference**
> Training script dùng StandardScaler để normalize data trước khi train. Tuy nhiên, khi inference trong Unity (hoặc Python realtime), data landmark thô được đưa thẳng vào model KHÔNG qua normalization. Cần export scaler params (mean[], std[]) ra JSON và load trong Unity `GestureDetector.cs` trước khi tạo tensor.

> [!TIP]
> **Vấn đề về Zero-padding trong lúc thu thập dữ liệu (`1_collect_data.py`)**
> Nếu ứng dụng đang ở trạng thái `recording = True` nhưng tay lại bị tuột ra khỏi khung hình webcam, code tự động thêm một chuỗi toàn số `0.0` vào `frame_buffer` cho đến khi đủ 15 frames. Việc tự động zero-pad giữa chừng như vậy có thể tạo ra các mẫu dữ liệu nhiễu (noise/outliers) làm giảm độ chính xác của mô hình Transformer/LSTM sau này. Lời khuyên là nên hủy mẫu đó thay vì zero-pad nếu tay biến mất giữa chừng.

> [!NOTE]
> **Phụ thuộc (Dependencies) ẩn**
> Kịch bản `5_os_controller.py` sử dụng thư viện `pycaw` và `comtypes` chỉ chạy được trên hệ điều hành Windows để chỉnh âm lượng. Code đã có fallback sang `pyautogui` nếu không import được `pycaw`, đây là cách xử lý lỗi khá tốt, tuy nhiên cần lưu ý document lại cho người dùng hệ điều hành khác (Linux/Mac).

> [!NOTE]
> **Hardcode Data trong Web App**
> Ứng dụng Streamlit (`app.py`) đang chứa các kết quả Accuracy, F1-Score (ví dụ Transformer 95.8%) được gõ cứng (hardcode) vào mã nguồn thay vì đọc từ file logs thực tế của quá trình huấn luyện. Nếu bạn huấn luyện lại mô hình và ra kết quả khác, bạn sẽ phải vào sửa code bằng tay.

---

## 5. Đánh giá tổng thể tiến độ hiện tại

*   **Mức độ hoàn thành mã nguồn (Codebase)**: **95%**. Các luồng xử lý từ lúc quay dữ liệu đến lúc predict đều đã được code xong. Script ONNX export và training script đã được bổ sung.
*   **Mức độ thực thi (Execution/Data)**: **90%**. Dataset đã được tạo (53MB), mô hình đã được huấn luyện (Transformer + MLP), ONNX đã được xuất thành công. Chỉ thiếu StandardScaler export.
*   **Tiến độ chung của toàn dự án**: **~90%**. Pipeline AI hoàn chỉnh từ thu thập → huấn luyện → xuất ONNX → tích hợp Unity.

---

## 6. Đánh giá độ hoàn chỉnh của project (Thang điểm)

*   **Kiến trúc và thiết kế**: **9.5/10**. Code tổ chức cực kỳ tốt, modular hóa tốt, các kịch bản python có giao diện console (CLI) và OpenCV UI trực quan, chuyên nghiệp.
*   **Tính sẵn sàng sử dụng (Usability)**: **8/10**. Đã có đầy đủ data, model weights, và ONNX export. Thiếu `requirements.txt` và StandardScaler params export.
*   **ĐỘ HOÀN CHỈNH TỔNG THỂ**: **9/10 (hoặc 90%)**.

### Khuyến nghị bước tiếp theo:
1. Tạo file `requirements.txt`.
2. Export StandardScaler params (mean, std) ra file JSON để Unity GestureDetector có thể normalize data trước inference.
3. Cập nhật `README.md` để phản ánh các script mới (`7_export_onnx.py`, `run_training.py`).
