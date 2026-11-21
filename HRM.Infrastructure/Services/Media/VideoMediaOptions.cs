namespace HRM.Infrastructure.Services.Media;

public sealed class VideoMediaOptions
{
    public const string SectionName = "VideoMedia";
    public bool Enabled { get; set; }
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string FfprobePath { get; set; } = "ffprobe";
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxDurationSeconds { get; set; } = 1800;
}
