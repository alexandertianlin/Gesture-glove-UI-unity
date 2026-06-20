# Gesture Glove UI - Unity（手势手套 UI - Unity）

Unity-based hand tracking visualizer for sensor glove systems.
基于 Unity 的传感器手套手部追踪可视化上位机系统。

## Projects / 项目内容

- **multisensor/** - Main Unity project integrating multiple sensor sources (IMU + visual)
  主 Unity 项目，集成多传感器数据源（IMU + 视觉）
- **onlytip/** - Original version (2026/6/5), initial hand tracking UI
  初始版本，基础手部追踪 UI
- **onlytip - 2.1/** - Updated version with visual sensor correction for host-computer virtual hand drift
  更新版本，加入视觉传感器矫正，解决上位机虚拟手漂移

## Overview / 概述

This Unity application receives real-time hand tracking data from STM32-based sensor gloves via serial/WiFi and renders a virtual hand. It integrates MediaPipe hand tracking with IMU sensor fusion to compensate for fingertip occlusion when wearing sensors.

本 Unity 应用通过串口/WiFi 接收 STM32 传感器手套的实时手部追踪数据，渲染虚拟手。系统融合 MediaPipe 手部追踪与 IMU 传感器数据，补偿佩戴传感器时的指尖遮挡问题。

## Hardware / 硬件

- Orbbec Astra Plus (RGB + Depth / 深度)
- Wearable IMU + tactile sensor glove / 穿戴式 IMU + 触觉传感器手套
- STM32H5/H7 microcontroller / 微控制器
