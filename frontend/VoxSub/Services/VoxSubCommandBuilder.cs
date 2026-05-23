using System.Collections.Generic;
using VoxSub.Models;

namespace VoxSub.Services;

public static class VoxSubCommandBuilder
{
    /// <summary>
    /// 把 VoxSubJob 转换成 voxsub 命令的参数列表。
    /// 返回的参数列表不含可执行文件名，只含子命令与选项。
    /// </summary>
    public static List<string> BuildArguments(VoxSubJob job)
    {
        var args = new List<string>();

        switch (job.CommandKind)
        {
            case VoxSubCommandKind.Transcribe:
                BuildTranscribeArgs(args, job);
                break;
            case VoxSubCommandKind.Embed:
                BuildEmbedArgs(args, job);
                break;
            case VoxSubCommandKind.All:
                BuildAllArgs(args, job);
                break;
        }

        return args;
    }

    private static void BuildTranscribeArgs(List<string> args, VoxSubJob job)
    {
        args.Add("transcribe");
        args.Add(job.MediaPath);

        AddLanguageArg(args, job.Language);
        AddOptionalArg(args, "--model", job.Model);
        AddOptionalArg(args, "--device", job.Device);
        AddOptionalArg(args, "--fp16", job.Fp16);
        AddOptionalArg(args, "--output", job.OutputSrtPath);

        if (job.Verbose)
            args.Add("--verbose");
    }

    private static void BuildEmbedArgs(List<string> args, VoxSubJob job)
    {
        args.Add("embed");
        args.Add(job.MediaPath);

        if (!string.IsNullOrWhiteSpace(job.SubtitlePath))
            args.Add(job.SubtitlePath);

        AddLanguageArg(args, job.Language);
        AddOptionalArg(args, "--output-video", job.OutputVideoPath);

        if (job.Overwrite)
            args.Add("--overwrite");
    }

    private static void BuildAllArgs(List<string> args, VoxSubJob job)
    {
        args.Add("all");
        args.Add(job.MediaPath);

        AddLanguageArg(args, job.Language);
        AddOptionalArg(args, "--model", job.Model);
        AddOptionalArg(args, "--device", job.Device);
        AddOptionalArg(args, "--fp16", job.Fp16);
        AddOptionalArg(args, "--output", job.OutputSrtPath);
        AddOptionalArg(args, "--output-video", job.OutputVideoPath);

        if (job.Verbose)
            args.Add("--verbose");
        if (job.Overwrite)
            args.Add("--overwrite");
    }

    /// <summary>
    /// 仅在 value 非 null 且非空白时追加 --flag value 两个参数。
    /// </summary>
    private static void AddOptionalArg(List<string> args, string flag, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        args.Add(flag);
        args.Add(value!);
    }

    private static void AddLanguageArg(List<string> args, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value.Trim(), "auto", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AddOptionalArg(args, "--language", value.Trim());
    }
}
