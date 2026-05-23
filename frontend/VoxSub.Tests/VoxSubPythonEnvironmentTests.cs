using System;
using System.IO;
using System.Runtime.InteropServices;
using VoxSub.Services;
using Xunit;

namespace VoxSub.Tests;

public class VoxSubPythonEnvironmentTests
{
    [Fact]
    public void CreatePlan_ConfiguredCommand_DoesNotUseScriptMode()
    {
        using var temp = new TempDirectory();
        var commandPath = temp.WriteFile("voxsub.exe", "");

        var plan = VoxSubPythonEnvironment.CreatePlan(new AppSettings
        {
            VoxSubPath = commandPath,
        });

        Assert.False(plan.UsesPythonScript);
        Assert.Null(plan.ScriptPath);
    }

    [Fact]
    public void CreatePlan_PythonScriptWithProjectVenv_UsesExistingEnvironment()
    {
        using var temp = new TempDirectory();
        var scriptPath = temp.WriteProjectScript();
        var venvPython = temp.WriteVirtualEnvironmentPython(".venv");

        var plan = VoxSubPythonEnvironment.CreatePlan(new AppSettings
        {
            PythonPath = "missing-global-python-for-test",
            VoxSubPath = scriptPath,
        });

        Assert.True(plan.UsesPythonScript);
        Assert.Equal(Path.GetFullPath(scriptPath), plan.ScriptPath);
        Assert.Equal(Path.GetFullPath(venvPython), plan.ExistingEnvironmentPython);
    }

    [Fact]
    public void CreatePlan_PythonScriptWithoutVenv_PreparesProjectLocalVenv()
    {
        using var temp = new TempDirectory();
        var scriptPath = temp.WriteProjectScript();
        var globalPython = temp.WriteFile("python.exe", "");

        var plan = VoxSubPythonEnvironment.CreatePlan(new AppSettings
        {
            PythonPath = globalPython,
            VoxSubPath = scriptPath,
        });

        Assert.True(plan.UsesPythonScript);
        Assert.Null(plan.ExistingEnvironmentPython);
        Assert.Equal(Path.GetFullPath(globalPython), plan.BootstrapPython);
        Assert.Equal(Path.Combine(temp.Path, ".venv"), plan.VirtualEnvironmentDirectory);
        Assert.Equal(GetVenvPythonPath(temp.Path), plan.VirtualEnvironmentPython);
        Assert.Equal(Path.Combine(temp.Path, "python", "requirements.txt"), plan.RequirementsPath);
    }

    private static string GetVenvPythonPath(string projectRoot)
    {
        var relativePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(".venv", "Scripts", "python.exe")
            : Path.Combine(".venv", "bin", "python");

        return Path.Combine(projectRoot, relativePath);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "voxsub-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public string WriteFile(string fileName, string content)
        {
            var path = System.IO.Path.Combine(Path, fileName);
            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, content);
            return path;
        }

        public string WriteProjectScript()
        {
            WriteFile("pyproject.toml", "[project]");
            WriteFile(System.IO.Path.Combine("python", "requirements.txt"), "pysrt");
            return WriteFile(System.IO.Path.Combine("python", "voxsub.py"), "print('voxsub')");
        }

        public string WriteVirtualEnvironmentPython(string environmentName)
        {
            WriteFile(System.IO.Path.Combine(environmentName, "pyvenv.cfg"), "");

            var relativePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? System.IO.Path.Combine(environmentName, "Scripts", "python.exe")
                : System.IO.Path.Combine(environmentName, "bin", "python");

            return WriteFile(relativePath, "");
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
