# Whisper Python Script

VoxSub 是一个多功能 Whisper 字幕工具，可以将媒体文件的音频识别成 `.srt` 字幕，也可以把已有字幕封装进 `.mkv`。安装后可直接在终端调用 `voxsub`，支持 macOS 和 Apple Silicon（M 系列，包括 M5）上的 `mps` 加速。

## 安装

推荐使用 `pipx` 安装。它会为 VoxSub 创建独立虚拟环境，同时把 `voxsub` 命令放到全局 PATH 中。这样不用每次激活虚拟环境，也不会依赖系统 Python 环境里的包。

```bash
brew install python@3.12 ffmpeg pipx
pipx ensurepath
```

第一次执行 `pipx ensurepath` 后，如果当前终端找不到 `voxsub`，请重开一个终端窗口。

安装 VoxSub：

```bash
git clone https://github.com/SIXiaolong1117/WhisperPythonScript.git
cd WhisperPythonScript
pipx install --python python3.12 .
```

> 第一次运行会下载 Whisper 模型。`large` 模型体积和内存占用较大，如果内存紧张，可以先用 `--model medium` 或 `--model small`。

安装完成后检查命令：

```bash
voxsub --help
```

更新本地代码后重新安装：

```bash
cd WhisperPythonScript
pipx reinstall voxsub
```

### 开发安装

如果你正在修改代码，也可以用可编辑安装：

```bash
cd WhisperPythonScript
pipx uninstall voxsub
pipx install --editable --python python3.12 .
```

可编辑安装后，修改 `voxsub.py` 通常会直接反映到 `voxsub` 命令中。

## 使用方法

### 统一入口

生成 `.srt` 字幕：

```bash
voxsub transcribe <媒体文件路径> --language zh-Hans
```

生成 `.srt` 并封装为 `.mkv`：

```bash
voxsub all <媒体文件路径> --language zh-Hans
```

把已有同名 `.srt` 封装到 `.mkv`：

```bash
voxsub embed <媒体文件路径>
```

把指定 `.srt` 封装到 `.mkv`：

```bash
voxsub embed <媒体文件路径> <字幕文件路径>
```

指定模型、设备和输出路径：

```bash
voxsub transcribe video.mp4 --language zh --model medium --device auto -o video.srt
```

`--device auto` 会在 Apple Silicon 上优先使用 `mps`。为了提高稳定性，脚本默认只在 CUDA 上启用 fp16；如果你确认当前 PyTorch/Whisper 组合在 MPS fp16 下表现正常，可以手动使用：

```bash
voxsub transcribe video.mp4 --device mps --fp16 true
```

### 命令说明

```bash
voxsub transcribe <媒体文件路径> [选项]
voxsub embed <媒体文件路径> [字幕文件路径] [选项]
voxsub all <媒体文件路径> [选项]
```

- `transcribe`：只生成 `.srt` 字幕。
- `embed`：跳过识别，把已有 `.srt` 封装为 `.mkv`。
- `all`：先生成 `.srt`，再封装为 `.mkv`。

常用选项：

- `--language zh-Hans`：按中文识别，并将繁体转换为简体。
- `--model medium`：指定 Whisper 模型，默认 `large`。
- `--device auto`：自动选择 `mps`、`cuda` 或 `cpu`。
- `--output-video output.mkv`：指定封装后的 MKV 输出路径。
- `--overwrite`：覆盖已有输出文件。

### 兼容旧用法

旧版统一入口参数仍然可用：

```bash
voxsub <媒体文件路径> --language zh-Hans
voxsub <媒体文件路径> --language zh-Hans --embed
voxsub <媒体文件路径> --subtitle <字幕文件路径>
```

## 开源许可

[MIT License](./LICENSE)
