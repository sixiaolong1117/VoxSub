# VoxSub

<div align="center">

<img src="frontend/VoxSub/Assets/voxsub-icon.svg" alt="VoxSub" width="128">

**基于 OpenAI Whisper 的字幕生成与封装工具<br/>支持 CUDA · Apple Silicon MPS · CPU · Avalonia 桌面界面**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Python](https://img.shields.io/badge/Python-3.11%2B-3776AB)](https://www.python.org/)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-12.0.3-8B5CFE)](https://avaloniaui.net/)

[English](README_EN.md) | **简体中文**

</div>

---

## 📖 简介

VoxSub 是一个轻量的 Whisper 字幕工具，用于从媒体文件生成 `.srt` 字幕，也可以把字幕封装进 `.mkv` 文件。同时提供命令行与基于 Avalonia 的桌面界面，适合日常视频字幕制作、已有字幕封装，以及需要批处理脚本接入的本地工作流。

## ✨ 功能特性

- **Whisper 转写**：支持 `tiny`、`base`、`small`、`medium`、`large`、`turbo` 等 Whisper 模型
- **多语言识别**：支持 Whisper 语言代码，例如 `zh`、`en`、`ja`、`ko`、`fr`、`de`、`es`
- **简体中文输出**：使用 `zh-Hans` 时会以 `zh` 交给 Whisper，并通过 OpenCC 转为简体中文
- **SRT 生成**：基于 Whisper segments 生成标准 `.srt`，默认输出到媒体同名文件
- **详细日志**：可通过 `--verbose` 显示 Whisper 识别进度
- **MKV 封装**：调用 FFmpeg 将 `.srt` 字幕写入 `.mkv`
- **无重编码**：视频和音频使用 copy 模式，避免不必要的画质损失和耗时
- **字幕语言元数据**：支持写入 `zh`、`en`、`ja` 等语言标记
- **支持多种设备**：`--device auto` 会优先选择当前机器可用的加速后端，支持包括 NVIDIA CUDA、Apple Silicon MPS、CPU

> --fp16 auto` 仅在 CUDA 上启用 fp16，在 MPS/CPU 上关闭以提高稳定性

## 🚀 快速开始

### 系统要求

- Python 3.11+
- FFmpeg，并确保 `ffmpeg` 位于系统 PATH
- PyTorch
- openai-whisper
- OpenCC
- pysrt
- .NET 10 SDK 或 Runtime

### 使用 pipx 安装命令行工具

如果还没有安装 `pipx`：

```bash
# Windows
py -m pip install --user pipx

# macOS
brew install pipx

# Ubuntu / Debian
python3 -m pip install --user pipx
```

推荐使用 `pipx` 将 VoxSub 安装成全局命令：

```bash
git clone https://github.com/SIXiaolong1117/VoxSub.git
cd VoxSub
pipx install --python python3.12 .
```

这里的 `python3.12` 可以替换为本机可用的 Python 3.11+ 解释器。

安装后检查：

```bash
voxsub --help
```

如果当前终端找不到 `pipx` 或 `voxsub`，请先执行：

```bash
# Windows
py -m pipx ensurepath

# macOS / Linux
pipx ensurepath
```

然后重新打开终端。

开发时可以使用可编辑安装：

```bash
pipx uninstall voxsub
pipx install --editable --python python3.12 .
```

### 安装图形界面工具

从 [Github Releases](https://github.com/sixiaolong1117/VoxSub/releases) 下载最新的 `.zip` 压缩包。解压到任意目录，运行 `VoxSub.exe`

## 命令行使用

### 生成字幕

```bash
voxsub transcribe <媒体文件> [选项]
```

示例：

```bash
voxsub transcribe video.mp4 --language zh-Hans
voxsub transcribe video.mp4 --language en --model medium -o video.srt
voxsub transcribe video.mp4 --device cuda --fp16 true --verbose
```

常用选项：

| 选项 | 说明 | 默认值 |
|------|------|--------|
| `-l, --language` | Whisper 语言代码；`zh-Hans` 会额外转为简体中文 | 自动检测 |
| `-m, --model` | Whisper 模型名 | `large` |
| `--device` | `auto`、`cuda`、`mps`、`cpu` | `auto` |
| `--fp16` | `auto`、`true`、`false` | `auto` |
| `-o, --output` | 输出 `.srt` 路径 | 媒体同名 `.srt` |
| `--verbose` | 显示 Whisper 识别进度 | 关闭 |

### 封装字幕

```bash
voxsub embed <媒体文件> [字幕文件] [选项]
```

如果不指定字幕文件，VoxSub 默认使用媒体同名 `.srt`。

示例：

```bash
voxsub embed video.mp4
voxsub embed video.mp4 subtitle.srt
voxsub embed video.mp4 -s subtitle.srt --language zh --output-video video.mkv --overwrite
```

常用选项：

| 选项 | 说明 | 默认值 |
|------|------|--------|
| `[字幕文件]` | 位置参数形式的字幕路径 | 媒体同名 `.srt` |
| `-s, --subtitle` | 显式指定字幕路径 | 媒体同名 `.srt` |
| `-l, --language` | 字幕流语言元数据；`zh-Hans` 会写入 `zh` | 不写入 |
| `--output-video` | 输出 `.mkv` 路径 | 媒体同名 `.mkv` |
| `--overwrite` | 覆盖已有输出文件 | 关闭 |

### 转写并封装

```bash
voxsub all <媒体文件> [选项]
```

`all` 会先生成 `.srt`，再将该字幕封装进 `.mkv`。

示例：

```bash
voxsub all video.mp4 --language zh-Hans --model medium --output-video video.mkv
voxsub all video.mp4 --device cuda --fp16 auto --overwrite
```

`all` 支持 `transcribe` 的转写选项，也支持 `embed` 的输出选项：

| 选项 | 说明 |
|------|------|
| `-l, --language` | 识别语言或字幕语言 |
| `-m, --model` | Whisper 模型 |
| `--device` | 运行设备 |
| `--fp16` | fp16 策略 |
| `-o, --output` | SRT 输出路径 |
| `--verbose` | Whisper 详细输出 |
| `--output-video` | MKV 输出路径 |
| `--overwrite` | 覆盖已有 MKV |

## CUDA 与 MPS

VoxSub 的设备由 `--device` 控制：

```bash
voxsub transcribe video.mp4 --device auto
voxsub transcribe video.mp4 --device cuda
voxsub transcribe video.mp4 --device mps
voxsub transcribe video.mp4 --device cpu
```

`--fp16 auto` 默认只在 CUDA 上启用 fp16：

```bash
voxsub transcribe video.mp4 --device cuda --fp16 true
voxsub transcribe video.mp4 --device mps --fp16 false
```

如果指定 `--device cuda` 后报错，常见原因是 `voxsub` 的 pipx 独立环境里安装的是 CPU 版 PyTorch。可在 `voxsub` 环境中重装 CUDA 版 PyTorch：

```bash
pipx runpip voxsub install --upgrade --force-reinstall torch --index-url https://download.pytorch.org/whl/cu121
```

CUDA 版本请以 PyTorch 官网当前安装命令为准。Windows 上可以这样检查 `voxsub` 环境中的 PyTorch：

```powershell
pipx runpip voxsub show torch
voxsub transcribe video.mp4 --device cuda
```

## 桌面图形界面

VoxSub 提供 Avalonia 桌面界面，用于在不手写命令的情况下执行同一套 CLI 能力。

### 使用流程

1. 选择任务类型：转写字幕、封装字幕或转写并封装
2. 选择媒体文件，并按需要选择字幕文件或输出路径
3. 设置语言、Whisper 模型、设备和 fp16 策略
4. 点击开始，在日志区域查看实时输出

> 图形界面本身不内置 Whisper、PyTorch 或 FFmpeg。它会优先调用系统中的 `voxsub` 命令；如果未安装 `voxsub`，则尝试使用本仓库内的 `python/voxsub.py`。脚本模式会自动在项目根目录创建或复用 `.venv` 并安装缺失依赖，不会向系统 Python 安装包。

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📄 许可证

本项目基于 [MIT 许可证](LICENSE) 开源。

## 🙏 致谢

- [OpenAI Whisper](https://github.com/openai/whisper) — 本地语音识别与转写能力
- [FFmpeg](https://ffmpeg.org/) — 多媒体处理工具套件
- [OpenCC](https://github.com/BYVoid/OpenCC) — 中文简繁转换
- [Avalonia UI](https://avaloniaui.net/) — 跨平台 .NET UI 框架
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — .NET MVVM 工具包
