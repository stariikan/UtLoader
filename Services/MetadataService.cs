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
                Arguments = $"-J \"{url}\"",   // JSON metadata
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

        /// <summary>
        /// Triggers yt-dlp's native update mechanism and returns a status message.
        /// </summary>
        public async Task<string> UpdateYtDlpAsync()
        {
            var psi = new ProcessStartInfo
            {
                FileName = YtDlpExe,
                Arguments = "-U", // The command to self-update
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var proc = new Process { StartInfo = psi };
                proc.Start();

                string output = await proc.StandardOutput.ReadToEndAsync();
                string error = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                // Combine output and error streams as yt-dlp sometimes writes update info to stderr
                string fullOutput = $"{output}\n{error}".ToLower();

                if (fullOutput.Contains("is up to date"))
                {
                    return "yt-dlp is already up to date.";
                }
                else if (fullOutput.Contains("updated yt-dlp to version"))
                {
                    return "yt-dlp updated successfully!";
                }
                else if (fullOutput.Contains("error"))
                {
                    return "Failed to update yt-dlp. Check your internet connection or file permissions.";
                }

                return "Update check completed (Unknown status).";
            }
            catch (Exception ex)
            {
                return $"Error checking for updates: {ex.Message}";
            }
        }
    }
}