import os
import sys
import math
import torch
import torch.nn as nn

# Fix Windows console encoding
sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')

# =============================================================================
# KIẾN TRÚC MÔ HÌNH — Positional Encoding & Gesture Transformer
# (Được trích xuất từ Notebooks/Tuan7_Transformer.ipynb)
# =============================================================================

class PositionalEncoding(nn.Module):
    """
    Mã hóa vị trí bằng hàm sin/cos theo công thức chuẩn.
    Input:  (batch, seq_len, d_model)
    Output: (batch, seq_len, d_model)  — cùng shape, đã cộng thêm vị trí.
    """
    
    def __init__(self, d_model, max_len=500, dropout=0.1):
        super().__init__()
        self.dropout = nn.Dropout(p=dropout)
        
        # Tạo ma trận PE: (max_len, d_model)
        pe = torch.zeros(max_len, d_model)
        position = torch.arange(0, max_len, dtype=torch.float32).unsqueeze(1)  # (max_len, 1)
        div_term = torch.exp(
            torch.arange(0, d_model, 2, dtype=torch.float32) * (-math.log(10000.0) / d_model)
        )  # (d_model/2,)
        
        pe[:, 0::2] = torch.sin(position * div_term)  # Vị trí chẵn: sin
        pe[:, 1::2] = torch.cos(position * div_term)  # Vị trí lẻ:  cos
        
        # Thêm batch dimension: (1, max_len, d_model)
        pe = pe.unsqueeze(0)
        self.register_buffer('pe', pe)  # Không cập nhật gradient
    
    def forward(self, x):
        # x shape: (batch, seq_len, d_model)
        x = x + self.pe[:, :x.size(1), :]
        return self.dropout(x)


class GestureTransformer(nn.Module):
    """
    Transformer Encoder cho phân loại chuỗi cử chỉ tay.
    """
    
    def __init__(self, input_dim=63, d_model=128, nhead=4,
                 dim_feedforward=256, num_layers=3, num_classes=14,
                 dropout=0.2, pe_dropout=0.1):
        super().__init__()
        
        self.d_model = d_model
        
        # ① Input Projection: 63 → 128
        self.input_projection = nn.Linear(input_dim, d_model)
        
        # ② Positional Encoding
        self.pos_encoder = PositionalEncoding(
            d_model=d_model, max_len=500, dropout=pe_dropout
        )
        
        # ③ Transformer Encoder
        encoder_layer = nn.TransformerEncoderLayer(
            d_model=d_model,
            nhead=nhead,
            dim_feedforward=dim_feedforward,
            dropout=dropout,
            batch_first=True,         # Input: (batch, seq, feature)
            activation='relu'
        )
        self.transformer_encoder = nn.TransformerEncoder(
            encoder_layer, num_layers=num_layers
        )
        
        # ④ Classifier Head
        self.classifier = nn.Sequential(
            nn.Linear(d_model, 64),
            nn.ReLU(),
            nn.Dropout(0.3),
            nn.Linear(64, num_classes)
        )
    
    def forward(self, x):
        # x: (batch, 15, 63)
        # ① Projection
        x = self.input_projection(x)  # (batch, 15, 128)
        # ② Positional Encoding
        x = self.pos_encoder(x)  # (batch, 15, 128)
        # ③ Transformer Encoder
        x = self.transformer_encoder(x)  # (batch, 15, 128)
        # ④ Global Mean Pooling — trung bình qua chiều thời gian
        x = x.mean(dim=1)  # (batch, 128)
        # ⑤ Classification
        x = self.classifier(x)  # (batch, num_classes)
        return x

# =============================================================================
# SCRIPT XUẤT MÔ HÌNH RA ONNX
# =============================================================================
def main():
    print("=" * 60)
    print("  ONNX EXPORT SCRIPT CHO SENTIS")
    print("=" * 60)
    
    PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    MODEL_DIR = os.path.join(PROJECT_ROOT, "Models")
    BEST_MODEL_PATH = os.path.join(MODEL_DIR, "best_transformer.pth")
    ONNX_OUTPUT_PATH = os.path.join(MODEL_DIR, "gesture_model.onnx")
    
    # 1. Cấu hình kiến trúc phải khớp với model đã train
    # Số classes = 14 (Gestures), frames = 15, features = 63
    num_classes = 14
    model = GestureTransformer(
        input_dim=63,
        d_model=128,
        nhead=4,
        dim_feedforward=256,
        num_layers=3,
        num_classes=num_classes,
        dropout=0.2,
        pe_dropout=0.1
    )
    
    # 2. Load trọng số
    if not os.path.exists(BEST_MODEL_PATH):
        print(f"[LỖI] Không tìm thấy file trọng số tại: {BEST_MODEL_PATH}")
        return
    
    print(f"[*] Đang tải trọng số từ: {BEST_MODEL_PATH}")
    state_dict = torch.load(BEST_MODEL_PATH, map_location='cpu', weights_only=True)
    model.load_state_dict(state_dict)
    
    # Chuyển model sang chế độ đánh giá (RẤT QUAN TRỌNG cho ONNX export)
    model.eval()
    
    # 3. Tạo dummy input (tensor mẫu)
    # Shape: (Batch_size=1, Frames=15, Features=63)
    dummy_input = torch.randn(1, 15, 63, dtype=torch.float32)
    print(f"[*] Đã khởi tạo dummy input với shape: {dummy_input.shape}")
    
    # 4. Xuất ra định dạng ONNX
    print(f"[*] Bắt đầu xuất mô hình ra định dạng ONNX...")
    torch.onnx.export(
        model,
        dummy_input,
        ONNX_OUTPUT_PATH,
        export_params=True,
        opset_version=11,
        do_constant_folding=True,
        input_names=['input'],
        output_names=['output']
    )
    
    print(f"[THÀNH CÔNG] Mô hình đã được xuất ra: {ONNX_OUTPUT_PATH}")
    print("=" * 60)

if __name__ == "__main__":
    main()
