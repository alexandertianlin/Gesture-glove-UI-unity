 # v2.2 完整数据流理解
 
 ## 场景结构 (onlytip - 2.1 / 2.2)
 
 sc.unity 有4个根物体:
 1. Main Camera (位置: 0,1,-3)
 2. Directional Light
 3. **hand-only-rig** (PrefabInstance, 位置: 0,0,2) ← 手模型已在场景中
 4. **mp** (post.cs脚本, 含旧版字段) ← 控制脚本
 
 ## 物理手套数据流 (已验证可工作)
 
 STM32 IMU手套 (6个传感器: 手掌 + 5指)
   │
   │  35字节串口帧 (device_id + int16四元数×4 + float力×3)
   ▼
 SerialReceiver.cs → ImuDataDict<int, Quaternion>
   │                   键: device_id (0x1E=拇指, 0x28=食指, ...)
   │                   值: 四元数 (传感器绝对朝向)
   ▼
 HandMotionManager.cs
   ├─ 手掌: rawPalmQ → swing-twist分解 → 限制角度 → palmBone/wristBone
   └─ 手指: inv(palmQ) * fingerQ → 相对四元数 → FingerSolver[i]
         │
         ▼
   FingerSolver.cs
     ├─ ExtractFingerAngles(): 从相对四元数提取 pitch(弯曲)/yaw(展开)
     ├─ 增量追踪: deltaPitch = rawPitch - lastRawPitch
     ├─ 累积: currentPitch += deltaPitch (带灵敏度sensitivity)
     ├─ 关节分配: rootAngle=currentPitch*rootWeight, mid=*midWeight, tip=*tipWeight
     ├─ 大拇指特殊: baseYawOffset + 反对掌 + 自动外展
     └─ 握拳边界: bendProgress插值到fistOffset
 
 ## v2.2 当前问题
 
 MediaPipeDirectHand.cs 直接旋转骨骼, 绕过整个HandMotionManager管线:
 
 D435i→MediaPipe → UDP → MediaPipeDirectHand → 骨骼 (太简单, 无IK)
 
 对比物理手套:
 STM32→串口 → SerialReceiver → ImuDataDict → HandMotionManager → FingerSolver → 骨骼 (有IK)
 
 ## 新思路: 接入现有管线
 
 让MediaPipe数据走跟物理手套相同的路径:
 
 MediaPipe 21 landmarks
   │
   │ 转换为每指相对四元数
   ▼
 ImuDataDict (模拟SerialReceiver)
   │
   ▼
 HandMotionManager (不用改)
   │
   ▼
 FingerSolver (不用改)
   │
   ▼
 3D手模型动画
 
 这样不破坏原始功能, 物理手套和MediaPipe可切换使用。
 
 ## 实施步骤
 
 1. 运行 Diagnose Bone Hierarchy, 确认骨骼层次
 2. 创建从 landmark→finger_quat 的转换算法
 3. 创建 MediaPipeImuBridge.cs (将MediaPipe数据填入ImuDataDict)
 4. 验证: 物理手套和MediaPipe都可控制同一手模型
 
 最后更新: 2026-06-21 19:45
