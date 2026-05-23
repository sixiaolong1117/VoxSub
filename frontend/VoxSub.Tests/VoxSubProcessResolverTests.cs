using System;
using System.IO;
using System.Runtime.InteropServices;
using VoxSub.Services;
using Xunit;

namespace VoxSub.Tests;

public class VoxSubProcessResolverTests
{
    [Fact]
    public void Resolve_ConfiguredPythonScript_UsesPythonExecutable()
    {
        using var temp = new TempDirectory();
        var pythonPath = temp.WriteVirtualEnvironmentPython(".venv");
        var scriptPath = temp.WriteProjectScript();

        var result = VoxSubProcessResolver.Resolve(new AppSettings
        {
            PythonPath = pythonPath,
            VoxSubPath = scriptPath,
        });

        var spec = result.Spec;
        Assert.NotNull(spec);
        Assert.Equal(Path.GetFullPath(pythonPath), spec.Executable);
        Assert.Equal([Path.GetFullPath(scriptPath)], spec.PrefixArguments);
    }

    [Fact]
    public void Resolve_ConfiguredPythonScript_UsesProjectVirtualEnvironment()
    {
        using var temp = new TempDirectory();
        var globalPythonPath = temp.WriteFile("python.exe", "");
        var scriptPath = temp.WriteProjectScript();
        var venvPythonPath = temp.WriteVirtualEnvironmentPython(".venv");

        var result = VoxSubProcessResolver.Resolve(new AppSettings
        {
            PythonPath = globalPythonPath,
            VoxSubPath = scriptPath,
        });

        var spec = result.Spec;
        Assert.NotNull(spec);
        Assert.Equal(Path.GetFullPath(venvPythonPath), spec.Executable);
        Assert.Equal([Path.GetFullPath(scriptPath)], spec.PrefixArguments);
    }

    [Fact]
    public void Resolve_ConfiguredPythonScript_RejectsGlobalPythonWithoutVirtualEnvironment()
    {
        using var temp = new TempDirectory();
        var globalPythonPath = temp.WriteFile("python.exe", "");
        var scriptPath = temp.WriteProjectScript();

        var result = VoxSubProcessResolver.Resolve(new AppSettings
        {
            PythonPath = globalPythonPath,
            VoxSubPath = scriptPath,
        });

        Assert.Null(result.Spec);
        Assert.Contains(result.Diagnostics, line => line.Contains("不会用系统 Python"));
        Assert.Contains(result.Diagnostics, line => line.Contains(globalPythonPath));
    }

    [Fact]
    public void Resolve_ConfiguredCommand_UsesCommandDirectly()
    {
        using var temp = new TempDirectory();
        var commandPath = temp.WriteFile("voxsub.exe", "");

        var result = VoxSubProcessResolver.Resolve(new AppSettings
        {
            PythonPath = "missing-python-for-direct-command-test",
            VoxSubPath = commandPath,
        });

        var spec = result.Spec;
        Assert.NotNull(spec);
        Assert.Equal(Path.GetFullPath(commandPath), spec.Executable);
        Assert.Empty(spec.PrefixArguments);
    }

    [Theory]
    [InlineData(@"C:\tools\voxsub.py")]
    [InlineData(@"""C:\tools\voxsub.py""")]
    [InlineData(@"C:\tools\VOXSUB.PY")]
    public void IsPythonScript_DetectsPyFiles(string path)
    {
        Assert.True(VoxSubProcessResolver.IsPythonScript(path));
    }

    private sealed class TempDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "voxsub-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(_path);
        }

        public string WriteFile(string fileName, string content)
        {
            var path = Path.Combine(_path, fileName);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, content);
            return path;
        }

        public string WriteProjectScript()
        {
            WriteFile("pyproject.toml", "[project]");
            return WriteFile(Path.Combine("python", "voxsub.py"), "print('voxsub')");
        }

        public string WriteVirtualEnvironmentPython(string environmentName)
        {
            WriteFile(Path.Combine(environmentName, "pyvenv.cfg"), "");

            var relativePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(environmentName, "Scripts", "python.exe")
                : Path.Combine(environmentName, "bin", "python");

            return WriteFile(relativePath, "");
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
