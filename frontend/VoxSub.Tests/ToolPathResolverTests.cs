using System;
using System.IO;
using VoxSub.Services;
using Xunit;

namespace VoxSub.Tests;

public class ToolPathResolverTests
{
    [Fact]
    public void Resolve_EmptyPath_ReturnsUnset()
    {
        var result = ToolPathResolver.Resolve("  ");

        Assert.False(result.IsFound);
        Assert.Equal(ToolPathLookupStatus.Unset, result.Status);
        Assert.Null(result.ResolvedPath);
    }

    [Fact]
    public void Resolve_ExistingFullPath_ReturnsFoundAtPath()
    {
        var tempPath = Path.GetTempFileName();

        try
        {
            var result = ToolPathResolver.Resolve($"\"{tempPath}\"");

            Assert.True(result.IsFound);
            Assert.Equal(ToolPathLookupStatus.FoundAtPath, result.Status);
            Assert.Equal(Path.GetFullPath(tempPath), result.ResolvedPath);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Resolve_MissingFullPath_ReturnsFileMissing()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            "voxsub-missing-tool-" + Guid.NewGuid().ToString("N") + ".exe");

        var result = ToolPathResolver.Resolve(missingPath);

        Assert.False(result.IsFound);
        Assert.Equal(ToolPathLookupStatus.FileMissing, result.Status);
        Assert.Null(result.ResolvedPath);
    }

    [Fact]
    public void Resolve_MissingCommandName_ReturnsNotFoundInPath()
    {
        var commandName = "voxsub-missing-tool-" + Guid.NewGuid().ToString("N");

        var result = ToolPathResolver.Resolve(commandName);

        Assert.False(result.IsFound);
        Assert.Equal(ToolPathLookupStatus.NotFoundInPath, result.Status);
        Assert.Null(result.ResolvedPath);
    }
}
