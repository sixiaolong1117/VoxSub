# VoxSub

<div align="center">

<img src="frontend/VoxSub/Assets/voxsub-icon.svg" alt="VoxSub" width="128">

**基于 OpenAI Whisper 的字幕生成与封装工具<br/>支持 CUDA · Apple Silicon MPS · CPU · Avalonia 桌面界面**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Python](https://img.shields.io/badge/Python-3.11%2B-3776AB)](pyproject.toml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](frontend/VoxSub/VoxSub.csproj)
[![Avalonia](https://img.shields.io/badge/Avalonia-12.0.3-8B5CFE)](https://avaloniaui.net/)

[English](README_EN.md) | **简体中文**

</div>

---

## 简介

VoxSub 是一个轻量的 Whisper 字幕工具，用于从媒体文件生成 `.srt` 字幕，并把字幕封装进 `.mkv` 文件。它同时提供命令行与 Avalonia 桌面界面，适合日常视频字幕制作、已有字幕封装，以及需要批处理脚本接入的本地工作流。

VoxSub 的核心目标很直接：

- 从视频或音频中识别语音并生成 UTF-8 `.srt` 字幕
- 将已有 `.srt` 字幕作为字幕流封装进 `.mkv`
- 一条命令完成转写与封装
- 自动选择可用计算设备，在 CUDA、MPS、CPU 之间回退
- 对 `zh-Hans` 输出执行简体中文转换

## 功能特性

### 字幕生成

- **Whisper 转写**：支持 `tiny`、`base`、`small`、`medium`、`large`、`turbo` 等 Whisper 模型
- **多语言识别**：支持 Whisper 语言代码，例如 `zh`、`en`、`ja`、`ko`、`fr`、`de`、`es`
- **简体中文输出**：使用 `zh-Hans` 时会以 `zh` 交给 Whisper，并通过 OpenCC 转为简体中文
- **SRT 生成**：基于 Whisper segments 生成标准 `.srt`，默认输出到媒体同名文件
- **详细日志**：可通过 `--verbose` 显示 Whisper 识别进度

### 字幕封装

- **MKV 封装**：调用 FFmpeg 将 `.srt` 字幕写入 `.mkv`
- **无重编码**：视频和音频使用 copy 模式，避免不必要的画质损失和耗时
- **字幕语言元数据**：支持写入 `zh`、`en`、`ja` 等语言标记
- **同名字幕约定**：未指定字幕文件时，默认使用媒体同名 `.srt`
- **覆盖控制**：默认不覆盖已有输出，可通过 `--overwrite` 显式覆盖

### 设备与稳定性

- **自动设备选择**：`--device auto` 会优先选择当前机器可用的加速后端
- **NVIDIA CUDA**：Windows 或 Linux 上检测到 CUDA 版 PyTorch 时使用 `cuda`
- **Apple Silicon MPS**：macOS 上检测到 MPS 时优先使用 `mps`
- **CPU 回退**：没有可用加速后端时自动使用 `cpu`
- **fp16 自动策略**：`--fp16 auto` 仅在 CUDA 上启用 fp16，在 MPS/CPU 上关闭以提高稳定性

### 桌面界面

- **三种任务模式**：转写字幕、封装字幕、转写并封装
- **文件选择器**：选择媒体、字幕、SRT 输出、MKV 输出路径
- **参数面板**：配置语言、模型、设备、fp16、详细输出和覆盖策略
- **实时日志**：显示 `voxsub` 或 `python/voxsub.py` 的 stdout/stderr 输出
- **任务取消**：运行期间可取消当前任务，并结束进程树
- **工具链回退**：优先调用已安装的 `voxsub` 命令，找不到时回退到仓库内 `python/voxsub.py`

## 快速开始

### 系统要求

- Python 3.11+
- FFmpeg，并确保 `ffmpeg` 位于系统 PATH
- PyTorch
- openai-whisper
- OpenCC
- pysrt

桌面界面还需要：

- .NET 10 SDK 或 Runtime
- 支持 Avalonia 的 Windows、macOS 或 Linux 桌面环境

安装 FFmpeg：

```bash
# Windows
winget install Gyan.FFmpeg

# macOS
brew install ffmpeg

# Ubuntu / Debian
sudo apt install ffmpeg
```

### 使用 pipx 安装

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

### 运行

需要 .NET 10 SDK：

```bash
dotnet build frontend/VoxSub
dotnet run --project frontend/VoxSub
```

### 使用流程

1. 选择任务类型：转写字幕、封装字幕或转写并封装
2. 选择媒体文件，并按需要选择字幕文件或输出路径
3. 设置语言、Whisper 模型、设备和 fp16 策略
4. 点击开始，在日志区域查看实时输出
5. 需要中断时点击取消

图形界面本身不内置 Whisper、PyTorch 或 FFmpeg。它会优先调用系统中的 `voxsub` 命令；如果未安装 `voxsub`，则尝试使用本仓库内的 `python/voxsub.py`。

## 技术架构

| 模块 | 说明 |
|------|------|
| `python/voxsub.py` | CLI 主入口，负责参数解析、Whisper 调用、SRT 生成和 FFmpeg 封装 |
| `python/requirements.txt` | Python 运行依赖 |
| `pyproject.toml` | Python 包元数据与 `voxsub` 命令入口 |
| `frontend/VoxSub` | Avalonia 桌面应用 |
| `frontend/VoxSub.Tests` | 命令参数构建等单元测试 |

关键依赖：

- **OpenAI Whisper**：语音识别与分段结果
- **PyTorch**：CUDA、MPS、CPU 推理后端
- **OpenCC**：`zh-Hans` 简体中文转换
- **pysrt**：SRT 字幕文件生成
- **FFmpeg**：媒体读取与字幕封装
- **Avalonia UI 12.0.3**：跨平台桌面界面
- **CommunityToolkit.Mvvm 8.4.1**：桌面端 MVVM 支持

## 开发与测试

运行 Python CLI：

```bash
python python/voxsub.py --help
```

运行桌面端测试：

```bash
dotnet test frontend/VoxSub.Tests
```

## 贡献

欢迎提交 Issue 和 Pull Request。建议在提交改动前至少确认：

- `voxsub --help` 或 `python python/voxsub.py --help` 可以正常运行
- 修改 CLI 参数时同步更新 Avalonia 命令构建逻辑和测试
- 修改桌面端行为时运行 `dotnet test frontend/VoxSub.Tests`

## 许可证

本项目基于 [MIT 许可证](LICENSE) 开源。

## 致谢

- [OpenAI Whisper](https://github.com/openai/whisper) — 本地语音识别与转写能力
- [FFmpeg](https://ffmpeg.org/) — 多媒体处理工具套件
- [OpenCC](https://github.com/BYVoid/OpenCC) — 中文简繁转换
- [Avalonia UI](https://avaloniaui.net/) — 跨平台 .NET UI 框架
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — .NET MVVM 工具包
