[English Version](README_EN.md)

# SiYangFSM — 轻量级可扩展 Unity 运行时有限状态机框架  
无需继承 MonoBehaviour · 支持嵌套 · 运行时动态组装 · 适用于各种游戏逻辑

## 🚀 项目简介

**SiYangFSM** 是一个为 Unity 打造的轻量级有限状态机（FSM）框架，注重：

- **纯 C# 实现**，不依赖 Unity 的 MonoBehaviour  
- **支持嵌套（HFSM）**  
- **运行时动态组装**，无需继承写死类结构  
- **结构清晰、可调试、易扩展**

该框架专注于 FSM 本身，不耦合任何 Unity 系统，包括：

❌ 输入系统（Input System）  
❌ 资源管理器（如 YooAsset）  
❌ Animator / StateMachineBehaviour  
❌ 网络、AI、UI 等模块  

你可以将它用于各种流程逻辑管理。

## ✨ 功能亮点

### 1. 纯 C# FSM

### 2. 支持嵌套状态机（HFSM）

### 3. 运行时动态组装

### 4. 支持 AnyState / 优先级

### 5. 状态切换事件

## 📁 推荐目录结构

SiYangFSM/
├── Runtime/
├── Editor/

## 🧠 示例

var sm = new StateMachine("Locomotion", ctx);

## 📄 协议
MIT License
