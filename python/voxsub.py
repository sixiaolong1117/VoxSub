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
# ponytail: 硬编码常见幻觉，长静音录屏必需；日语多为结束语/订阅请求
BLOCKED_PHRASES = (
    "请不吝点赞 订阅 转发 打赏支持明镜与点点栏目",
    "明镜与点点栏目",
    "ご視聴ありがとうございました",
    "ご覧いただきありがとうございました",
    "ありがとうございました",
    "チャンネル登録お願いします",
    "チャンネル登録よろしくお願いします",
)

# 翻译子命令的语言名称映射：用户输入 → Argos Translate 内部代码。
LANG_NORMALIZE: dict[str, str] = {
    "zh": "zh",
    "zh-hans": "zh",
    "zh-cn": "zh",
    "chinese": "zh",
    "简体中文": "zh",
    "中文": "zh",
    "zh-hant": "zt",
    "zh-tw": "zt",
    "繁体中文": "zt",
    "en": "en",
    "english": "en",
    "英语": "en",
    "英文": "en",
    "ja": "ja",
    "jp": "ja",
    "japanese": "ja",
    "日语": "ja",
    "日文": "ja",
    "ko": "ko",
    "kr": "ko",
    "korean": "ko",
    "韩语": "ko",
    "韩文": "ko",
    "fr": "fr",
    "french": "fr",
    "法语": "fr",
    "de": "de",
    "german": "de",
    "德语": "de",
    "es": "es",
    "spanish": "es",
    "西班牙语": "es",
    "ru": "ru",
    "russian": "ru",
    "俄语": "ru",
    "pt": "pt",
    "portuguese": "pt",
    "葡萄牙语": "pt",
    "it": "it",
    "italian": "it",
    "意大利语": "it",
    "ar": "ar",
    "arabic": "ar",
    "阿拉伯语": "ar",
    "th": "th",
    "thai": "th",
    "泰语": "th",
    "vi": "vi",
    "vietnamese": "vi",
    "越南语": "vi",
}


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
                "如果使用桌面端 .py 脚本模式，请在项目 .venv 中安装 CUDA 版 PyTorch；"
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
    filtered_low_confidence = 0
    filtered_blocked = 0

    for segment in segments:
        text = str(segment.get("text", "")).strip()
        if not text:
            continue

        # ponytail: 长视频静音段幻觉过滤，阈值与 transcribe 保持一致；缺字段时跳过判断以兼容旧数据
        no_speech_prob = segment.get("no_speech_prob")
        if isinstance(no_speech_prob, (int, float)) and no_speech_prob > 0.6:
            filtered_low_confidence += 1
            continue
        avg_logprob = segment.get("avg_logprob")
        if isinstance(avg_logprob, (int, float)) and avg_logprob < -1.0:
            filtered_low_confidence += 1
            continue
        compression_ratio = segment.get("compression_ratio")
        if isinstance(compression_ratio, (int, float)) and compression_ratio > 2.4:
            filtered_low_confidence += 1
            continue

        # 过滤已知无关文案，避免重复出现在导出的字幕里。
        if any(phrase in text for phrase in BLOCKED_PHRASES):
            filtered_blocked += 1
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

    if filtered_low_confidence or filtered_blocked:
        print(f"已过滤 {filtered_low_confidence} 条低置信段、{filtered_blocked} 条幻觉短语")

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
    # ponytail: 抗幻觉参数硬编码，长视频大量静音必需；阈值与 make_subtitles 过滤保持一致
    result = model.transcribe(
        str(media_path),
        language=language,
        fp16=fp16,
        verbose=args.verbose,
        condition_on_previous_text=False,
        no_speech_threshold=0.6,
        compression_ratio_threshold=2.4,
        logprob_threshold=-1.0,
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


def normalize_translation_lang(user_input: str) -> str:
    """把用户输入的语言描述规范化为 Argos Translate 使用的 ISO 639-1 代码。"""
    key = user_input.strip().lower()
    if key in LANG_NORMALIZE:
        return LANG_NORMALIZE[key]
    return key


def detect_srt_language(subs: Any) -> str:
    """根据 SRT 字幕内容自动检测源语言。"""
    from langdetect import DetectorFactory, detect

    DetectorFactory.seed = 0

    texts = [sub.text.strip() for sub in subs if sub.text.strip()]
    if not texts:
        raise RuntimeError("字幕文件中没有可检测的文本内容。")

    combined = " ".join(texts[: min(len(texts), 50)])
    detected = detect(combined)
    return normalize_translation_lang(detected)


def resolve_translation_device(requested: str) -> str:
    """解析翻译设备参数，auto 时自动检测 CUDA。"""
    if requested == "cuda":
        return "cuda"
    if requested == "cpu":
        return "cpu"
    if requested != "auto":
        return requested

    try:
        import ctranslate2

        if ctranslate2.get_cuda_device_count() > 0:
            return "cuda"
    except Exception:
        pass

    return "cpu"


def load_translation_model(
    source_code: str,
    target_code: str,
    model_override: str | None,
    device: str = "cpu",
) -> Any:
    """加载 Argos Translate 翻译模型，首次使用会自动下载语言包。"""
    import os

    resolved_device = resolve_translation_device(device)
    os.environ["ARGOS_DEVICE_TYPE"] = resolved_device

    import argostranslate.package
    import argostranslate.translate

    installed_languages = argostranslate.translate.get_installed_languages()
    source_lang = next(
        (lang for lang in installed_languages if lang.code == source_code), None
    )
    target_lang = next(
        (lang for lang in installed_languages if lang.code == target_code), None
    )

    if source_lang is None or target_lang is None:
        print("正在获取可用翻译模型列表...")
        argostranslate.package.update_package_index()
        available_packages = argostranslate.package.get_available_packages()

        package_to_install = None
        for pkg in available_packages:
            if pkg.from_code == source_code and pkg.to_code == target_code:
                package_to_install = pkg
                break

        if package_to_install is None:
            raise RuntimeError(
                f"Argos Translate 不支持 {source_code} → {target_code} 的语言对。"
            )

        print(
            f"首次翻译 {source_code} → {target_code}，正在下载翻译模型（约 50-100MB）..."
        )
        package_path = package_to_install.download()
        argostranslate.package.install_from_path(package_path)

        installed_languages = argostranslate.translate.get_installed_languages()
        source_lang = next(
            (lang for lang in installed_languages if lang.code == source_code), None
        )
        target_lang = next(
            (lang for lang in installed_languages if lang.code == target_code), None
        )

    if model_override is not None:
        import argostranslate.translate as at_translate
        try:
            return at_translate.Translation(source_lang, target_lang, model_override)
        except Exception:
            pass

    translation = source_lang.get_translation(target_lang)
    if translation is None:
        raise RuntimeError(f"无法创建 {source_code} → {target_code} 的翻译器。")

    print(f"翻译引擎就绪：{source_lang.name} → {target_lang.name}")
    return translation


def translate_srt(args: argparse.Namespace) -> Path:
    """翻译 SRT 字幕文件，保持时间轴不变。"""
    import pysrt

    srt_path = args.srt.expanduser().resolve()
    if not srt_path.is_file():
        raise FileNotFoundError(f"{srt_path} 不是有效的字幕文件。")

    source_code = (
        normalize_translation_lang(args.source) if args.source else None
    )
    target_code = normalize_translation_lang(args.target or "zh")

    print(f"读取字幕：{srt_path}")
    subs = pysrt.open(str(srt_path), encoding="utf-8")

    if source_code is None:
        source_code = detect_srt_language(subs)
        print(f"自动检测到源语言代码：{source_code}")

    if source_code == target_code:
        print(f"源语言与目标语言相同（{source_code}），跳过翻译。")
        return srt_path

    # ponytail: Argos 仅支持 ja->en、en->zh 等单跳，ja->zh 需中转；失败时自动尝试经 en 枢纽
    try:
        translator = load_translation_model(
            source_code,
            target_code,
            getattr(args, "model", None),
            device=getattr(args, "translate_device", "cpu"),
        )
    except RuntimeError as exc:
        if "不支持" in str(exc) and source_code != "en" and target_code != "en":
            print(f"直译 {source_code} → {target_code} 不可用，尝试经 en 中转...")
            t1 = load_translation_model(
                source_code, "en", None, device=getattr(args, "translate_device", "cpu")
            )
            t2 = load_translation_model(
                "en", target_code, None, device=getattr(args, "translate_device", "cpu")
            )

            class _Chained:
                def translate(self, text: str) -> str:  # type: ignore[no-redef]
                    return t2.translate(t1.translate(text))

            translator = _Chained()  # type: ignore[assignment]
            print(f"翻译引擎就绪（中转）：{source_code} → en → {target_code}")
        else:
            raise

    translated_count = 0
    for sub in subs:
        text = sub.text.strip()
        if not text:
            continue
        sub.text = translator.translate(text)
        translated_count += 1

    if args.output:
        output_srt = args.output.expanduser().resolve()
    else:
        output_srt = srt_path.parent / f"{srt_path.stem}_{target_code}{srt_path.suffix}"

    output_srt.parent.mkdir(parents=True, exist_ok=True)
    subs.save(str(output_srt), encoding="utf-8")
    print(f"已翻译 {translated_count} 条字幕，保存至：{output_srt}")
    return output_srt


def add_translate_options(parser: argparse.ArgumentParser) -> None:
    """给 translate 命令添加翻译相关参数。"""
    parser.add_argument(
        "-s",
        "--source",
        help="源语言（默认自动检测），例如 en / ja / zh",
    )
    parser.add_argument(
        "-t",
        "--target",
        default="zh",
        help="目标语言（默认 zh / 简体中文）",
    )
    parser.add_argument(
        "-o",
        "--output",
        type=Path,
        help="输出 SRT 路径，默认在输入文件名后添加目标语言后缀",
    )
    parser.add_argument(
        "-d",
        "--device",
        dest="translate_device",
        default="cpu",
        choices=("cpu", "cuda", "auto"),
        help="翻译运行设备（默认 cpu）；auto 会自动检测 CUDA",
    )
    parser.add_argument(
        "-m",
        "--model",
        help="Argos Translate 自定义模型路径（通常无需指定）",
    )


def add_transcribe_options(parser: argparse.ArgumentParser) -> None:
    """给 transcribe/all 命令添加 Whisper 识别相关参数。"""
    parser.add_argument("-l", "--language", help="Whisper 语言代码，例如 zh、en、ja；简体中文可用 zh-Hans")
    parser.add_argument("-m", "--model", default="turbo", help="Whisper 模型名，默认 turbo")
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
    """构建 CLI：transcribe、embed、translate、all 四个子命令。"""
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

    translate_parser = subparsers.add_parser("translate", help="翻译 SRT 字幕文件")
    translate_parser.add_argument("srt", type=Path, help="输入 SRT 字幕文件路径")
    add_translate_options(translate_parser)

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

    if args.command == "translate":
        translate_srt(args)
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
