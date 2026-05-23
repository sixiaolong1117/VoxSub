namespace VoxSub.Models;

public sealed class VoxSubJob
{
    public VoxSubCommandKind CommandKind { get; set; }
    public string MediaPath { get; set; } = string.Empty;
    public string? SubtitlePath { get; set; }
    public string? OutputSrtPath { get; set; }
    public string? OutputVideoPath { get; set; }
    public string? Language { get; set; }
    public string? Model { get; set; }
    public string? Device { get; set; }
    public string? Fp16 { get; set; }
    public bool Verbose { get; set; }
    public bool Overwrite { get; set; }
}