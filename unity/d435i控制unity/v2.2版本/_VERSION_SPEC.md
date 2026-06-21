 # v2.2-feat-direct-hand-control - MediaPipe 直接驱骨（独立脚本）
 
 > **Theme**: 独立脚本，像 post.cs 一样直接驱动骨骼，但带更智能的角度分配
 > **Date**: 2026-06-21
 > **Status**: in_progress
 
 ## Summary
 
 独立 C# 脚本，无 HandMotionManager/FingerSolver 依赖。
 骨骼绑定方式同 post.cs（wristBone + 每指4根骨骼），
 但角度计算使用 per-finger maxPitch/maxYaw + 关节比例权重。
 
 ## Files
 src/unity/MediaPipeDirectHand.cs - 主脚本
 
 ## Port
 5056
 
 最后更新: 2026-06-21 18:25

## Auto Bone Discovery
- public GameObject handPrefab — 拖入 FBX 的字段
- [ContextMenu("Auto Assign Bones")] — 右键执行自动寻骨
- 按名称搜索 (wrist/thumb/index/middle/ring/pinky)
- 回退到层次顺序 (wrist + 5指×4骨)
- 运行时自动: 如果 handPrefab 已设置且骨骼未分配,自动实例化