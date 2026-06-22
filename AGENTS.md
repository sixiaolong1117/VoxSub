# VoxSub 项目指南

## 项目结构

双栈项目：Python CLI + Avalonia .NET 桌面 GUI。

- `python/voxsub.py` — 核心 CLI，Whisper 转写 + FFmpeg 封装，单文件实现
- `frontend/VoxSub/` — Avalonia 桌面界面，调用 CLI 执行任务
- `frontend/VoxSub.Tests/` — xunit 单元测试（仅测 .NET 侧服务层）

## 构建与运行

### Python CLI

```bash
pip install -e .              # 开发安装
voxsub transcribe video.mp4 --language zh-Hans
```

无需构建步骤，Python 脚本直接运行。

### .NET 前端

```bash
dotnet restore frontend/VoxSub/VoxSub.csproj
dotnet build frontend/VoxSub/VoxSub.csproj --configuration Release
```

目标框架是 `net10.0`，需要 .NET 10 SDK。

### 测试

```bash
dotnet test frontend/VoxSub.Tests/VoxSub.Tests.csproj --configuration Release
```

测试框架是 xunit，无额外测试命令或服务依赖。

## 版本管理

版本号在两处维护，tag 推送时 CI 自动同步：
- `pyproject.toml` — Python 包版本（`version = "x.y.z"`）
- `frontend/VoxSub/VoxSub.csproj` — .NET 版本（`<Version>x.y.z.0</Version>`）

修改版本时两处都要更新。

## 架构要点

- GUI 不内嵌 Whisper/PyTorch，通过 `VoxSubProcessResolver` 找到 `voxsub` 命令或 `python/voxsub.py`，后者会自动在项目根目录创建 `.venv` 并安装依赖
- `zh-Hans` 语言参数会转为 `zh` 给 Whisper，再用 OpenCC 转简体
- `--device auto` 按平台优先选 CUDA > MPS > CPU
- FFmpeg 是硬依赖，转写和封装都需要

## 依赖

Python：`openai-whisper`, `OpenCC`, `pysrt`, `torch`
.NET：Avalonia 12.0.3, CommunityToolkit.Mvvm 8.4.1
运行时：FFmpeg（需在 PATH 中）

## 无配置项

本项目无 lint、typecheck、formatter 工具配置。无 pre-commit hooks。CI 仅构建+测试。