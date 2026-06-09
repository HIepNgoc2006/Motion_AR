"""
run_training.py — Script huấn luyện Transformer cho phân loại cử chỉ tay
Trích xuất từ: Notebooks/Tuan7_Transformer.ipynb
Input:  Dataset/raw_data.csv
Output: Models/best_transformer.pth
"""

import os
import sys
import math
import time
import warnings
import numpy as np
import pandas as pd
from copy import deepcopy

# Fix Windows console encoding (cp1252 → utf-8)
sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')

import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import Dataset, DataLoader

from sklearn.model_selection import train_test_split
from sklearn.preprocessing import StandardScaler, LabelEncoder
from sklearn.metrics import classification_report

warnings.filterwarnings('ignore')

# =============================================================================
# CẤU HÌNH
# =============================================================================
SEED = 42
NUM_EPOCHS = 40
BATCH_SIZE = 32
LEARNING_RATE = 5e-4
WEIGHT_DECAY = 1e-4

# Đường dẫn
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA_PATH = os.path.join(PROJECT_ROOT, 'Dataset', 'raw_data.csv')
MODEL_DIR = os.path.join(PROJECT_ROOT, 'Models')
BEST_MODEL_PATH = os.path.join(MODEL_DIR, 'best_transformer.pth')
os.makedirs(MODEL_DIR, exist_ok=True)

# Seed
torch.manual_seed(SEED)
np.random.seed(SEED)
if torch.cuda.is_available():
    torch.cuda.manual_seed_all(SEED)

device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')

# =============================================================================
# KIẾN TRÚC MÔ HÌNH (giữ nguyên 100% từ notebook)
# =============================================================================
class PositionalEncoding(nn.Module):
    """
    Mã hóa vị trí bằng hàm sin/cos theo công thức chuẩn.
    Input:  (batch, seq_len, d_model)
    Output: (batch, seq_len, d_model)
    """
    def __init__(self, d_model, max_len=500, dropout=0.1):
        super().__init__()
        self.dropout = nn.Dropout(p=dropout)

        pe = torch.zeros(max_len, d_model)
        position = torch.arange(0, max_len, dtype=torch.float32).unsqueeze(1)
        div_term = torch.exp(
            torch.arange(0, d_model, 2, dtype=torch.float32) * (-math.log(10000.0) / d_model)
        )
        pe[:, 0::2] = torch.sin(position * div_term)
        pe[:, 1::2] = torch.cos(position * div_term)
        pe = pe.unsqueeze(0)
        self.register_buffer('pe', pe)

    def forward(self, x):
        x = x + self.pe[:, :x.size(1), :]
        return self.dropout(x)


class GestureTransformer(nn.Module):
    """
    Transformer Encoder cho phân loại chuỗi cử chỉ tay.
    Pipeline:
        Input (batch, 15, 63)
          → Linear Projection (63 → 128)
          → Positional Encoding
          → Transformer Encoder (3 layers, 4 heads)
          → Global Mean Pooling (trung bình 15 frames)
          → Classifier Head (128 → 64 → num_classes)
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
            batch_first=True,
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
        x = self.input_projection(x)          # (batch, 15, 128)
        x = self.pos_encoder(x)               # (batch, 15, 128)
        x = self.transformer_encoder(x)       # (batch, 15, 128)
        x = x.mean(dim=1)                     # (batch, 128)
        x = self.classifier(x)                # (batch, num_classes)
        return x


# =============================================================================
# DATASET
# =============================================================================
class GestureSequenceDataset(Dataset):
    """Dataset cho chuỗi cử chỉ tay (15 frame × 63 features)."""
    def __init__(self, X, y):
        self.X = torch.tensor(X, dtype=torch.float32)
        self.y = torch.tensor(y, dtype=torch.long)

    def __len__(self):
        return len(self.y)

    def __getitem__(self, idx):
        return self.X[idx], self.y[idx]


# =============================================================================
# TRAIN / EVALUATE
# =============================================================================
def train_one_epoch(model, loader, criterion, optimizer, device):
    model.train()
    running_loss = 0.0
    correct = 0
    total = 0

    for X_batch, y_batch in loader:
        X_batch = X_batch.to(device)
        y_batch = y_batch.to(device)

        optimizer.zero_grad()
        outputs = model(X_batch)
        loss = criterion(outputs, y_batch)
        loss.backward()

        # Gradient clipping — ổn định huấn luyện Transformer
        torch.nn.utils.clip_grad_norm_(model.parameters(), max_norm=1.0)

        optimizer.step()

        running_loss += loss.item() * X_batch.size(0)
        _, predicted = outputs.max(1)
        total += y_batch.size(0)
        correct += predicted.eq(y_batch).sum().item()

    epoch_loss = running_loss / total
    epoch_acc = correct / total
    return epoch_loss, epoch_acc


def evaluate(model, loader, criterion, device):
    model.eval()
    running_loss = 0.0
    correct = 0
    total = 0

    with torch.no_grad():
        for X_batch, y_batch in loader:
            X_batch = X_batch.to(device)
            y_batch = y_batch.to(device)

            outputs = model(X_batch)
            loss = criterion(outputs, y_batch)

            running_loss += loss.item() * X_batch.size(0)
            _, predicted = outputs.max(1)
            total += y_batch.size(0)
            correct += predicted.eq(y_batch).sum().item()

    epoch_loss = running_loss / total
    epoch_acc = correct / total
    return epoch_loss, epoch_acc


# =============================================================================
# MAIN
# =============================================================================
def main():
    print("=" * 70)
    print("  GESTURE TRANSFORMER — TRAINING SCRIPT")
    print("=" * 70)
    print(f"  Device : {device}")
    if device.type == 'cuda':
        print(f"  GPU    : {torch.cuda.get_device_name(0)}")
    print(f"  Epochs : {NUM_EPOCHS}")
    print(f"  Batch  : {BATCH_SIZE}")
    print(f"  LR     : {LEARNING_RATE}")
    print(f"  Data   : {DATA_PATH}")
    print(f"  Output : {BEST_MODEL_PATH}")
    print("=" * 70)

    # ── 1. Đọc CSV ──
    print("\n[1/5] Đọc dữ liệu...")
    df = pd.read_csv(DATA_PATH)
    print(f"  DataFrame shape: {df.shape}")
    print(f"  Nhãn ({df['label'].nunique()} lớp): {list(df['label'].unique())}")

    # ── 2. Tiền xử lý ──
    print("\n[2/5] Tiền xử lý dữ liệu...")
    meta_cols = ['sample_id', 'frame_index', 'label']
    feature_cols = [c for c in df.columns if c not in meta_cols]
    print(f"  Số cột đặc trưng: {len(feature_cols)}")
    assert len(feature_cols) == 63, f"Kỳ vọng 63 features, nhận {len(feature_cols)}"

    le = LabelEncoder()
    df['label_encoded'] = le.fit_transform(df['label'])
    num_classes = len(le.classes_)
    print(f"  Số lớp: {num_classes}")

    # Tạo ma trận 3D: (num_samples, 15, 63)
    grouped = df.sort_values(['sample_id', 'frame_index']).groupby('sample_id')
    X_list, y_list = [], []
    for sample_id, group in grouped:
        features = group[feature_cols].values
        label = group['label_encoded'].iloc[0]
        X_list.append(features)
        y_list.append(label)

    X_all = np.array(X_list, dtype=np.float32)
    y_all = np.array(y_list, dtype=np.int64)
    print(f"  X shape: {X_all.shape}  (expected: (N, 15, 63))")
    print(f"  y shape: {y_all.shape}")

    # ── 3. Chia tập 70/15/15 ──
    print("\n[3/5] Chia tập dữ liệu 70/15/15...")
    X_train_val, X_test, y_train_val, y_test = train_test_split(
        X_all, y_all, test_size=0.15, random_state=SEED, stratify=y_all
    )
    val_ratio = 0.15 / 0.85
    X_train, X_val, y_train, y_val = train_test_split(
        X_train_val, y_train_val, test_size=val_ratio, random_state=SEED, stratify=y_train_val
    )
    print(f"  Train : {X_train.shape[0]} samples")
    print(f"  Val   : {X_val.shape[0]} samples")
    print(f"  Test  : {X_test.shape[0]} samples")

    # StandardScaler: fit trên Train
    num_features = X_train.shape[2]
    scaler = StandardScaler()
    scaler.fit(X_train.reshape(-1, num_features))
    X_train = scaler.transform(X_train.reshape(-1, num_features)).reshape(X_train.shape)
    X_val   = scaler.transform(X_val.reshape(-1, num_features)).reshape(X_val.shape)
    X_test  = scaler.transform(X_test.reshape(-1, num_features)).reshape(X_test.shape)
    print(f"  Chuẩn hóa hoàn tất (StandardScaler)")

    # DataLoader
    train_loader = DataLoader(
        GestureSequenceDataset(X_train, y_train),
        batch_size=BATCH_SIZE, shuffle=True, drop_last=False
    )
    val_loader = DataLoader(
        GestureSequenceDataset(X_val, y_val),
        batch_size=BATCH_SIZE, shuffle=False, drop_last=False
    )
    test_loader = DataLoader(
        GestureSequenceDataset(X_test, y_test),
        batch_size=BATCH_SIZE, shuffle=False, drop_last=False
    )

    # ── 4. Khởi tạo model & huấn luyện ──
    print("\n[4/5] Khởi tạo model & bắt đầu huấn luyện...")
    model = GestureTransformer(
        input_dim=63,
        d_model=128,
        nhead=4,
        dim_feedforward=256,
        num_layers=3,
        num_classes=num_classes,
        dropout=0.2,
        pe_dropout=0.1
    ).to(device)

    total_params = sum(p.numel() for p in model.parameters())
    print(f"  Tổng tham số: {total_params:,}")

    criterion = nn.CrossEntropyLoss()
    optimizer = optim.Adam(model.parameters(), lr=LEARNING_RATE, weight_decay=WEIGHT_DECAY)
    scheduler = optim.lr_scheduler.CosineAnnealingLR(optimizer, T_max=NUM_EPOCHS, eta_min=1e-6)

    best_val_acc = 0.0
    best_epoch = 0
    best_model_state = None

    header = f"{'Epoch':>6} | {'Train Loss':>10} | {'Train Acc':>9} | {'Val Loss':>10} | {'Val Acc':>9} | {'LR':>10} | {'Time':>6}"
    print(f"\n{'=' * 80}")
    print(header)
    print(f"{'-' * 80}")

    total_train_time = 0.0

    for epoch in range(1, NUM_EPOCHS + 1):
        epoch_start = time.time()

        train_loss, train_acc = train_one_epoch(model, train_loader, criterion, optimizer, device)
        val_loss, val_acc = evaluate(model, val_loader, criterion, device)

        current_lr = optimizer.param_groups[0]['lr']
        scheduler.step()

        epoch_time = time.time() - epoch_start
        total_train_time += epoch_time

        marker = ''
        if val_acc > best_val_acc:
            best_val_acc = val_acc
            best_epoch = epoch
            best_model_state = deepcopy(model.state_dict())
            torch.save(best_model_state, BEST_MODEL_PATH)
            marker = ' << BEST'

        # In mỗi epoch
        print(
            f"{epoch:>6} | {train_loss:>10.4f} | {train_acc:>8.2%} | "
            f"{val_loss:>10.4f} | {val_acc:>8.2%} | {current_lr:>10.6f} | {epoch_time:>5.1f}s{marker}"
        )

    print(f"{'-' * 80}")
    print(f"\n  Best Val Accuracy: {best_val_acc:.4%} (Epoch {best_epoch})")
    print(f"  Tổng thời gian: {total_train_time:.1f}s")
    print(f"  Model đã lưu: {BEST_MODEL_PATH}")

    # ── 5. Đánh giá trên Test set ──
    print(f"\n[5/5] Đánh giá trên tập Test...")
    model.load_state_dict(best_model_state)
    model.eval()

    all_preds = []
    all_labels = []
    with torch.no_grad():
        for X_batch, y_batch in test_loader:
            X_batch = X_batch.to(device)
            outputs = model(X_batch)
            _, predicted = outputs.max(1)
            all_preds.extend(predicted.cpu().numpy())
            all_labels.extend(y_batch.numpy())

    print(classification_report(
        all_labels, all_preds,
        target_names=le.classes_,
        digits=4
    ))

    test_loss, test_acc = evaluate(model, test_loader, criterion, device)
    print(f"  Test Accuracy: {test_acc:.4%}")
    print(f"  Test Loss:     {test_loss:.4f}")
    print("=" * 70)
    print("  HOÀN TẤT HUẤN LUYỆN!")
    print("=" * 70)


if __name__ == '__main__':
    main()
