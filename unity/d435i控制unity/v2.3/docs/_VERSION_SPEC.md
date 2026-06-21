# v2.3-feat-fingersolver-integration
# 日期: 2026-06-21
# 状态: in_progress

## 功能
将 D435i + MediaPipe Hand 的 curl/spread 数据喂入 Unity 的 HandMotionManager + FingerSolver 管线。

## 技术栈
- Python: run_d435i_hand.py (D435i → MediaPipe → UDP)
- Unity: MediaPipeFingerSolverBridge.cs → HandMotionManager → FingerSolver

## 端口
5057

## 关键文件
| 文件 | 路径 |
|------|------|
| Unity C# 脚本 | unity/d435i控制unity/v2.3/MediaPipeFingerSolverBridge.cs |
| Python 发送端 | unity/d435i控制unity/v2.2版本/run_d435i_hand.py |
