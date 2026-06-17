# onlytip 2.1 Setup / Configuration Guide

## Overview

This Unity project renders a 3D hand model driven by IMU glove sensor data received over serial.
Version 2.1 adds vision sensor correction via UDP from an external visual module.

## Requirements

| Component        | Detail                              |
| ---------------- | ----------------------------------- |
| Sensor Glove     | 6-point IMU + tactile glove (STM32) |
| Serial           | USB-TTL, default COM3, 460800 bps   |
| Camera (opt)     | Orbbec Astra Plus / RGB camera      |
| OS               | Windows 10/11                       |
| Unity            | **2022.3.62f3c1** exact            |
| Python (opt)     | 3.9+ for visual correction module   |

## First Launch Checklist

### 1. Open Project

Use Unity Hub to open the `onlytip - 2.1` folder.
Wait for Library folder to generate (3-10 minutes first time).

### 2. Open the Correct Scene

**File > Open Scene > Assets/Scenes/SampleScene.unity**

If you see a blank scene, you opened the wrong one or Unity failed to load it.

### 3. Add Scene to Build Settings

**File > Build Settings > Add Open Scenes**

This step is critical. The default EditorBuildSettings.asset has an empty scene list.
Without this, builds will fail and Unity may open a blank scene on first launch.

### 4. Configure Serial Port

On the Hand GameObject in Hierarchy:
- Find `Serial Receiver (Script)` component
- Set `Port Name` to your actual COM port (check Device Manager)
- Baud Rate: leave at 460800

If the port does not exist, Unity will still run but the hand will not move.

### 5. IMU Axis Mapping (usually leave as default)

On `HandMotionManager` component:
- IMU X/Y/Z to Unity axis mapping
- Thumb-specific IMU axis mapping
- These depend on your glove hardware orientation

### 6. Enable Vision Correction (optional)

On the same Hand GameObject:
- `Vision Finger Correction Receiver` > `Enable Vision Correction` = checked
- `Listen Port` = 5055
- Other parameters can stay at default

## Running

### Glove-only mode (no vision)

1. Connect glove via USB
2. Press Play in Unity
3. Console should show: "Serial COMx opened"
4. Press **Space** to calibrate IMU baseline
5. Hand should now follow glove motion

### Vision correction mode

1. Start Unity project first
2. Run external Python vision script in another terminal
3. Python script must send UDP JSON to `127.0.0.1:5055`

Expected JSON format:
```json
{
  "timestampMs": 1718000000000,
  "sequenceId": 1,
  "confidence": 0.85,
  "stableMs": 500,
  "command": "TRIGGER_OPEN",
  "gestureState": "TRIGGER_OPEN",
  "score": 0.92,
  "vis_conf": 0.88,
  "isPalmFacing": true
}
```

Supported commands: TRIGGER_OPEN, TRIGGER_FIST, FINGER_OPEN, FINGER_FIST, IDLE

## Camera Controls

| Key    | Action              |
| ------ | ------------------- |
| 1      | Default dual view   |
| 2      | Auto orbit          |
| 3      | Free control        |
| 4      | Fist close-up       |
| 5      | Hero orbit          |
| 6-0    | Finger close-ups    |
| RClick | Rotate              |
| MClick | Pan                 |
| Scroll | Zoom                |
| Space  | Pause orbit         |
| R      | Reset camera        |
| F12    | Screenshot          |
| Ctrl+S | Save camera preset  |

## Key Settings Reference

| Setting              | Value                             |
| -------------------- | --------------------------------- |
| Unity version        | 2022.3.62f3c1                     |
| Color space          | Linear                            |
| Resolution           | 1920 x 1080                       |
| Run in background    | Enabled                           |
| Scene                | Assets/Scenes/SampleScene.unity   |
| Serial baud rate     | 460800                            |
| UDP vision port      | 5055                              |

## Important: Why the Background is Black

The SampleScene camera uses **Solid Color (black)** as the clear flag.
There is NO webcam feed rendered inside Unity.
If you expect to see a camera feed background, that is rendered by the
external Python/MediaPipe program in its own window, not by Unity.

## File Structure

```
onlytip - 2.1/
  .vsconfig
  Assets/
    ForceGrid.prefab / ForceGridVisualizer.cs
    hand-only-rig.fbx
    post.cs / PostProcess.cs
    sc.unity
    Scenes/
      CameraController.cs
      FingerSolver.cs
      HandAntiClipping.cs
      HandMotionManager.cs
      SampleScene.unity
      SerialReceiver.cs
      VisionFingerCorrectionReceiver.cs
      VisionOpenPalmRefreshModule.cs
      SampleScene_Profiles/
        MotionGlove Profile.asset
        PostProcess Profile.asset
      source/
        hand-only-rig.fbx / p2.cs
      textures/
        AAA_Hologram_Grid.shader
        HologramShader.shader / Hologram_Blue.mat
        *.png (hand textures)
  Packages/
    manifest.json
  ProjectSettings/
    ProjectSettings.asset
    ProjectVersion.txt
    EditorBuildSettings.asset
    QualitySettings.asset
    GraphicsSettings.asset
  SETUP.md
```


## Troubleshooting: White / Missing Textures

If the hand model appears pure white instead of the blue hologram effect, the
material reference chain is probably fine (all GUIDs are correct in git), but
the shader or prefab connection might be broken after a fresh clone.

### Fix: Re-assign the Hologram Material

1. In the Project window, navigate to `Assets/Scenes/textures/`
2. Find `Hologram_Blue.mat`
3. In the Hierarchy window, expand the hand model object
4. For each child with a `Skinned Mesh Renderer` component:
   - Drag `Hologram_Blue.mat` into the `Materials` > `Element 0` slot
5. Save the scene (Ctrl+S)

### Fix: Verify Shader Compilation

Open the Unity Console window (`Window > General > Console`).
If you see shader compilation errors for `Custom/BlueHologram_Final_Fixed`:

1. Select `Assets/Scenes/textures/HologramShader.shader` in the Project window
2. In the Inspector, click `Compile and show code`
3. If errors appear, try these steps:
   - Ensure `Edit > Project Settings > Graphics` uses Built-in Render Pipeline
   - Check that `Player Settings > Other Settings > Color Space` is set to `Linear`
   - Reimport the shader: right-click `HologramShader.shader` > `Reimport`

### Fix: Material Location for FBX

The FBX uses `EmbeddedMaterials` mode, which can cause GUID conflicts on
other machines. To fix this in Unity:

1. Select `Assets/Scenes/source/hand-only-rig.fbx` in the Project window
2. In the Inspector, go to the `Materials` tab
3. Change `Material Location` to `Use External Materials (Legacy)`
4. Click `Extract Materials...` and save to `Assets/Scenes/source/Materials/`
5. Click `Apply`
6. Commit the new `.mat` files to git

### Verify the Full Material Chain

The expected chain is:

```
SampleScene.unity (PrefabModification)
  -> Hologram_Blue.mat (guid: 58aafc50...)
    -> HologramShader.shader (guid: 16cf8cbc...)
      -> Custom/BlueHologram_Final_Fixed
```

All three files must exist in the cloned repo. Open each in a text editor and
verify the GUID references match.
