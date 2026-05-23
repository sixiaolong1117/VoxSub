using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VoxSub.Services;

public sealed class VoxSubRunner
{
    /// <summary>
    /// 运行 voxsub 进程，实时回调 stdout 和 stderr 的输出。
    /// </summary>
    /// <param name="executable">可执行文件路径（python 或 voxsub）。</param>
    /// <param name="arguments">传递给可执行文件的参数列表。</param>
    /// <param name="onStdOut">stdout 行回调。</param>
    /// <param name="onStdErr">stderr 行回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>进程退出码。</returns>
    public static async Task<int> RunAsync(
        string executable,
        List<string> arguments,
        Action<string> onStdOut,
        Action<string> onStdErr,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // 设置 PYTHONUTF8=1 减少 Windows 中文乱码。
        startInfo.Environment["PYTHONUTF8"] = "1";

        if (environmentVariables is not null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo };

        // 捕获取消，结束整个进程树。
        using var ctr = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // 忽略取消时的异常。
            }
        });

        process.Start();

        // 同时读取 stdout 和 stderr。
        var readStdOut = ReadLinesAsync(process.StandardOutput, onStdOut, cancellationToken);
        var readStdErr = ReadLinesAsync(process.StandardError, onStdErr, cancellationToken);

        await Task.WhenAll(readStdOut, readStdErr, process.WaitForExitAsync(cancellationToken));

        return process.ExitCode;
    }

    private static async Task ReadLinesAsync(
        System.IO.StreamReader reader,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            onLine(line);
        }
    }

    public static IReadOnlyDictionary<string, string> BuildEnvironmentOverrides(AppSettings settings)
    {
        var result = new Dictionary<string, string>();
        var resolvedFfmpegPath = ToolPathResolver.Resolve(settings.FfmpegPath).ResolvedPath;
        var ffmpegDirectory = resolvedFfmpegPath is null ? null : Path.GetDirectoryName(resolvedFfmpegPath);

        if (!string.IsNullOrWhiteSpace(ffmpegDirectory))
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            result["PATH"] = string.IsNullOrEmpty(currentPath)
                ? ffmpegDirectory
                : ffmpegDirectory + Path.PathSeparator + currentPath;
        }

        return result;
    }
}
