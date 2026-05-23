using System;
using System.Collections.Generic;
using System.IO;

namespace VoxSub.Services;

public sealed record VoxSubProcessSpec(string Executable, IReadOnlyList<string> PrefixArguments);

public sealed record VoxSubProcessResolution(
    VoxSubProcessSpec? Spec,
    IReadOnlyList<string> Diagnostics)
{
    public static VoxSubProcessResolution Success(VoxSubProcessSpec spec)
    {
        return new VoxSubProcessResolution(spec, []);
    }

    public static VoxSubProcessResolution Failure(IReadOnlyList<string> diagnostics)
    {
        return new VoxSubProcessResolution(null, diagnostics);
    }
}

public static class VoxSubProcessResolver
{
    public static VoxSubProcessResolution Resolve(AppSettings settings)
    {
        var configuredVoxSub = ToolLocator.FindVoxSubCommand(settings.VoxSubPath);
        if (configuredVoxSub is not null)
        {
            if (IsPythonScript(configuredVoxSub))
                return ResolvePythonScript(configuredVoxSub, settings);

            return VoxSubProcessResolution.Success(new VoxSubProcessSpec(configuredVoxSub, []));
        }

        var fallbackScript = ToolLocator.FindVoxSubPy();
        if (fallbackScript is null)
        {
            return VoxSubProcessResolution.Failure([
                "[错误] 未找到 voxsub 命令，也未找到仓库内的 python/voxsub.py。",
            ]);
        }

        return ResolvePythonScript(fallbackScript, settings);
    }

    public static bool IsPythonScript(string path)
    {
        var normalizedPath = path.Trim().Trim('"');
        return string.Equals(Path.GetExtension(normalizedPath), ".py", StringComparison.OrdinalIgnoreCase);
    }

    private static VoxSubProcessResolution ResolvePythonScript(string scriptPath, AppSettings settings)
    {
        var python = FindIsolatedPythonForScript(scriptPath, settings.PythonPath);
        return python is not null
            ? VoxSubProcessResolution.Success(new VoxSubProcessSpec(python, [scriptPath]))
            : VoxSubProcessResolution.Failure(BuildMissingVirtualEnvironmentDiagnostics(scriptPath, settings.PythonPath));
    }

    private static string? FindIsolatedPythonForScript(string scriptPath, string configuredPythonPath)
    {
        var configuredPython = ToolLocator.FindPython(configuredPythonPath);
        if (configuredPython is not null && ToolLocator.IsVirtualEnvironmentPython(configuredPython))
            return configuredPython;

        var projectRoot = ToolLocator.FindProjectRootFromScript(scriptPath) ?? ToolLocator.FindRepoRoot();
        return projectRoot is null
            ? null
            : ToolLocator.FindVirtualEnvironmentPython(projectRoot);
    }

    private static IReadOnlyList<string> BuildMissingVirtualEnvironmentDiagnostics(
        string scriptPath,
        string configuredPythonPath)
    {
        var diagnostics = new List<string>
        {
            "[错误] 当前使用的是 python/voxsub.py，但未找到可用的虚拟环境 Python。",
            "  VoxSub 不会用系统 Python 直接运行脚本，以免影响本机 pip 包。",
        };

        var configuredPython = ToolLocator.FindPython(configuredPythonPath);
        if (configuredPython is not null && !ToolLocator.IsVirtualEnvironmentPython(configuredPython))
            diagnostics.Add($"  当前 Python 路径解析为：{configuredPython}（不是虚拟环境）。");

        var projectRoot = ToolLocator.FindProjectRootFromScript(scriptPath) ?? ToolLocator.FindRepoRoot();
        if (projectRoot is null)
        {
            diagnostics.Add("  请先创建虚拟环境并在设置里把 Python 指向该环境中的 python 可执行文件。");
            return diagnostics;
        }

        var venvPythonPath = ToolLocator.GetPreferredVirtualEnvironmentPythonPath(projectRoot);
        diagnostics.Add("  请先在项目根目录创建虚拟环境并安装依赖：");
        diagnostics.Add(OperatingSystem.IsWindows()
            ? $"  cd /d \"{projectRoot}\""
            : $"  cd \"{projectRoot}\"");
        diagnostics.Add(OperatingSystem.IsWindows()
            ? "  py -3.12 -m venv .venv"
            : "  python3 -m venv .venv");
        diagnostics.Add(OperatingSystem.IsWindows()
            ? "  .\\.venv\\Scripts\\python.exe -m pip install --upgrade pip"
            : "  ./.venv/bin/python -m pip install --upgrade pip");
        diagnostics.Add(OperatingSystem.IsWindows()
            ? "  .\\.venv\\Scripts\\python.exe -m pip install -r python\\requirements.txt"
            : "  ./.venv/bin/python -m pip install -r python/requirements.txt");
        diagnostics.Add($"  然后在设置里把 Python 路径设为：{venvPythonPath}");

        return diagnostics;
    }
}
