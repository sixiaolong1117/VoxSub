# VoxSub

VoxSub 是一个 Whisper 字幕命令行工具，用于：

- 从媒体文件生成 `.srt` 字幕
- 把已有 `.srt` 字幕封装进 `.mkv`
- 一步完成转写和封装

支持的运行设备：

- `cuda`：NVIDIA GPU
- `mps`：Apple Silicon
- `cpu`：通用 CPU 回退

默认 `--device auto` 会自动选择可用设备。macOS 上优先使用 `mps`，Windows 上检测到 CUDA 时使用 `cuda`，否则使用 `cpu`。

## 依赖

- Python 3.11+
- ffmpeg
- PyTorch
- openai-whisper
- OpenCC
- pysrt

Python 依赖由项目自动安装；`ffmpeg` 需要先安装到系统 PATH。

安装 ffmpeg：

```bash
# macOS
brew install ffmpeg

# Windows
winget install Gyan.FFmpeg
```

## 使用 pipx 安装

先安装 `pipx`：

```bash
# macOS
brew install pipx

# Windows
py -m pip install --user pipx
```

安装后执行：

```bash
# macOS
pipx ensurepath

# Windows
py -m pipx ensurepath
```

如果当前终端仍然找不到 `pipx` 或 `voxsub`，请重开一个终端窗口。

推荐用 `pipx` 把 VoxSub 安装成全局命令：

```bash
git clone https://github.com/SIXiaolong1117/VoxSub.git
cd VoxSub
pipx install --python python3.12 .
```

安装后检查：

```bash
voxsub --help
```

如果正在开发或修改本项目，可以使用可编辑安装：

```bash
pipx uninstall voxsub   # 卸载之前的安装
pipx install --editable --python python3.12 .
```

## CUDA 和 MPS

VoxSub 的设备选择由 `--device` 控制：

```bash
voxsub transcribe video.mp4 --device auto
voxsub transcribe video.mp4 --device cuda
voxsub transcribe video.mp4 --device mps
voxsub transcribe video.mp4 --device cpu
```

`--fp16 auto` 默认只在 CUDA 上启用 fp16，在 MPS/CPU 上关闭以提高稳定性。也可以手动指定：

```bash
voxsub transcribe video.mp4 --device cuda --fp16 true
voxsub transcribe video.mp4 --device mps --fp16 false
```

如果 `--device cuda` 报错，通常不是显卡没有 CUDA，而是 `voxsub` 的 pipx 独立环境里安装的 PyTorch 不支持 CUDA。先在 `voxsub` 环境中重装 CUDA 版 PyTorch，例如：

```bash
pipx runpip voxsub install --upgrade --force-reinstall torch --index-url https://download.pytorch.org/whl/cu121
```

具体 CUDA 版本请以 PyTorch 官网给出的安装命令为准。

Windows 上可以这样检查 `voxsub` 环境里的 PyTorch 是否能看到 CUDA：

```powershell
pipx runpip voxsub show torch
pipx runpip voxsub install --upgrade --force-reinstall torch --index-url https://download.pytorch.org/whl/cu121
voxsub transcribe video.mp4 --device cuda
```

如果仍然不可用，请确认 NVIDIA 驱动正常，并且安装的 CUDA 版 PyTorch 与当前 Python 版本兼容。

## 命令

### 生成字幕

```bash
voxsub transcribe <媒体文件> [选项]
```

示例：

```bash
voxsub transcribe video.mp4 --language zh-Hans
voxsub transcribe video.mp4 --language en --model medium -o video.srt
```

`transcribe` 常用选项：

- `-l, --language`：Whisper 语言代码，例如 `zh`、`en`、`ja`；`zh-Hans` 会额外把中文转为简体。
- `-m, --model`：Whisper 模型名，默认 `large`。
- `--device`：`auto`、`cuda`、`mps` 或 `cpu`，默认 `auto`。
- `--fp16`：`auto`、`true` 或 `false`，默认 `auto`。
- `-o, --output`：输出 `.srt` 路径，默认与媒体同名。
- `--verbose`：显示 Whisper 识别进度。

### 封装字幕

```bash
voxsub embed <媒体文件> [字幕文件] [选项]
```

如果不指定字幕文件，默认使用媒体同名 `.srt`。

示例：

```bash
voxsub embed video.mp4
voxsub embed video.mp4 subtitle.srt
voxsub embed video.mp4 -s subtitle.srt --output-video video.mkv --overwrite
```

`embed` 常用选项：

- `[字幕文件]` 或 `-s, --subtitle`：要封装的 `.srt` 文件。
- `-l, --language`：写入字幕流语言元数据；`zh-Hans` 会写入 `zh`。
- `--output-video`：输出 `.mkv` 路径，默认与媒体同名。
- `--overwrite`：覆盖已有输出文件。

### 转写并封装

```bash
voxsub all <媒体文件> [选项]
```

`all` 会先生成 `.srt`，再把它封装进 `.mkv`。

示例：

```bash
voxsub all video.mp4 --language zh-Hans --model medium --output-video video.mkv
```

`all` 支持 `transcribe` 的转写选项，也支持 `embed` 的输出选项：

- `-l, --language`
- `-m, --model`
- `--device`
- `--fp16`
- `-o, --output`
- `--verbose`
- `--output-video`
- `--overwrite`

## 许可

[MIT License](./LICENSE)
