# 手势手套 UI - Unity（Gesture Glove UI - Unity）

基于 Unity 的传感器手套手部追踪可视化上位机系统。

## 项目内容

- **multisensor/** - 主 Unity 项目，集成多传感器数据源（IMU + 视觉）
- **onlytip/** - 初始版本（2026/6/5），基础手部追踪 UI
- **onlytip - 2.1/** - 更新版本，加入视觉传感器矫正，解决上位机虚拟手漂移

## 概述

本 Unity 应用通过串口/WiFi 接收 STM32 传感器手套的实时手部追踪数据，渲染虚拟手。系统融合 MediaPipe 手部追踪与 IMU 传感器数据，补偿佩戴传感器时的指尖遮挡问题。

## 硬件

- Orbbec Astra Plus（RGB + 深度）
- 穿戴式 IMU + 触觉传感器手套
- STM32H5/H7 微控制器
