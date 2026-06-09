# 🖐️ Phân loại Chuỗi Cử chỉ Tay Động (Time-Series Hand Gesture Classification)

![Python](https://img.shields.io/badge/Python-3.10+-blue.svg)
![PyTorch](https://img.shields.io/badge/PyTorch-2.0+-red.svg)
![MediaPipe](https://img.shields.io/badge/MediaPipe-Vision-green.svg)

Đây là Đồ án môn học Deep Learning (Lộ trình 8 tuần). Dự án tập trung nghiên cứu, thiết kế và huấn luyện các kiến trúc mạng nơ-ron từ cơ bản đến nâng cao nhằm giải quyết bài toán **phân loại dữ liệu chuỗi thời gian (Time-series Classification)**, cụ thể là nhận diện các cử chỉ tay động trong không gian 3D.

---

## 📑 Mục lục
1. [Mô tả Bài toán](#-mô-tả-bài-toán)
2. [Cấu trúc Dữ liệu (Dataset)](#-cấu-trúc-dữ-liệu-dataset)
3. [Cài đặt Môi trường](#-cài-đặt-môi-trường)
4. [Lộ trình Huấn luyện (8 Tuần)](#-lộ-trình-huấn-luyện-8-tuần)
5. [Cấu trúc Thư mục](#-cấu-trúc-thư-mục)

---

## 🎯 Mô tả Bài toán
Việc nhận diện cử chỉ tay tĩnh (Static Posture) thông qua ảnh chụp RGB đã trở nên phổ biến. Tuy nhiên, các hành động như "Vuốt trái", "Vuốt phải", "Đẩy tới" mang **tính chất thời gian (Temporal)**, đòi hỏi hệ thống phải quan sát quỹ đạo chuyển động qua nhiều khung hình.

Dự án này ứng dụng giải pháp:
- **Tiền xử lý:** Sử dụng thuật toán Computer Vision (MediaPipe) để chuyển hóa khung hình thô thành ma trận tọa độ không gian số học, giúp mô hình học sâu không bị nhiễu bởi bối cảnh (background) hay ánh sáng.
- **Học Sâu:** Ứng dụng các mạng học chuỗi (LSTM, Transformer) để phân loại chuỗi tọa độ thời gian thực.

---

## 📊 Cấu trúc Dữ liệu (Dataset)
Dữ liệu đầu vào không phải là ảnh gốc mà là các vector tọa độ:
- **Số điểm (Landmarks):** 21 khớp xương trên bàn tay.
- **Không gian:** 3 trục (X, Y, Z).
- **Kích thước một khung hình (Frame Feature):** $21 \times 3 = 63$ chiều.
- **Độ dài chuỗi (Sequence Length):** 15 khung hình cho một hành động hoàn chỉnh.
- **Input Tensor Shape cho LSTM/Transformer:** `(Batch_Size, 15, 63)`.

---

## ⚙️ Cài đặt Môi trường

Dự án được cấu hình chạy trong môi trường ảo độc lập. 

```bash
# Di chuyển vào không gian làm việc
cd D:\WORKSAPCE\Monitor_AR\1_AI_Model_Training

# Khởi tạo và kích hoạt môi trường ảo
python -m venv venv
.\venv\Scripts\activate

# Cài đặt các thư viện lõi
pip install torch torchvision torchaudio mediapipe opencv-python pandas scikit-learn matplotlib seaborn jupyterlab

```

---

## 📅 Lộ trình Huấn luyện (8 Tuần)

Dự án bám sát tiến độ học thuật với các cột mốc sau:

| Giai đoạn | Kiến trúc | Mục tiêu nghiên cứu |
| --- | --- | --- |
| **Tuần 2-3** | **MLP (Baseline)** | Nhận diện tư thế tĩnh. Cắt lấy 1 frame duy nhất để huấn luyện. Thiết lập Pipeline chia tập Train/Val/Test, vẽ biểu đồ Loss. |
| **Tuần 4** | **1D-CNN** | Khám phá khả năng dùng tích chập 1D trượt dọc theo chiều thời gian để trích xuất đặc trưng chuyển động cục bộ. |
| **Tuần 5** | **Transfer Learning** | Thử nghiệm đóng băng trọng số (Freeze weights) hoặc Fine-tune một số lớp Embedding để tối ưu hóa biểu diễn tọa độ. |
| **Tuần 6** | **RNN / LSTM** | Xây dựng mạng Recurrent Neural Network nhằm theo dõi sự thay đổi tọa độ từ frame 1 đến frame 15, trực quan hóa Hidden States. |
| **Tuần 7** | **Transformer** | Ứng dụng Self-Attention trên dữ liệu chuỗi tọa độ, so sánh tốc độ hội tụ và độ chính xác với kiến trúc LSTM. |
| **Tuần 8** | **Failure Analysis** | Lọc các nhãn dự đoán sai (Misclassifications), phân tích nguyên nhân kỹ thuật (do ngoại lệ dữ liệu hay điểm mù thuật toán). |

---

## 📁 Cấu trúc Thư mục

```text
D:\WORKSAPCE\Monitor_AR\1_AI_Model_Training\
├── Dataset/                     # Dữ liệu dạng bảng (CSV) chứa chuỗi tọa độ
│   └── raw_data.csv             
├── Models/                      # Nơi lưu các file trọng số (.pth) tốt nhất
├── Notebooks/                   # Mã nguồn huấn luyện (Jupyter Lab)
│   ├── Tuan2_3_Baseline_MLP.ipynb
│   ├── Tuan4_CNN.ipynb
│   ├── Tuan6_LSTM.ipynb
│   ├── Tuan7_Transformer.ipynb
│   └── Tuan8_Failure_Analysis.ipynb
├── Scripts/                     # Các file script chạy tác vụ độc lập
│   └── 1_collect_data.py        # Kịch bản thu thập dữ liệu bằng Webcam
├── README.md                    # Tài liệu tổng quan (File này)
└── requirements.txt             

```

```


