# v2.2 MediaPipe Direct Hand Control

## 概述
D435i + MediaPipe Hand → Unity 3D 手模型直接驱骨（无 HandMotionManager 依赖）

## 文件说明
| 文件 | 说明 |
|------|------|
| MediaPipeDirectHand.cs | Unity C# 脚本（放入 Assets/Scenes/） |
| un_d435i_hand.py | Python UDP 发送端（参考 v2.0） |
| _VERSION_SPEC.md | 版本说明 |

## Unity 设置步骤

### 1. 添加脚本
将 MediaPipeDirectHand.cs 放入 Assets/Scenes/，然后拖到任意 GameObject 上（建议 mp）。

### 2. 绑定手模型
在 Inspector 中：
- 将 hand-only-rig.fbx 从 Project 窗口拖到 Hand Prefab 字段
- 右键脚本标题 → Diagnose Bone Hierarchy（看 Console 输出骨骼层次）
- 右键脚本标题 → Auto Assign Bones（自动分配）

如果自动分配失败：根据诊断结果，将骨骼按顺序拖入 llBonesManual 数组
（索引 0=wrist, 1-4=thumb, 5-8=index, 9-12=middle, 13-16=ring, 17-20=pinky）

### 3. 运行 Python 发送端
`
cd /d C:\Users\tianl\Documents\Codex\tasks\task-hand-tracking\v2.0-feat-d435i-hand-20260621\
C:\Users\tianl\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe src\run_d435i_hand.py --send --port 5056
`

### 4. Unity Hit Play

## 端口设置
- Unity 接收端口: 5056（脚本中可调）
- Python 发送端口: 5056（--port 参数）

## 关键参数
- Bend Limits: 每指最大弯曲角度 (thumb=80°, index=95°, middle=100°, ring=100°, little=90°)
- Spread Limits: 每指最大展开角度 (thumb=35°, index=20°, middle=15°, ring=15°, little=25°)
- Joint Weights: 每指4关节角度分配 {MCP, PIP, DIP, TIP} = {0.1, 0.4, 0.7, 1.0}
- Thumb Natural Spread: 大拇指自然外展偏移 30°
