using System.Runtime.InteropServices;

namespace VoxSub.Services;

public static class ToolDefaults
{
    public static string FfmpegCommand =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

    public static string PythonCommand =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python.exe" : "python3";

    public static string VoxSubCommand =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "voxsub.exe" : "voxsub";
}
