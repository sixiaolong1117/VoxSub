using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VoxSub.Services;

public sealed record VoxSubPythonEnvironmentPlan(
    string? ScriptPath,
    string? ProjectRoot,
    string? ExistingEnvironmentPython,
    string? BootstrapPython,
    string? VirtualEnvironmentDirectory,
    string? VirtualEnvironmentPython,
    string? RequirementsPath,
    IReadOnlyList<string> Diagnostics)
{
    public bool UsesPythonScript => ScriptPath is not null;
}

public static class VoxSubPythonEnvironment
{
    private static readonly string[] RequiredModules = ["whisper", "opencc", "pysrt", "torch", "argostranslate", "langdetect"];
    private const string TorchCudaIndexUrlEnvironmentVariable = "VOXSUB_TORCH_CUDA_INDEX_URL";
    private const string DefaultTorchCudaIndexUrl = "https://download.pytorch.org/whl/cu128";

    public static VoxSubPythonEnvironmentPlan CreatePlan(AppSettings settings)
    {
        var configuredVoxSub = ToolLocator.FindVoxSubCommand(settings.VoxSubPath);
        if (configuredVoxSub is not null && !VoxSubProcessResolver.IsPythonScript(configuredVoxSub))
            return NoScriptPlan();

        var scriptPath = configuredVoxSub is not null
            ? configuredVoxSub
            : ToolLocator.FindVoxSubPy();

        if (scriptPath is null)
            return NoScriptPlan();

        var projectRoot = ToolLocator.FindProjectRootFromScript(scriptPath) ?? ToolLocator.FindRepoRoot();
        if (projectRoot is null)
        {
            return new VoxSubPythonEnvironmentPlan(
                scriptPath,
                null,
                null,
                null,
                null,
                null,
                null,
                ["[错误] 无法定位 VoxSub 项目根目录，不能自动创建虚拟环境。"]);
        }

        var configuredPython = ToolLocator.FindPython(settings.PythonPath);
        var existingEnvironmentPython = configuredPython is not null && ToolLocator.IsVirtualEnvironmentPython(configuredPython)
            ? configuredPython
            : ToolLocator.FindVirtualEnvironmentPython(projectRoot);

        var bootstrapPython = configuredPython ?? ToolLocator.FindPython();
        var virtualEnvironmentDirectory = ToolLocator.GetPreferredVirtualEnvironmentDirectory(projectRoot);
        var virtualEnvironmentPython = ToolLocator.GetPreferredVirtualEnvironmentPythonPath(projectRoot);
        var requirementsPath = Path.Combine(projectRoot, "python", "requirements.txt");

        return new VoxSubPythonEnvironmentPlan(
            scriptPath,
            projectRoot,
            existingEnvironmentPython,
            bootstrapPython,
            virtualEnvironmentDirectory,
            virtualEnvironmentPython,
            requirementsPath,
            []);
    }

    public static async Task<bool> EnsureReadyAsync(
        AppSettings settings,
        string? requestedDevice,
        Action<string> onLog,
        CancellationToken cancellationToken)
    {
        var plan = CreatePlan(settings);
        if (!plan.UsesPythonScript)
            return true;

        foreach (var diagnostic in plan.Diagnostics)
            onLog(diagnostic);

        if (plan.Diagnostics.Count > 0)
            return false;

        var python = plan.ExistingEnvironmentPython;
        if (python is null)
        {
            if (plan.BootstrapPython is null
                || plan.VirtualEnvironmentDirectory is null
                || plan.VirtualEnvironmentPython is null)
            {
                onLog("[错误] 未找到可用于创建虚拟环境的 Python。请先安装 Python 3.11+。");
                return false;
            }

            onLog($"[信息] 首次使用脚本模式，正在创建项目虚拟环境：{plan.VirtualEnvironmentDirectory}");
            onLog($"[信息] 仅使用 {plan.BootstrapPython} 创建 .venv，不会向系统 Python 安装包。");

            var createExitCode = await RunProcessAsync(
                plan.BootstrapPython,
                ["-m", "venv", plan.VirtualEnvironmentDirectory],
                onLog,
                cancellationToken);

            if (createExitCode != 0)
            {
                onLog($"[错误] 虚拟环境创建失败，退出码：{createExitCode}");
                return false;
            }

            python = File.Exists(plan.VirtualEnvironmentPython)
                ? plan.VirtualEnvironmentPython
                : ToolLocator.FindVirtualEnvironmentPython(plan.ProjectRoot!);

            if (python is null)
            {
                onLog("[错误] 虚拟环境已创建，但未找到其中的 Python 可执行文件。");
                return false;
            }
        }

        if (await HasRequiredModulesAsync(python, cancellationToken))
        {
            if (!await EnsureCudaTorchReadyIfRequestedAsync(python, requestedDevice, onLog, cancellationToken))
                return false;

            onLog($"[信息] Python 虚拟环境就绪：{python}");
            return true;
        }

        if (plan.RequirementsPath is null || !File.Exists(plan.RequirementsPath))
        {
            onLog("[错误] 未找到 python/requirements.txt，无法自动安装依赖。");
            return false;
        }

        onLog("[信息] 正在安装或补齐 Python 依赖，首次运行可能需要几分钟。");
        onLog($"[信息] 依赖安装目标：{python}");

        var upgradePipExitCode = await RunProcessAsync(
            python,
            ["-m", "pip", "install", "--upgrade", "pip"],
            onLog,
            cancellationToken);

        if (upgradePipExitCode != 0)
        {
            onLog($"[错误] pip 升级失败，退出码：{upgradePipExitCode}");
            return false;
        }

        var installExitCode = await RunProcessAsync(
            python,
            ["-m", "pip", "install", "-r", plan.RequirementsPath],
            onLog,
            cancellationToken);

        if (installExitCode != 0)
        {
            onLog($"[错误] Python 依赖安装失败，退出码：{installExitCode}");
            return false;
        }

        if (!await HasRequiredModulesAsync(python, cancellationToken))
        {
            onLog("[错误] 依赖安装完成后仍检测到缺失模块。");
            return false;
        }

        if (!await EnsureCudaTorchReadyIfRequestedAsync(python, requestedDevice, onLog, cancellationToken))
            return false;

        onLog($"[信息] Python 虚拟环境就绪：{python}");
        return true;
    }

    private static VoxSubPythonEnvironmentPlan NoScriptPlan()
    {
        return new VoxSubPythonEnvironmentPlan(null, null, null, null, null, null, null, []);
    }

    private static async Task<bool> HasRequiredModulesAsync(string python, CancellationToken cancellationToken)
    {
        var checkCode = string.Join(
            "; ",
            "import importlib.util, sys",
            "mods = " + ToPythonListLiteral(RequiredModules),
            "missing = [m for m in mods if importlib.util.find_spec(m) is None]",
            "print(','.join(missing))",
            "sys.exit(1 if missing else 0)");

        var result = await RunProcessCaptureAsync(python, ["-c", checkCode], cancellationToken);
        return result.ExitCode == 0;
    }

    private static async Task<bool> EnsureCudaTorchReadyIfRequestedAsync(
        string python,
        string? requestedDevice,
        Action<string> onLog,
        CancellationToken cancellationToken)
    {
        if (!IsCudaRequested(requestedDevice))
            return true;

        if (await HasTorchCudaAsync(python, cancellationToken))
            return true;

        var cudaIndexUrl = ResolveTorchCudaIndexUrl();
        onLog("[信息] 已请求 CUDA，但当前虚拟环境中的 PyTorch 不是 CUDA 可用版本。");
        onLog($"[信息] 正在安装 CUDA 版 PyTorch：{cudaIndexUrl}");
        onLog("[信息] 该步骤需要从 PyTorch 官方源下载较大的包，首次运行可能需要较久。");

        var installExitCode = await RunProcessAsync(
            python,
            ["-m", "pip", "install", "--upgrade", "--force-reinstall", "torch", "--index-url", cudaIndexUrl],
            onLog,
            cancellationToken);

        if (installExitCode != 0)
        {
            onLog($"[错误] CUDA 版 PyTorch 安装失败，退出码：{installExitCode}");
            onLog($"[提示] 可手动执行：\"{python}\" -m pip install --upgrade --force-reinstall torch --index-url {cudaIndexUrl}");
            return false;
        }

        if (await HasTorchCudaAsync(python, cancellationToken))
            return true;

        onLog("[错误] CUDA 版 PyTorch 安装后仍无法使用 CUDA。请检查 NVIDIA 驱动是否正常，或改用 CPU 设备。");
        onLog($"[提示] 可手动检查：\"{python}\" -c \"import torch; print(torch.__version__, torch.version.cuda, torch.cuda.is_available())\"");
        return false;
    }

    private static bool IsCudaRequested(string? requestedDevice)
    {
        return string.Equals(requestedDevice?.Trim(), "cuda", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveTorchCudaIndexUrl()
    {
        var configuredIndexUrl = Environment.GetEnvironmentVariable(TorchCudaIndexUrlEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configuredIndexUrl)
            ? DefaultTorchCudaIndexUrl
            : configuredIndexUrl.Trim();
    }

    private static async Task<bool> HasTorchCudaAsync(string python, CancellationToken cancellationToken)
    {
        var checkCode = string.Join(
            "; ",
            "import sys, torch",
            "print(getattr(torch, '__version__', 'unknown'))",
            "print(getattr(torch.version, 'cuda', None))",
            "print(torch.cuda.is_available())",
            "sys.exit(0 if torch.cuda.is_available() else 1)");

        var result = await RunProcessCaptureAsync(python, ["-c", checkCode], cancellationToken);
        return result.ExitCode == 0;
    }

    private static async Task<int> RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        Action<string> onLog,
        CancellationToken cancellationToken)
    {
        var result = await RunProcessCaptureAsync(
            executable,
            arguments,
            cancellationToken,
            line => onLog(line),
            line => onLog($"[stderr] {line}"));

        return result.ExitCode;
    }

    private static async Task<ProcessRunResult> RunProcessCaptureAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        Action<string>? onStdOut = null,
        Action<string>? onStdErr = null)
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

        startInfo.Environment["PYTHONUTF8"] = "1";

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        using var process = new Process { StartInfo = startInfo };

        using var ctr = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        });

        process.Start();

        var readStdOut = ReadLinesAsync(process.StandardOutput, stdout, onStdOut, cancellationToken);
        var readStdErr = ReadLinesAsync(process.StandardError, stderr, onStdErr, cancellationToken);

        await Task.WhenAll(readStdOut, readStdErr, process.WaitForExitAsync(cancellationToken));

        return new ProcessRunResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        StringBuilder capture,
        Action<string>? onLine,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            capture.AppendLine(line);
            onLine?.Invoke(line);
        }
    }

    private static string ToPythonListLiteral(IEnumerable<string> values)
    {
        return "[" + string.Join(", ", values.Select(value => $"'{value}'")) + "]";
    }

    private sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr);
}
