#!/usr/bin/env python3

from __future__ import annotations

import argparse
import platform
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any


# Whisper 有时会把没有说话的部分识别为以下字符串，这里统一过滤掉。
BLOCKED_PHRASES = (
    "请不吝点赞 订阅 转发 打赏支持明镜与点点栏目",
)


def normalize_language(language: str | None) -> tuple[str | None, bool]:
    """把用户输入的语言参数转换成 Whisper 可识别的语言和附加处理标记。"""
    if language == "zh-Hans":
        return "zh", True
    return language, False


def torch_backends() -> tuple[Any | None, bool, bool]:
    """检测 PyTorch 以及当前机器可用的 CUDA/MPS 后端。"""
    try:
        import torch
    except ImportError:
        return None, False, False

    mps_backend = getattr(torch.backends, "mps", None)
    mps_available = mps_backend is not None and mps_backend.is_available()
    return torch, torch.cuda.is_available(), mps_available


def select_device(requested_device: str) -> str:
    """按平台和可用后端选择运行设备：用户指定 > CUDA/MPS > CPU。"""
    torch, cuda_available, mps_available = torch_backends()

    if requested_device == "cpu":
        return "cpu"
    if requested_device == "cuda":
        if not cuda_available:
            raise RuntimeError(
                "请求使用 CUDA，但当前 PyTorch 没有检测到可用的 NVIDIA GPU/CUDA。"
                "如果使用 pipx 安装 voxsub，请在 voxsub 的 pipx 环境中安装 CUDA 版 PyTorch。"
            )
        return "cuda"
    if requested_device == "mps":
        if not mps_available:
            raise RuntimeError("请求使用 MPS，但当前 PyTorch 没有检测到可用的 Apple Silicon MPS 后端。")
        return "mps"
    if requested_device != "auto":
        return requested_device

    if torch is None:
        return "cpu"

    # Windows + NVIDIA 的常见目标是 CUDA；macOS Apple Silicon 则优先 MPS。
    if platform.system() == "Darwin" and mps_available:
        return "mps"
    if cuda_available:
        return "cuda"
    if mps_available:
        return "mps"
    return "cpu"


def resolve_fp16(fp16: str, device: str) -> bool:
    """决定 Whisper 是否使用 fp16，默认只在 CUDA 上自动启用。"""
    if fp16 == "true":
        return True
    if fp16 == "false":
        return False

    # Apple Silicon 的 MPS 后端使用 fp32 更稳定；CUDA 通常可以使用 fp16。
    return device == "cuda"


def describe_device(device: str) -> str:
    """返回用于日志展示的设备说明。"""
    if device != "cuda":
        return device

    try:
        import torch

        return f"cuda ({torch.cuda.get_device_name(0)})"
    except Exception:
        return "cuda"


def ffmpeg_install_hint() -> str:
    """按当前平台给出 ffmpeg 安装提示。"""
    system = platform.system()
    if system == "Windows":
        return "Windows 可执行：winget install Gyan.FFmpeg，或使用 Scoop/Chocolatey 安装 ffmpeg 并加入 PATH。"
    if system == "Darwin":
        return "macOS 可执行：brew install ffmpeg。"
    return "Linux 可使用系统包管理器安装，例如：sudo apt install ffmpeg。"


def ensure_ffmpeg_available() -> None:
    """Whisper 转写和 MKV 封装都依赖 ffmpeg。"""
    if shutil.which("ffmpeg") is None:
        raise RuntimeError(f"未找到 ffmpeg。{ffmpeg_install_hint()}")


def make_subtitles(
    segments: list[dict[str, Any]],
    *,
    convert_to_simplified: bool,
) -> Any:
    """把 Whisper segments 转换成 pysrt 字幕对象。"""
    import pysrt

    subs = pysrt.SubRipFile()
    converter = None
    if convert_to_simplified:
        from opencc import OpenCC

        converter = OpenCC("t2s")
    subtitle_index = 1

    for segment in segments:
        text = str(segment.get("text", "")).strip()
        if not text:
            continue

        # 过滤已知无关文案，避免重复出现在导出的字幕里。
        if any(phrase in text for phrase in BLOCKED_PHRASES):
            continue

        if converter is not None:
            text = converter.convert(text)

        subs.append(
            pysrt.SubRipItem(
                index=subtitle_index,
                start=pysrt.SubRipTime(milliseconds=int(segment["start"] * 1000)),
                end=pysrt.SubRipTime(milliseconds=int(segment["end"] * 1000)),
                text=text,
            )
        )
        subtitle_index += 1

    return subs


def transcribe_to_srt(args: argparse.Namespace) -> Path:
    """读取媒体文件，调用 Whisper 识别音频并保存为 SRT。"""
    media_path = args.media.expanduser().resolve()
    if not media_path.is_file():
        raise FileNotFoundError(f"{media_path} is not a valid file.")
    ensure_ffmpeg_available()

    # whisper 和相关模型较重，放到真正转写时再导入，让 --help 等命令更快。
    import whisper

    output_srt = args.output.expanduser().resolve() if args.output else media_path.with_suffix(".srt")
    output_srt.parent.mkdir(parents=True, exist_ok=True)

    language, convert_to_simplified = normalize_language(args.language)
    device = select_device(args.device)
    fp16 = resolve_fp16(args.fp16, device)

    print(f"加载 Whisper 模型：{args.model}，设备：{describe_device(device)}，fp16：{fp16}")
    model = whisper.load_model(args.model, device=device)

    # result["segments"] 包含每段字幕的 start/end/text，是后续生成 SRT 的核心数据。
    result = model.transcribe(
        str(media_path),
        language=language,
        fp16=fp16,
        verbose=args.verbose,
    )

    subs = make_subtitles(result["segments"], convert_to_simplified=convert_to_simplified)
    subs.save(str(output_srt), encoding="utf-8")
    print(f"字幕文件已保存为：{output_srt}")
    return output_srt


def embed_subtitles(
    media_path: Path,
    subtitle_path: Path,
    output_video: Path | None,
    *,
    overwrite: bool,
    language: str | None,
) -> Path:
    """使用 ffmpeg 把 SRT 字幕流封装进 MKV，视频和音频保持 copy。"""
    ensure_ffmpeg_available()

    media_path = media_path.expanduser().resolve()
    subtitle_path = subtitle_path.expanduser().resolve()
    if not media_path.is_file():
        raise FileNotFoundError(f"{media_path} is not a valid file.")
    if not subtitle_path.is_file():
        raise FileNotFoundError(f"{subtitle_path} is not a valid file.")

    output_video = output_video.expanduser().resolve() if output_video else media_path.with_suffix(".mkv")
    output_video.parent.mkdir(parents=True, exist_ok=True)

    # -map 0:v:0 取第一个视频流，0:a? 表示有音频就带上，没有也不报错。
    # 视频/音频 copy 避免重新编码；字幕转成 srt 字幕流写入 MKV。
    command = [
        "ffmpeg",
        "-y" if overwrite else "-n",
        "-i",
        str(media_path),
        "-i",
        str(subtitle_path),
        "-map",
        "0:v:0",
        "-map",
        "0:a?",
        "-map",
        "1:s:0",
        "-c:v",
        "copy",
        "-c:a",
        "copy",
        "-c:s",
        "srt",
    ]

    if language:
        # 给字幕流写入语言元数据，播放器可据此显示字幕语言。
        command.extend(["-metadata:s:s:0", f"language={language}"])

    command.append(str(output_video))
    subprocess.run(command, check=True)
    print(f"已将字幕封装到 MKV 文件：{output_video}")
    return output_video


def add_transcribe_options(parser: argparse.ArgumentParser) -> None:
    """给 transcribe/all 命令添加 Whisper 识别相关参数。"""
    parser.add_argument("-l", "--language", help="Whisper 语言代码，例如 zh、en、ja；简体中文可用 zh-Hans")
    parser.add_argument("-m", "--model", default="large", help="Whisper 模型名，默认 large")
    parser.add_argument(
        "--device",
        default="auto",
        choices=("auto", "mps", "cpu", "cuda"),
        help="运行设备；auto 会在 Windows/NVIDIA 上使用 cuda，在 Apple Silicon 上使用 mps",
    )
    parser.add_argument(
        "--fp16",
        default="auto",
        choices=("auto", "true", "false"),
        help="是否使用 fp16；auto 在 CUDA 上启用，在 MPS/CPU 上关闭以提高稳定性",
    )
    parser.add_argument("-o", "--output", type=Path, help="输出 SRT 路径，默认与媒体同名")
    parser.add_argument("--verbose", action="store_true", help="显示 Whisper 识别进度")


def add_embed_options(parser: argparse.ArgumentParser) -> None:
    """给 embed/all 命令添加 MKV 输出相关参数。"""
    parser.add_argument("--output-video", type=Path, help="输出 MKV 路径，默认与媒体同名")
    parser.add_argument("--overwrite", action="store_true", help="覆盖已有输出文件")


def build_command_parser() -> argparse.ArgumentParser:
    """构建 CLI：transcribe、embed、all 三个子命令。"""
    parser = argparse.ArgumentParser(
        description="VoxSub：多功能 Whisper 字幕工具，生成 SRT、封装 MKV，或一步完成。"
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    transcribe_parser = subparsers.add_parser("transcribe", help="从媒体文件生成 SRT 字幕")
    transcribe_parser.add_argument("media", type=Path, help="媒体文件路径")
    add_transcribe_options(transcribe_parser)

    embed_parser = subparsers.add_parser("embed", help="把已有字幕封装进 MKV")
    embed_parser.add_argument("media", type=Path, help="媒体文件路径")
    embed_parser.add_argument("subtitle_arg", nargs="?", type=Path, help="字幕文件路径，默认使用同名 .srt")
    embed_parser.add_argument("-s", "--subtitle", type=Path, help="字幕文件路径，默认使用同名 .srt")
    embed_parser.add_argument("-l", "--language", help="字幕语言元数据，例如 zh、en、ja；zh-Hans 会写入 zh")
    add_embed_options(embed_parser)

    all_parser = subparsers.add_parser("all", help="生成 SRT 后封装为 MKV")
    all_parser.add_argument("media", type=Path, help="媒体文件路径")
    add_transcribe_options(all_parser)
    add_embed_options(all_parser)
    return parser


def subtitle_for_embed(args: argparse.Namespace) -> Path:
    """解析 embed 命令的字幕路径，未指定时默认使用媒体同名 .srt。"""
    subtitle = getattr(args, "subtitle", None) or getattr(args, "subtitle_arg", None)
    return subtitle.expanduser().resolve() if subtitle else args.media.with_suffix(".srt")


def run_command(args: argparse.Namespace) -> None:
    """执行子命令。"""
    if args.command == "transcribe":
        transcribe_to_srt(args)
        return

    if args.command == "embed":
        embed_subtitles(
            args.media,
            subtitle_for_embed(args),
            args.output_video,
            overwrite=args.overwrite,
            language=normalize_language(args.language)[0],
        )
        return

    if args.command == "all":
        subtitle_path = transcribe_to_srt(args)
        embed_subtitles(
            args.media,
            subtitle_path,
            args.output_video,
            overwrite=args.overwrite,
            language=normalize_language(args.language)[0],
        )
        return

    raise RuntimeError(f"未知命令：{args.command}")


def main(argv: list[str] | None = None) -> int:
    """程序入口：解析参数、执行任务，并把常见异常转换成命令行错误码。"""
    argv = sys.argv[1:] if argv is None else argv
    parser = build_command_parser()
    args = parser.parse_args(argv)

    try:
        run_command(args)
    except (FileNotFoundError, RuntimeError, subprocess.CalledProcessError) as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
