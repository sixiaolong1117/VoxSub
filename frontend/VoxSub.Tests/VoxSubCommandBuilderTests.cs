using System.Collections.Generic;
using VoxSub.Models;
using VoxSub.Services;
using Xunit;

namespace VoxSub.Tests;

public class VoxSubCommandBuilderTests
{
    // ============ Transcribe ============

    [Fact]
    public void Transcribe_Minimal_OnlyRequiredArgs()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.Transcribe,
            MediaPath = @"C:\video.mp4"
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        Assert.Equal(new[] { "transcribe", @"C:\video.mp4" }, args);
    }

    [Fact]
    public void Transcribe_AllOptionalArgs()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.Transcribe,
            MediaPath = @"D:\电影\视频.mp4",
            Language = "zh",
            Model = "medium",
            Device = "cuda",
            Fp16 = "true",
            OutputSrtPath = @"D:\输出\字幕.srt",
            Verbose = true
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        Assert.Contains("transcribe", args);
        Assert.Contains(@"D:\电影\视频.mp4", args);
        Assert.Contains("--language", args);
        Assert.Contains("zh", args);
        Assert.Contains("--model", args);
        Assert.Contains("medium", args);
        Assert.Contains("--device", args);
        Assert.Contains("cuda", args);
        Assert.Contains("--fp16", args);
        Assert.Contains("true", args);
        Assert.Contains("--output", args);
        Assert.Contains(@"D:\输出\字幕.srt", args);
        Assert.Contains("--verbose", args);
    }

    [Fact]
    public void Transcribe_NoVerbose_FlagAbsent()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.Transcribe,
            MediaPath = @"C:\video.mp4",
            Verbose = false
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        Assert.DoesNotContain("--verbose", args);
    }

    [Fact]
    public void Transcribe_EmptyLanguage_NotAdded()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.Transcribe,
            MediaPath = @"C:\video.mp4",
            Language = "  "
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        Assert.DoesNotContain("--language", args);
    }

    [Fact]
    public void Transcribe_AllNullOptionals_OnlyRequiredArgs()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.Transcribe,
            MediaPath = @"C:\media.mkv"
            // Language, Model, Device, Fp16, OutputSrtPath = null
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        Assert.Equal(2, args.Count);
        Assert.Equal("transcribe", args[0]);
        Assert.Equal(@"C:\media.mkv", args[1]);
    }

    // ============ Embed ============

    [Fact]
    public void Embed_Minimal_OnlyRequiredArgs()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.Embed,
            MediaPath = @"C:\video.mp4"
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        Assert.Equal(new[] { "embed", @"C:\video.mp4" }, args);
    }

    [Fact]
    public void Embed_WithSubtitlePath()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.Embed,
            MediaPath = @"C:\video.mp4",
            SubtitlePath = @"C:\subtitle.srt"
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        Assert.Contains("embed", args);
        Assert.Contains(@"C:\video.mp4", args);
        Assert.Contains(@"C:\subtitle.srt", args);
    }

    [Fact]
    public void Embed_EmptySubtitlePath_NotAdded()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.Embed,
            MediaPath = @"C:\video.mp4",
            SubtitlePath = ""
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        // 不应把空字符串作为位置参数加入
        Assert.Equal(2, args.Count);
    }

    [Fact]
    public void Embed_AllParams()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.Embed,
            MediaPath = @"C:\video.mp4",
            SubtitlePath = @"C:\字幕.srt",
            Language = "ja",
            OutputVideoPath = @"C:\output.mkv",
            Overwrite = true
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        Assert.Contains("embed", args);
        Assert.Contains(@"C:\video.mp4", args);
        Assert.Contains(@"C:\字幕.srt", args);
        Assert.Contains("--language", args);
        Assert.Contains("ja", args);
        Assert.Contains("--output-video", args);
        Assert.Contains(@"C:\output.mkv", args);
        Assert.Contains("--overwrite", args);
    }

    [Fact]
    public void Embed_OverwriteFalse_FlagAbsent()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.Embed,
            MediaPath = @"C:\video.mp4",
            Overwrite = false
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        Assert.DoesNotContain("--overwrite", args);
    }

    // ============ All ============

    [Fact]
    public void All_Minimal_OnlyRequiredArgs()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.All,
            MediaPath = @"C:\video.mp4"
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        Assert.Equal(new[] { "all", @"C:\video.mp4" }, args);
    }

    [Fact]
    public void All_FullParams()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.All,
            MediaPath = @"C:\video.mp4",
            Language = "en",
            Model = "large",
            Device = "cpu",
            Fp16 = "false",
            OutputSrtPath = @"C:\out.srt",
            OutputVideoPath = @"C:\out.mkv",
            Verbose = true,
            Overwrite = true
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        Assert.Contains("all", args);
        Assert.Contains(@"C:\video.mp4", args);
        Assert.Contains("--language", args);
        Assert.Contains("en", args);
        Assert.Contains("--model", args);
        Assert.Contains("large", args);
        Assert.Contains("--device", args);
        Assert.Contains("cpu", args);
        Assert.Contains("--fp16", args);
        Assert.Contains("false", args);
        Assert.Contains("--output", args);
        Assert.Contains(@"C:\out.srt", args);
        Assert.Contains("--output-video", args);
        Assert.Contains(@"C:\out.mkv", args);
        Assert.Contains("--verbose", args);
        Assert.Contains("--overwrite", args);
    }

    [Fact]
    public void All_NoVerboseNoOverwrite_FlagsAbsent()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.All,
            MediaPath = @"C:\video.mp4",
            Verbose = false,
            Overwrite = false
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        Assert.DoesNotContain("--verbose", args);
        Assert.DoesNotContain("--overwrite", args);
    }

    // ============ Edge Cases ============

    [Fact]
    public void PathWithSpaces_Preserved()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.Transcribe,
            MediaPath = @"C:\My Videos\movie file.mp4",
            OutputSrtPath = @"C:\My Subtitles\output file.srt"
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        // 参数完整不因为空格丢失
        Assert.Equal(4, args.Count);
        Assert.Contains(@"C:\My Videos\movie file.mp4", args);
        Assert.Contains(@"C:\My Subtitles\output file.srt", args);
    }

    [Fact]
    public void PathWithChinese_Preserved()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.All,
            MediaPath = @"D:\电影\阿凡达.mp4",
            Language = "zh-Hans",
            OutputSrtPath = @"D:\字幕\阿凡达.srt",
            OutputVideoPath = @"D:\输出\阿凡达.mkv"
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        Assert.Contains(@"D:\电影\阿凡达.mp4", args);
        Assert.Contains("zh-Hans", args);
        Assert.Contains(@"D:\字幕\阿凡达.srt", args);
        Assert.Contains(@"D:\输出\阿凡达.mkv", args);
    }

    [Fact]
    public void EmptyAndWhitespaceStrings_NotAdded()
    {
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.Transcribe,
            MediaPath = @"C:\v.mp4",
            Language = null,
            Model = "  ",
            Device = "",
            Fp16 = null,
            OutputSrtPath = null
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        // 除 transcribe + media 外没有额外参数
        Assert.Equal(2, args.Count);
        Assert.DoesNotContain("--language", args);
        Assert.DoesNotContain("--model", args);
        Assert.DoesNotContain("--device", args);
        Assert.DoesNotContain("--fp16", args);
        Assert.DoesNotContain("--output", args);
    }

    [Fact]
    public void ArgumentsMappedToOneToOneAfterFlag()
    {
        // 确保 --flag value 成对出现，value 不会变成下一个 flag
        var job = new VoxSubJob
        {
            CommandKind = VoxSubCommandKind.All,
            MediaPath = @"C:\v.mp4",
            Language = "en",
            Model = "small",
            Device = "cuda",
            Verbose = true
        };

        var args = VoxSubCommandBuilder.BuildArguments(job);

        int langIdx = args.IndexOf("--language");
        int modelIdx = args.IndexOf("--model");
        int deviceIdx = args.IndexOf("--device");

        Assert.True(langIdx >= 0);
        Assert.True(modelIdx >= 0);
        Assert.True(deviceIdx >= 0);

        Assert.Equal("en", args[langIdx + 1]);
        Assert.Equal("small", args[modelIdx + 1]);
        Assert.Equal("cuda", args[deviceIdx + 1]);
    }

    [Fact]
    public void AllCommands_MediaPathAlwaysFirstPositional()
    {
        var jobs = new[]
        {
            new VoxSubJob { CommandKind = VoxSubCommandKind.Transcribe, MediaPath = @"C:\a.mp4" },
            new VoxSubJob { CommandKind = VoxSubCommandKind.Embed, MediaPath = @"C:\b.mkv" },
            new VoxSubJob { CommandKind = VoxSubCommandKind.All, MediaPath = @"C:\c.avi" },
        };

        foreach (var job in jobs)
        {
            var args = VoxSubCommandBuilder.BuildArguments(job);
            var cmd = job.CommandKind switch
            {
                VoxSubCommandKind.Transcribe => "transcribe",
                VoxSubCommandKind.Embed => "embed",
                VoxSubCommandKind.All => "all",
                _ => "?"
            };

            Assert.Equal(cmd, args[0]);
            Assert.Equal(job.MediaPath, args[1]);
        }
    }
}