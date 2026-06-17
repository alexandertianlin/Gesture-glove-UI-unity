# Setup Guide — onlytip 2.1 + HAMER Vision Bridge

## Overview

This project fuses HAMER (3D hand mesh recovery) with a Unity hand rig for real-time hand pose visualization. The Python bridge runs HAMER inference on camera input and sends 21 3D keypoints + per-finger curl data via UDP to Unity.

## Prerequisites

| Component | Requirement |
|-----------|-------------|
| OS | Windows 10+ |
| GPU | NVIDIA GPU with CUDA 12+ (RTX 30xx/40xx recommended) |
| Unity | 2022.3.62f3c1 (Editor) |
| Python | 3.10 (conda environment) |
| Camera | Any USB camera or built-in webcam |

## Repository Structure

```
onlytip - 2.1/          # Unity project
├── Assets/
│   ├── post.cs         # UDP receiver + hand rig controller
│   ├── PostProcess.cs  # (placeholder for IMU fusion)
│   ├── ForceGridVisualizer.cs  # Pressure sensor visualizer
│   ├── hand-only-rig.fbx       # Hand bone rig model
│   └── sc.unity        # Main scene
├── Packages/
├── ProjectSettings/
└── SETUP.md            # This file

hamer_unity_bridge.py   # Python: HAMER → Unity UDP bridge (workspace root)
```

## 1. Unity Setup

### 1.1 Install Unity Editor

Required version: **2022.3.62f3c1**

- Download from Unity Hub or China CDN:
  - Global: `https://download.unity3d.com/download_unity/1623fc0bbb97/Windows64EditorInstaller/UnitySetup64-2022.3.62f3c1.exe`
  - China: `https://download.unitychina.cn/download_unity/1623fc0bbb97/Windows64EditorInstaller/UnitySetup64-2022.3.62f3c1.exe`
- Install to `D:\Program\Unity\Editor\2022.3.62f3c1\`

### 1.2 Open the Project

1. Open Unity Hub
2. Click **Open** → select `onlytip - 2.1` folder
3. Open scene: `Assets/sc.unity`

### 1.3 Scene Hierarchy

```
Main Camera          ← Camera at (0, 1, -3), identity rotation
Directional Light    ← Default light
hand-only-rig        ← Hand rig FBX (PrefabInstance)
mp                   ← Game Object with post.cs attached
```

### 1.4 Configure post.cs

Select the `mp` GameObject in the Hierarchy. In the Inspector, assign bones:

| Field | Source |
|-------|--------|
| `wristBone` | `hand-only-rig/Root/Wrist` (or similar bone) |
| `thumbBones[0-3]` | Thumb CMC → MCP → IP → TIP |
| `indexBones[0-3]` | Index MCP → PIP → DIP → TIP |
| `middleBones[0-3]` | Middle MCP → PIP → DIP → TIP |
| `ringBones[0-3]` | Ring MCP → PIP → DIP → TIP |
| `littleBones[0-3]` | Little MCP → PIP → DIP → TIP |

> Expand `hand-only-rig` in the Hierarchy to see all bone Transforms.

## 2. Python Bridge Setup

### 2.1 Environment

```powershell
# Create conda environment (already exists as 'hamer')
conda activate hamer

# Verify GPU
python -c "import torch; print(torch.cuda.is_available(), torch.cuda.get_device_name(0))"
```

### 2.2 Required Files

The bridge script `hamer_unity_bridge.py` depends on:

- HAMER model files in `hamer_code/hamer-main/_DATA/`:
  - `hamer_ckpts/checkpoints/hamer.ckpt` (2.5 GB)
  - `model_config.yaml` (2 KB)
  - `data/mano/MANO_RIGHT.pkl` (3.8 MB)
- ViTPose checkpoint:
  - `vitpose_ckpts/vitpose+_huge/wholebody.pth` (3.8 GB)
  - Config: `ViTPose_huge_wholebody_256x192.py`

### 2.3 Run the Bridge

```powershell
# 1. In Unity: Press Play (scene sc.unity)

# 2. In separate terminal:
"D:\ProgramData\anaconda3\envs\hamer\python.exe" hamer_unity_bridge.py

# 3. Camera window shows HAMER keypoints overlay
# 4. Unity Console shows "[post] seq=..." logs on data receive
```

### 2.4 UDP Protocol

The Python bridge sends JSON packets to `127.0.0.1:5055`:

```json
{
  "type": "hamer_hand",
  "seq": 42,
  "ts": 1718600000000,
  "num_hands": 2,
  "hand_0_label": "right",
  "hand_0_conf": 0.95,
  "hand_0_wrist": [x, y, z],
  "hand_0_kp3d": [x0,y0,z0, x1,y1,z1, ...],  // 63 floats: 21×3
  "hand_0_orient_q": [qw, qx, qy, qz],
  "hand_0_curl_thumb": 0.5,
  "hand_0_curl_index": 0.3,
  "hand_0_curl_middle": 0.2,
  "hand_0_curl_ring": 0.4,
  "hand_0_curl_little": 0.3,
  "hand_0_spread_thumb": 0.1,
  "hand_0_spread_index": 0.05,
  ...
}
```

## 3. Data Flow Architecture

```
Camera ──▶ ViTPose ──▶ HAMER ──▶ 21×3D keypoints ──▶ UDP JSON ──▶ Unity post.cs
                │                  + MANO params                  │
           (every 6 frames)        + finger curl/spread           │
                                                                  ▼
                                                          hand-only-rig
                                                          (bone rotations)
```

- **ViTPose**: Full-body pose estimation (used for hand bbox detection, runs every 6 frames)
- **HAMER**: Hand Mesh Recovery — outputs 21 3D hand keypoints + MANO pose parameters (runs every frame)
- **UDP Bridge**: Computes per-finger curl (0=straight, 1=curled) and spread from 3D keypoints
- **Unity**: Smooths received data with exponential moving average, applies bone rotations

## 4. Configuration

### Python Bridge (`hamer_unity_bridge.py`)

| Variable | Default | Description |
|----------|---------|-------------|
| `UNITY_IP` | `127.0.0.1` | Unity UDP listener IP |
| `UNITY_PORT` | `5055` | Unity UDP listener port |
| `FRAME_WIDTH` | `640` | Camera capture width |
| `FRAME_HEIGHT` | `480` | Camera capture height |
| `VITPOSE_SKIP` | `5` | ViTPose frame skip interval |

### Unity `post.cs`

| Parameter | Default | Description |
|-----------|---------|-------------|
| `listenPort` | `5055` | UDP listen port |
| `enableLog` | `true` | Log received packets |
| `curlSmooth` | `0.3` | Smoothing factor (lower = smoother) |

## 5. IMU Fusion (Future)

The placeholder `IMUFusion` class in `hamer_unity_bridge.py` and `PostProcess.cs` are reserved for fusing STM32 IMU glove data with HAMER vision output using a complementary filter.

Pending hardware:
- STM32 serial data parser
- Quaternion → Unity bone mapping
- Complementary/Kalman filter implementation

## 6. Troubleshooting

| Problem | Solution |
|---------|----------|
| "No module named 'torch'" | Activate conda: `conda activate hamer` |
| Camera not opening | Try index 0 or 1: `cv2.VideoCapture(1)` |
| No UDP in Unity Console | Check firewall, verify port 5055 |
| Hand not visible in Scene | Check Hierarchy for `mp` and `hand-only-rig` |
| Hand only shows bones | `hand-only-rig.fbx` is a rig (no mesh). Add skinned mesh or use as skeleton reference |
