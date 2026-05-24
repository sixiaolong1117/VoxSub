# VoxSub

<div align="center">

<img src="frontend/VoxSub/Assets/voxsub-icon.svg" alt="VoxSub" width="128">

**An OpenAI Whisper-powered subtitle generation and embedding tool<br/>CUDA | Apple Silicon MPS | CPU | Avalonia desktop UI**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Python](https://img.shields.io/badge/Python-3.11%2B-3776AB)](https://www.python.org/)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-12.0.3-8B5CFE)](https://avaloniaui.net/)

**English** | [简体中文](README.md)

</div>

---

## 📖 Introduction

VoxSub is a lightweight Whisper subtitle tool for generating `.srt` subtitles from media files and embedding subtitles into `.mkv` files. It provides both a command-line interface and an Avalonia desktop UI, suitable for everyday video subtitle creation, embedding existing subtitles, and local workflows that require batch-script integration.

## ✨ Features

- **Whisper transcription**: supports Whisper models such as `tiny`, `base`, `small`, `medium`, `large`, and `turbo`
- **Multilingual recognition**: supports Whisper language codes such as `zh`, `en`, `ja`, `ko`, `fr`, `de`, and `es`
- **Simplified Chinese output**: `zh-Hans` is passed to Whisper as `zh`, then converted to Simplified Chinese with OpenCC
- **SRT generation**: creates standard `.srt` files from Whisper segments, defaulting to the same basename as the media file
- **Verbose logs**: use `--verbose` to show Whisper transcription progress
- **MKV embedding**: uses FFmpeg to write `.srt` subtitles into `.mkv` files
- **No re-encoding**: video and audio streams are copied to avoid unnecessary quality loss and processing time
- **Subtitle language metadata**: supports language tags such as `zh`, `en`, and `ja`
- **Multi-device support**: `--device auto` automatically selects the best available acceleration backend — NVIDIA CUDA, Apple Silicon MPS, or CPU

> `--fp16 auto` enables fp16 only on CUDA, disables on MPS/CPU for improved stability

## 🚀 Quick Start

### Requirements

- Python 3.11+
- FFmpeg, with `ffmpeg` available on the system PATH
- PyTorch
- openai-whisper
- OpenCC
- pysrt
- .NET 10 SDK or Runtime

### Install the CLI Tool with pipx

If `pipx` is not installed yet:

```bash
# Windows
py -m pip install --user pipx

# macOS
brew install pipx

# Ubuntu / Debian
python3 -m pip install --user pipx
```

Using `pipx` is recommended so VoxSub can be installed as a global command:

```bash
git clone https://github.com/SIXiaolong1117/VoxSub.git
cd VoxSub
pipx install --python python3.12 .
```

You can replace `python3.12` with any available Python 3.11+ interpreter on your machine.

Check the installation:

```bash
voxsub --help
```

If the current terminal cannot find `pipx` or `voxsub`, run:

```bash
# Windows
py -m pipx ensurepath

# macOS / Linux
pipx ensurepath
```

Then reopen the terminal.

For development, use an editable install:

```bash
pipx uninstall voxsub
pipx install --editable --python python3.12 .
```

### Install the Desktop GUI

Download the latest `.zip` package from [GitHub Releases](https://github.com/sixiaolong1117/VoxSub/releases). Extract it to any directory and run `VoxSub.exe`.

## Command-Line Usage

### Generate Subtitles

```bash
voxsub transcribe <media-file> [options]
```

Examples:

```bash
voxsub transcribe video.mp4 --language zh-Hans
voxsub transcribe video.mp4 --language en --model medium -o video.srt
voxsub transcribe video.mp4 --device cuda --fp16 true --verbose
```

Common options:

| Option | Description | Default |
|--------|-------------|---------|
| `-l, --language` | Whisper language code; `zh-Hans` also converts output to Simplified Chinese | Auto-detect |
| `-m, --model` | Whisper model name | `large` |
| `--device` | `auto`, `cuda`, `mps`, or `cpu` | `auto` |
| `--fp16` | `auto`, `true`, or `false` | `auto` |
| `-o, --output` | Output `.srt` path | Same-name `.srt` |
| `--verbose` | Show Whisper transcription progress | Off |

### Embed Subtitles

```bash
voxsub embed <media-file> [subtitle-file] [options]
```

If no subtitle file is specified, VoxSub uses a same-name `.srt` next to the media file.

Examples:

```bash
voxsub embed video.mp4
voxsub embed video.mp4 subtitle.srt
voxsub embed video.mp4 -s subtitle.srt --language zh --output-video video.mkv --overwrite
```

Common options:

| Option | Description | Default |
|--------|-------------|---------|
| `[subtitle-file]` | Subtitle path as a positional argument | Same-name `.srt` |
| `-s, --subtitle` | Explicit subtitle path | Same-name `.srt` |
| `-l, --language` | Subtitle stream language metadata; `zh-Hans` is written as `zh` | Not written |
| `--output-video` | Output `.mkv` path | Same-name `.mkv` |
| `--overwrite` | Overwrite existing output files | Off |

### Transcribe and Embed

```bash
voxsub all <media-file> [options]
```

`all` first generates an `.srt` file, then embeds it into an `.mkv` file.

Examples:

```bash
voxsub all video.mp4 --language zh-Hans --model medium --output-video video.mkv
voxsub all video.mp4 --device cuda --fp16 auto --overwrite
```

`all` supports transcription options from `transcribe` and output options from `embed`:

| Option | Description |
|--------|-------------|
| `-l, --language` | Recognition language or subtitle language |
| `-m, --model` | Whisper model |
| `--device` | Runtime device |
| `--fp16` | fp16 policy |
| `-o, --output` | SRT output path |
| `--verbose` | Verbose Whisper output |
| `--output-video` | MKV output path |
| `--overwrite` | Overwrite an existing MKV |

## CUDA and MPS

VoxSub selects the runtime device with `--device`:

```bash
voxsub transcribe video.mp4 --device auto
voxsub transcribe video.mp4 --device cuda
voxsub transcribe video.mp4 --device mps
voxsub transcribe video.mp4 --device cpu
```

`--fp16 auto` enables fp16 only on CUDA by default:

```bash
voxsub transcribe video.mp4 --device cuda --fp16 true
voxsub transcribe video.mp4 --device mps --fp16 false
```

If `--device cuda` fails, the most common cause is that the isolated `pipx` environment for `voxsub` has a CPU-only PyTorch build installed. Reinstall a CUDA-enabled PyTorch build inside the `voxsub` environment:

```bash
pipx runpip voxsub install --upgrade --force-reinstall torch --index-url https://download.pytorch.org/whl/cu121
```

Choose the CUDA version according to the current installation command from the PyTorch website. On Windows, you can check PyTorch inside the `voxsub` environment with:

```powershell
pipx runpip voxsub show torch
voxsub transcribe video.mp4 --device cuda
```

## Desktop GUI

VoxSub provides an Avalonia desktop UI for executing the same CLI capabilities without writing commands by hand.

### Workflow

1. Choose a task type: transcribe subtitles, embed subtitles, or transcribe and embed
2. Choose a media file, and optionally choose subtitle or output paths
3. Set the language, Whisper model, device, and fp16 policy
4. Click start and watch the live output in the log area

> The desktop UI does not bundle Whisper, PyTorch, or FFmpeg. It calls the system `voxsub` command first; if `voxsub` is not installed, it tries to use this repository's `python/voxsub.py`. Script mode automatically creates or reuses a project-local `.venv` and installs missing dependencies, without installing packages into the system Python.

## 🤝 Contributing

Issues and Pull Requests are welcome!

## 📄 License

This project is open source under the [MIT License](LICENSE).

## 🙏 Acknowledgements

- [OpenAI Whisper](https://github.com/openai/whisper) — local speech recognition and transcription
- [FFmpeg](https://ffmpeg.org/) — multimedia processing toolkit
- [OpenCC](https://github.com/BYVoid/OpenCC) — Chinese simplified/traditional conversion
- [Avalonia UI](https://avaloniaui.net/) — cross-platform .NET UI framework
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — .NET MVVM toolkit
