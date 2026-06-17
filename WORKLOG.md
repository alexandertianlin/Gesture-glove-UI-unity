# Work Log

## Gesture Glove UI Unity & Glove Sensor System — GitHub Migration & Debugging

**Date**: 2026-06-17
**Repos**: 
- [Gesture-glove-UI-unity](https://github.com/alexandertianlin/Gesture-glove-UI-unity)
- [Glove_sensor_system](https://github.com/alexandertianlin/Glove_sensor_system)

---

## Task 1: Upload Both Projects to GitHub

### Repo 1: Gesture-glove-UI-unity
- Contains 3 Unity projects: multisensor, onlytip, onlytip - 2.1
- Added Unity-specific .gitignore (excludes Library/, Logs/, obj/, .vs/, *.csproj, *.sln, UpgradeLog*.htm)
- Added README.md with project overview
- Pushed all source files (232 files initially, later merged with remote)

### Repo 2: Glove_sensor_system
- Contains 2 STM32 firmware projects: H523 I2C IMU-Glove, H70B WiFi 6IT-Glove
- Added STM32-specific .gitignore (excludes Debug/, build artifacts, IDE files)
- Added README.md with project overview
- Pushed all source files (335 files)

---

## Task 2: Debug "White Hand" Texture Issue on Another Host

### Problem
User reported that onlytip 2.1 shows a hand without textures (pure white) on another machine,
while the working machine shows the correct blue hologram effect.

### Root Cause Analysis

Investigated the full asset chain:

Scene -> SkinnedMeshRenderer -> Material (GUID: 6c6cc3c8...)
  -> hand-lp_Material.001.mat (same GUID)
    -> HologramShader.shader (GUID: 16cf8cbc...)
      -> Custom/BlueHologram_Final_Fixed

**Root cause**: hand-only-rig.fbx.meta had materialLocation: 1 (Embedded Materials).

With EmbeddedMaterials, Unity extracts materials from the FBX into the Library cache 
with auto-generated GUIDs. These GUIDs differ on each machine because the Library 
folder is gitignored. The scene's material reference resolves to a different GUID 
on the other machine, causing the material to fail -> renders as white.

### Fix Applied

Changed materialLocation from 1 to 0 (External Materials) in:
onlytip - 2.1/Assets/Scenes/source/hand-only-rig.fbx.meta

Commit: 13e344e — "Fix: change FBX materialLocation from Embedded to External Materials"

With External Materials mode, Unity uses the committed Hologram_Blue.mat directly,
which references HologramShader.shader. Both files are in git, so GUIDs are
consistent across all machines.

### Additional Notes

- The scene camera uses Solid Color (black) background — no webcam feed is rendered in Unity
- External camera/MediaPipe feed (if expected) runs in a separate window outside Unity
- Vision correction module listens on UDP port 5055 for external gesture detection

---

## Task 3: Configuration Documentation

Created onlytip - 2.1/SETUP.md with:
- Hardware/software requirements
- First-launch checklist (open scene, build settings, serial port, IMU mapping)
- Running instructions (glove-only and vision correction modes)
- Camera controls reference
- Troubleshooting section (white textures, serial port, vision correction)
- Full file structure reference

---

## File Completeness Check

| Asset Type        | Status | Files                          |
| ----------------- | ------ | ------------------------------ |
| C# Scripts        | OK     | 10 scripts (core + vision)     |
| Scene Files       | OK     | SampleScene.unity, sc.unity    |
| FBX Models        | OK     | hand-only-rig.fbx (2 copies)   |
| Textures (PNG)    | OK     | BaseColor, Normal, AO          |
| Shaders           | OK     | HologramShader, AAA_Hologram   |
| Materials         | OK     | Hologram_Blue.mat              |
| Post Processing   | OK     | 2 profiles                     |
| Package Manifests | OK     | manifest.json, packages-lock   |
| Project Settings  | OK     | Full ProjectSettings/ folder   |
| Documentation     | OK     | SETUP.md, README.md            |

**No files are missing from the repository.**

---

## Git Configuration

- Global user.name: Alexandertianlin
- Global user.email: alexandertianlin@gmail.com


## Quick Setup: Files Required for the Hologram Hand Effect

To reproduce the blue hologram hand on any new machine, clone the repo and verify
these files exist. All GUIDs in the .meta files must match.

### Minimum File Checklist (onlytip - 2.1)

**3D Model & Import (2 files)**
- Assets/Scenes/source/hand-only-rig.fbx          — hand rig with bone weights
- Assets/Scenes/source/hand-only-rig.fbx.meta      — must have materialLocation: 0

**Hologram Shader (2 files)**
- Assets/Scenes/textures/HologramShader.shader      — Custom/BlueHologram_Final_Fixed
- Assets/Scenes/textures/HologramShader.shader.meta — GUID: 16cf8cbcc97a3f8458824b86a3657f7f

**Backup Shader (2 files)**
- Assets/Scenes/textures/AAA_Hologram_Grid.shader
- Assets/Scenes/textures/AAA_Hologram_Grid.shader.meta

**Hologram Material (2 files)**
- Assets/Scenes/textures/Hologram_Blue.mat          — references HologramShader
- Assets/Scenes/textures/Hologram_Blue.mat.meta     — GUID: 58aafc503d0530a4eaacaed7fb6785d8

**Hand Textures (6 files)**
- Assets/Scenes/textures/hand-lp_Material.001_BaseColor.png
- Assets/Scenes/textures/hand-lp_Material.001_BaseColor.png.meta
- Assets/Scenes/textures/hand-lp_Material.001_Normal.png
- Assets/Scenes/textures/hand-lp_Material.001_Normal.png.meta
- Assets/Scenes/textures/hand-lp_Material.001_AmbientOcclusion.png
- Assets/Scenes/textures/hand-lp_Material.001_AmbientOcclusion.png.meta

**Main Scene (2 files)**
- Assets/Scenes/SampleScene.unity                  — references material & camera setup
- Assets/Scenes/SampleScene.unity.meta

**Post-Processing Profiles (4 files)**
- Assets/Scenes/SampleScene_Profiles/MotionGlove Profile.asset
- Assets/Scenes/SampleScene_Profiles/MotionGlove Profile.asset.meta
- Assets/Scenes/SampleScene_Profiles/PostProcess Profile.asset
- Assets/Scenes/SampleScene_Profiles/PostProcess Profile.asset.meta

**Project Config (2 files)**
- ProjectSettings/GraphicsSettings.asset           — Built-in Render Pipeline
- ProjectSettings/ProjectSettings.asset            — Linear color space

**Total: 22 files needed for the visual effect (excluding C# scripts)**

### Verification on New Machine

```powershell
# In repo root, verify all key files exist:
git ls-tree -r --name-only HEAD -- "onlytip - 2.1/Assets/Scenes/source/hand-only-rig.fbx" "onlytip - 2.1/Assets/Scenes/textures/HologramShader.shader" "onlytip - 2.1/Assets/Scenes/textures/Hologram_Blue.mat" "onlytip - 2.1/Assets/Scenes/SampleScene.unity"
```

### Unity Project Setup Steps

1. Unity Hub -> Open -> select `onlytip - 2.1` folder
2. Wait for Library generation (3-10 min)
3. File -> Build Settings -> Add Open Scenes (add SampleScene.unity)
4. File -> Open Scene -> select SampleScene.unity
5. Verify in Console: no pink/magenta materials, no shader errors
6. Press Play: hand should appear as blue hologram on black background

### Material Reference Chain

```
SampleScene.unity
  -> SkinnedMeshRenderer.m_Materials[0]
    -> hand-lp_Material.001.mat  (GUID: 6c6cc3c8f5bd3eb4fb5bfc655b05d825)
      -> HologramShader.shader   (GUID: 16cf8cbcc97a3f8458824b86a3657f7f)
        -> Custom/BlueHologram_Final_Fixed
          -> Textures: BaseColor.png, Normal.png, AO.png
```
