# Gesture Glove UI - Unity

Unity-based hand tracking visualizer for sensor glove systems.

## Projects

- **multisensor/** - Main Unity project integrating multiple sensor sources (IMU + visual)
- **onlytip/** - Original version (2026/6/5), initial hand tracking UI
- **onlytip - 2.1/** - Updated version with visual sensor correction for host-computer virtual hand drift

## Overview

This Unity application receives real-time hand tracking data from STM32-based sensor gloves via serial/WiFi and renders a virtual hand. It integrates MediaPipe hand tracking with IMU sensor fusion to compensate for fingertip occlusion when wearing sensors.

## Hardware

- Orbbec Astra Plus (RGB + Depth)
- Wearable IMU + tactile sensor glove
- STM32H5/H7 microcontroller
