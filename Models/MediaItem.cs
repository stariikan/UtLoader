namespace UtLoader.Models
{
    public enum MediaFormat { Mp3, Mp4 }
    public enum MediaStatus { Pending, Downloading, Completed, Failed }

    public class MediaItem
    {
        public string Url { get; set; } = "";
        public string? Title { get; set; }
        public string? ThumbnailUrl { get; set; }
        public MediaFormat Format { get; set; }
        public string OutputPath { get; set; } = "";
        public double Progress { get; set; }
        public MediaStatus Status { get; set; } = MediaStatus.Pending;
        public string? Message { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
    }
}
