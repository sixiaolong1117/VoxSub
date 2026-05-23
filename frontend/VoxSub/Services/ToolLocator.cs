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
            ? ["python.exe", "python3.exe", "py.exe", "python", "python3", "py"]
            : ["python3", "python"];

    /// <summary>
    /// 在当前环境中检测 Python 可执行文件。
    /// 先检查用户手动指定的路径，再检查 PATH。
    /// </summary>
    public static string? FindPython(string? userSpecifiedPath = null)
    {
        if (!string.IsNullOrWhiteSpace(userSpecifiedPath))
            return ResolveConfiguredPath(userSpecifiedPath);

        foreach (var candidate in PythonCandidates)
        {
            var fullPath = ResolveConfiguredPath(candidate);
            if (fullPath is not null)
                return fullPath;
        }

        return null;
    }

    public static string? FindVirtualEnvironmentPython(string rootDirectory)
    {
        foreach (var environmentName in new[] { ".venv", "venv", "env" })
        {
            var environmentRoot = Path.Combine(rootDirectory, environmentName);
            foreach (var relativePath in GetVirtualEnvironmentPythonRelativePaths())
            {
                var pythonPath = Path.Combine(environmentRoot, relativePath);
                if (File.Exists(pythonPath) && IsVirtualEnvironmentPython(pythonPath))
                    return Path.GetFullPath(pythonPath);
            }
        }

        return null;
    }

    public static string GetPreferredVirtualEnvironmentDirectory(string rootDirectory)
    {
        return Path.Combine(rootDirectory, ".venv");
    }

    public static string GetPreferredVirtualEnvironmentPythonPath(string rootDirectory)
    {
        var relativePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(".venv", "Scripts", "python.exe")
            : Path.Combine(".venv", "bin", "python");

        return Path.Combine(rootDirectory, relativePath);
    }

    public static bool IsVirtualEnvironmentPython(string pythonPath)
    {
        var resolvedPath = ToolPathResolver.Resolve(pythonPath).ResolvedPath;
        if (resolvedPath is null)
            return false;

        var executableDirectory = Path.GetDirectoryName(resolvedPath);
        if (string.IsNullOrWhiteSpace(executableDirectory))
            return false;

        return IsPythonEnvironmentRoot(executableDirectory)
            || (Directory.GetParent(executableDirectory) is { } parent
                && IsPythonEnvironmentRoot(parent.FullName));
    }

    /// <summary>
    /// 查找 voxsub 命令（已安装的 pip/pipx 版本）。
    /// </summary>
    public static string? FindVoxSubCommand(string? userSpecifiedPath = null)
    {
        if (!string.IsNullOrWhiteSpace(userSpecifiedPath))
            return ResolveConfiguredPath(userSpecifiedPath);

        return ResolveConfiguredPath(ToolDefaults.VoxSubCommand);
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

    public static string? FindProjectRootFromScript(string scriptPath)
    {
        var normalizedScriptPath = scriptPath.Trim().Trim('"');
        var directory = Path.GetDirectoryName(normalizedScriptPath);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var pyprojectPath = Path.Combine(directory, "pyproject.toml");
            var scriptInPythonDirectory = Path.Combine(directory, PythonDirectoryName, VoxSubScriptName);
            var legacyScript = Path.Combine(directory, VoxSubScriptName);

            if (File.Exists(pyprojectPath)
                && (File.Exists(scriptInPythonDirectory) || File.Exists(legacyScript)))
            {
                return Path.GetFullPath(directory);
            }

            directory = Directory.GetParent(directory)?.FullName;
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
        if (!string.IsNullOrWhiteSpace(userSpecifiedPath))
            return ResolveConfiguredPath(userSpecifiedPath);

        return ResolveConfiguredPath(ToolDefaults.FfmpegCommand);
    }

    private static string? ResolveConfiguredPath(string path)
    {
        return ToolPathResolver.Resolve(path).ResolvedPath;
    }

    private static IEnumerable<string> GetVirtualEnvironmentPythonRelativePaths()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return Path.Combine("Scripts", "python.exe");
            yield return Path.Combine("Scripts", "python3.exe");
            yield break;
        }

        yield return Path.Combine("bin", "python3");
        yield return Path.Combine("bin", "python");
    }

    private static bool IsPythonEnvironmentRoot(string directory)
    {
        return File.Exists(Path.Combine(directory, "pyvenv.cfg"))
            || Directory.Exists(Path.Combine(directory, "conda-meta"));
    }
}
