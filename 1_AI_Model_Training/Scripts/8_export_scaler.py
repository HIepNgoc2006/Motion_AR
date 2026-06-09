import os
import json
import pandas as pd
import numpy as np
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import StandardScaler, LabelEncoder

SEED = 42
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA_PATH = os.path.join(PROJECT_ROOT, 'Dataset', 'raw_data.csv')
OUTPUT_PATH = os.path.join(PROJECT_ROOT, 'Models', 'scaler_params.json')

def main():
    print("Reading data...")
    df = pd.read_csv(DATA_PATH)
    
    meta_cols = ['sample_id', 'frame_index', 'label']
    feature_cols = [c for c in df.columns if c not in meta_cols]
    
    le = LabelEncoder()
    df['label_encoded'] = le.fit_transform(df['label'])
    
    grouped = df.sort_values(['sample_id', 'frame_index']).groupby('sample_id')
    X_list, y_list = [], []
    for sample_id, group in grouped:
        features = group[feature_cols].values
        label = group['label_encoded'].iloc[0]
        X_list.append(features)
        y_list.append(label)
        
    X_all = np.array(X_list, dtype=np.float32)
    y_all = np.array(y_list, dtype=np.int64)
    
    X_train_val, X_test, y_train_val, y_test = train_test_split(
        X_all, y_all, test_size=0.15, random_state=SEED, stratify=y_all
    )
    val_ratio = 0.15 / 0.85
    X_train, X_val, y_train, y_val = train_test_split(
        X_train_val, y_train_val, test_size=val_ratio, random_state=SEED, stratify=y_train_val
    )
    
    num_features = X_train.shape[2]
    scaler = StandardScaler()
    scaler.fit(X_train.reshape(-1, num_features))
    
    params = {
        "mean": scaler.mean_.tolist(),
        "scale": scaler.scale_.tolist()
    }
    
    with open(OUTPUT_PATH, 'w') as f:
        json.dump(params, f, indent=4)
        
    print(f"Exported scaler params to {OUTPUT_PATH}")

if __name__ == '__main__':
    main()
