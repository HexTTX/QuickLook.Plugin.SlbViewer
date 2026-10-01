# QuickLook.Plugin.SlbViewer

[![QuickLook Plugin](https://img.shields.io/badge/QuickLook-Plugin-blue)](https://github.com/QL-Win/QuickLook)
[![Target](https://img.shields.io/badge/.NET-4.6.2-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

[English](#english) | [中文说明](#chinese)

---

<a name="english"></a>
## English

A lightweight, high-performance [QuickLook](https://github.com/QL-Win/QuickLook) plugin for previewing AutoCAD Slide (`.sld`) and Slide Library (`.slb`) files by pressing <kbd>Space</kbd> in Windows File Explorer.

### ✨ Features

- **AutoCAD `.sld` Single Slide Preview**:
  - High-fidelity vector rendering based on AutoCAD standard 256 ACI color table.
  - Automatic scaling and aspect-ratio preservation.
  - Shows slide dimensions and vector command count.
- **AutoCAD `.slb` Slide Library Preview**:
  - Browse through all slides inside a `.slb` library without launching AutoCAD.
  - Multi-slide navigation:
    - **Keyboard**: <kbd>←</kbd> / <kbd>→</kbd>, <kbd>PageUp</kbd> / <kbd>PageDown</kbd>, <kbd>Space</kbd>, <kbd>Home</kbd> / <kbd>End</kbd>.
    - **Mouse Wheel**: Scroll up/down to flip slides seamlessly.
    - **Toolbar**: Click Previous `◀`, Next `▶`, or select from the slide name dropdown.
- **Dark / Light Theme Toggle**:
  - Press <kbd>B</kbd> or click `🌓` to switch between classic AutoCAD dark background and clean white background.
  - Automatic contrast adjustment (ACI 7 white/black inversion).
- **Ultra Lightweight**:
  - Package size is only ~30 KB, zero third-party dependencies, purely managed GDI+ and WPF.

### 📦 Installation

#### Method 1: Spacebar One-Click Install (Recommended)
1. Download `QuickLook.Plugin.SlbViewer.qlplugin` from [Releases](https://github.com/Hexon-X/QuickLook.Plugin.SlbViewer/releases).
2. Select the downloaded file in File Explorer and press <kbd>Spacebar</kbd>.
3. Click **Install** on the QuickLook prompt, then restart QuickLook.

#### Method 2: Manual Installation
Extract or copy the contents of `QuickLook.Plugin.SlbViewer.qlplugin` (rename to `.zip` if needed) to:
```
%APPDATA%\pooi.moe\QuickLook\QuickLook.Plugin\QuickLook.Plugin.SlbViewer\
```
Then restart QuickLook.

### 🔨 Build from Source

```bash
cd QuickLook.Plugin.SlbViewer
dotnet build -c Release
powershell -ExecutionPolicy Bypass -File pack.ps1
```

---

<a name="chinese"></a>
## 中文说明

为 Windows 效率工具 [QuickLook](https://github.com/QL-Win/QuickLook) 打造的 AutoCAD 幻灯片（`.sld`）与幻灯片库（`.slb`）预览插件。在文件资源管理器中选中文件按下 <kbd>空格键</kbd> 即可秒级预览！

### ✨ 功能特性

- **支持 AutoCAD `.sld` 单张幻灯片预览**：
  - 完整实现 AutoCAD 标准 256 色（ACI）索引矢量渲染；
  - 自动保持宽高比居中显示，清晰呈现线段、点、填充等元素；
  - 标题与底栏实时显示幻灯片分辨率与矢量命令统计。
- **支持 AutoCAD `.slb` 幻灯片库多图浏览**：
  - 无需启动 AutoCAD，在资源管理器中直接浏览库中包含的全部幻灯片；
  - 便捷的翻页与跳转操作：
    - **键盘控制**：<kbd>←</kbd> / <kbd>→</kbd>、<kbd>PageUp</kbd> / <kbd>PageDown</kbd>、<kbd>空格</kbd> 快速翻页，<kbd>Home</kbd> / <kbd>End</kbd> 跳至首尾；
    - **鼠标滚轮**：在预览区直接滚动滚轮即可顺畅切图；
    - **悬浮工具栏**：支持点击上一张/下一张按钮，或在下拉菜单中直接选择指定幻灯片。
- **亮暗底色自由切换**：
  - 按 <kbd>B</kbd> 键或点击工具栏 `🌓` 按钮，一键切换 AutoCAD 经典深黑底色与打印浅白底色；
  - 支持 AutoCAD 7 号色自动黑白对比度反色。
- **极简轻量**：
  - 插件仅约 30 KB，零外部第三方动态库依赖，纯原生 WPF + GDI+ 高性能渲染。

### 📦 安装方式

#### 方式一：空格一键安装（推荐）
1. 在 [Releases](https://github.com/Hexon-X/QuickLook.Plugin.SlbViewer/releases) 页面下载 `QuickLook.Plugin.SlbViewer.qlplugin`；
2. 在 Windows 资源管理器中选中该 `.qlplugin` 文件，按下 <kbd>空格键</kbd>；
3. 点击界面中的 **Install** 按钮，安装后重启 QuickLook 即可生效。

#### 方式二：手动安装
将下载的 `.qlplugin` 文件解压至以下目录（若无则新建）：
```
%APPDATA%\pooi.moe\QuickLook\QuickLook.Plugin\QuickLook.Plugin.SlbViewer\
```
重启 QuickLook 即可。

### 🔨 从源码编译与打包

确保已安装 .NET SDK 8.0 或更高版本：
```bash
cd QuickLook.Plugin.SlbViewer
dotnet build -c Release
powershell -ExecutionPolicy Bypass -File pack.ps1
```
打包完成后将在当前目录生成 `QuickLook.Plugin.SlbViewer.qlplugin`。

---

## 📄 License

MIT License
