# Whisper Python Script

VoxSub 是一个多功能 Whisper 字幕脚本，可以将媒体文件的音频识别成 `.srt` 字幕，也可以把已有字幕封装进 `.mkv`。统一入口是 `voxsub.py`，支持 macOS 和 Apple Silicon（M 系列，包括 M5）上的 `mps` 加速。

## 依赖安装

建议使用 Python 3.11 或 3.12。Apple Silicon 上不建议直接使用系统 Python。

```bash
brew install python@3.12 ffmpeg
```

创建虚拟环境并安装依赖：

```bash
git clone https://github.com/SIXiaolong1117/WhisperPythonScript.git
cd WhisperPythonScript
python3.12 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

> 第一次运行会下载 Whisper 模型。`large` 模型体积和内存占用较大，如果内存紧张，可以先用 `--model medium` 或 `--model small`。

## 使用方法

### 统一入口

生成 `.srt` 字幕：

```bash
python voxsub.py transcribe <媒体文件路径> --language zh-Hans
```

生成 `.srt` 并封装为 `.mkv`：

```bash
python voxsub.py all <媒体文件路径> --language zh-Hans
```

把已有同名 `.srt` 封装到 `.mkv`：

```bash
python voxsub.py embed <媒体文件路径>
```

把指定 `.srt` 封装到 `.mkv`：

```bash
python voxsub.py embed <媒体文件路径> <字幕文件路径>
```

指定模型、设备和输出路径：

```bash
python voxsub.py transcribe video.mp4 --language zh --model medium --device auto -o video.srt
```

`--device auto` 会在 Apple Silicon 上优先使用 `mps`。为了提高稳定性，脚本默认只在 CUDA 上启用 fp16；如果你确认当前 PyTorch/Whisper 组合在 MPS fp16 下表现正常，可以手动使用：

```bash
python voxsub.py transcribe video.mp4 --device mps --fp16 true
```

### 命令说明

```bash
python voxsub.py transcribe <媒体文件路径> [选项]
python voxsub.py embed <媒体文件路径> [字幕文件路径] [选项]
python voxsub.py all <媒体文件路径> [选项]
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
python voxsub.py <媒体文件路径> --language zh-Hans
python voxsub.py <媒体文件路径> --language zh-Hans --embed
python voxsub.py <媒体文件路径> --subtitle <字幕文件路径>
```

## 开源许可

[MIT License](./LICENSE)
