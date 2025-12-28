using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace UtLoader.Services

{
    public class VideoMetadata
    {
        public string? Title { get; set; }
        public string? ThumbnailUrl { get; set; }
    }

    public class MetadataService
    {
        private const string YtDlpExe = "yt-dlp.exe";

        public async Task<VideoMetadata?> GetMetadataAsync(string url)
        {
            var psi = new ProcessStartInfo
            {
                FileName = YtDlpExe,
                Arguments = $"-J {url}",   // JSON metadata
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var proc = new Process { StartInfo = psi };
                proc.Start();

                string json = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                if (string.IsNullOrWhiteSpace(json))
                    return null;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
                string? thumbnail = root.TryGetProperty("thumbnail", out var th) ? th.GetString() : null;

                return new VideoMetadata
                {
                    Title = title,
                    ThumbnailUrl = thumbnail
                };
            }
            catch
            {
                return null;
            }
        }
    }
}