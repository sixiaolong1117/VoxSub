namespace VoxSub.Services;

public sealed class AppSettings
{
    public string FfmpegPath { get; set; } = ToolDefaults.FfmpegCommand;

    public string PythonPath { get; set; } = ToolDefaults.PythonCommand;

    public string VoxSubPath { get; set; } = ToolDefaults.VoxSubCommand;
}
