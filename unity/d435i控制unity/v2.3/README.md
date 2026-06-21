# v2.3 - MediaPipe → FingerSolver 管线集成

## 功能
将 D435i + MediaPipe Hand 的 curl/spread 数据通过 UDP 喂入 Unity 的 HandMotionManager + FingerSolver 管线，利用完整的 IK 逻辑驱动 3D 手模型。

## 文件结构
- MediaPipeFingerSolverBridge.cs — Unity C# 脚本
- config/ — 配置文件
- docs/ — 文档

## 端口
- UDP 监听端口: 5057

## 使用
1. 将 MediaPipeFingerSolverBridge.cs 拖入 Unity 项目的 Assets 文件夹
2. 在 mp GameObject 上挂载 MediaPipeFingerSolverBridge（自动添加 HandMotionManager）
3. 将 hand-only-rig.fbx 拖到 handPrefab 字段
4. 右键脚本 → Auto Assign Bones 自动分配骨骼
5. 运行: python run_d435i_hand.py --send --port 5057
6. Unity 中按 Play

## 依赖
- HandMotionManager（自动添加）
- FingerSolver（HandMotionManager 持有 5 个 FingerSolver 实例）
- SerialReceiver（HandMotionManager 自动依赖）

## 骨骼映射
FBX 每指 4 骨 → FingerSolver 3 骨:
- rootBone = bone[0] (MCP)
- midBone = bone[2] (DIP)
- tipBone = bone[3] (TIP)
