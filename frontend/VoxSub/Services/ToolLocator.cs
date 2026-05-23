using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace VoxSub.Services;

public sealed class ToolLocator
{
    private const string PythonDirectoryName = "python";
    private const string VoxSubScriptName = "voxsub.py";

    /// <summary>
    /// 按顺序尝试的 Python 可执行文件名。
    /// </summary>
    private static readonly string[] PythonCandidates =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["python", "python3", "py"]
            : ["python3", "python"];

    /// <summary>
    /// 在当前环境中检测 Python 可执行文件。
    /// 先检查用户手动指定的路径，再检查 PATH。
    /// </summary>
    public static string? FindPython(string? userSpecifiedPath = null)
    {
        if (!string.IsNullOrWhiteSpace(userSpecifiedPath) && File.Exists(userSpecifiedPath))
            return userSpecifiedPath;

        foreach (var candidate in PythonCandidates)
        {
            var fullPath = FindOnPath(candidate);
            if (fullPath is not null)
                return fullPath;
        }

        return null;
    }

    /// <summary>
    /// 查找 voxsub 命令（已安装的 pip/pipx 版本）。
    /// </summary>
    public static string? FindVoxSubCommand(string? userSpecifiedPath = null)
    {
        if (!string.IsNullOrWhiteSpace(userSpecifiedPath) && File.Exists(userSpecifiedPath))
            return userSpecifiedPath;

        var commandName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "voxsub.exe" : "voxsub";
        return FindOnPath(commandName);
    }

    /// <summary>
    /// 从 AppContext.BaseDirectory 向上查找包含 python/voxsub.py 的仓库根目录。
    /// </summary>
    public static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var pyPath = Path.Combine(dir.FullName, PythonDirectoryName, VoxSubScriptName);
            var legacyPyPath = Path.Combine(dir.FullName, VoxSubScriptName);
            if (File.Exists(pyPath) || File.Exists(legacyPyPath))
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// 查找 voxsub.py 脚本路径。
    /// </summary>
    public static string? FindVoxSubPy(string? userSpecifiedPath = null)
    {
        if (!string.IsNullOrWhiteSpace(userSpecifiedPath) && File.Exists(userSpecifiedPath))
            return userSpecifiedPath;

        var repoRoot = FindRepoRoot();
        if (repoRoot is not null)
        {
            var pyPath = Path.Combine(repoRoot, PythonDirectoryName, VoxSubScriptName);
            if (File.Exists(pyPath))
                return pyPath;

            var legacyPyPath = Path.Combine(repoRoot, VoxSubScriptName);
            if (File.Exists(legacyPyPath))
                return legacyPyPath;
        }

        return null;
    }

    /// <summary>
    /// 查找 ffmpeg 可执行文件。
    /// </summary>
    public static string? FindFfmpeg(string? userSpecifiedPath = null)
    {
        if (!string.IsNullOrWhiteSpace(userSpecifiedPath) && File.Exists(userSpecifiedPath))
            return userSpecifiedPath;

        var commandName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        return FindOnPath(commandName);
    }

    /// <summary>
    /// 在 PATH 环境变量中查找指定可执行文件。
    /// </summary>
    private static string? FindOnPath(string executableName)
    {
        // 如果是完整路径且存在，直接返回。
        if (Path.IsPathFullyQualified(executableName) && File.Exists(executableName))
            return executableName;

        // 检查当前工作目录。
        var cwd = Path.Combine(Environment.CurrentDirectory, executableName);
        if (File.Exists(cwd))
            return cwd;

        // 检查 PATH 环境变量。
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var fullPath = Path.Combine(dir.Trim(), executableName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }
}
