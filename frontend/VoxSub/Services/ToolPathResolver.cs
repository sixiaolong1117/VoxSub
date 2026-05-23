using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace VoxSub.Services;

public static class ToolPathResolver
{
    public static ToolPathLookupResult Resolve(string path)
    {
        var normalizedPath = path.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(normalizedPath))
            return new ToolPathLookupResult(false, ToolPathLookupStatus.Unset, null);

        if (!HasDirectorySeparator(normalizedPath))
        {
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
            foreach (var dir in paths)
            {
                foreach (var executableName in ExpandExecutableNames(normalizedPath))
                {
                    try
                    {
                        var candidate = Path.Combine(dir.Trim(), executableName);
                        if (File.Exists(candidate))
                            return new ToolPathLookupResult(true, ToolPathLookupStatus.FoundInPath, candidate);
                    }
                    catch
                    {
                    }
                }
            }

            return new ToolPathLookupResult(false, ToolPathLookupStatus.NotFoundInPath, null);
        }

        return File.Exists(normalizedPath)
            ? new ToolPathLookupResult(true, ToolPathLookupStatus.FoundAtPath, Path.GetFullPath(normalizedPath))
            : new ToolPathLookupResult(false, ToolPathLookupStatus.FileMissing, null);
    }

    public static bool IsPathValid(string path)
    {
        return Resolve(path).IsFound;
    }

    public static string GetPathHint(string path)
    {
        var result = Resolve(path);
        return result.Status switch
        {
            ToolPathLookupStatus.FoundAtPath => $"OK {result.ResolvedPath}",
            ToolPathLookupStatus.FoundInPath => $"PATH: {result.ResolvedPath}",
            ToolPathLookupStatus.NotFoundInPath => "未在 PATH 中找到",
            ToolPathLookupStatus.FileMissing => "文件不存在",
            _ => "未设置"
        };
    }

    private static bool HasDirectorySeparator(string path)
    {
        return path.Contains(Path.DirectorySeparatorChar)
            || path.Contains(Path.AltDirectorySeparatorChar);
    }

    private static IEnumerable<string> ExpandExecutableNames(string executableName)
    {
        yield return executableName;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            || Path.HasExtension(executableName))
        {
            yield break;
        }

        var pathExt = Environment.GetEnvironmentVariable("PATHEXT");
        var extensions = string.IsNullOrWhiteSpace(pathExt)
            ? [".EXE", ".CMD", ".BAT"]
            : pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var extension in extensions)
            yield return executableName + extension.Trim();
    }
}

public enum ToolPathLookupStatus
{
    Unset,
    FoundAtPath,
    FoundInPath,
    NotFoundInPath,
    FileMissing
}

public sealed record ToolPathLookupResult(bool IsFound, ToolPathLookupStatus Status, string? ResolvedPath);
