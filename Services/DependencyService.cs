using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace UtLoader.Services
{
    public class DependencyService
    {
        private readonly string _baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // Official direct download links
        private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

        // This is a stable, automated Windows build of FFmpeg containing both ffmpeg.exe and ffprobe.exe
        private const string FfmpegZipUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

        /// <summary>
        /// Checks if dependencies exist. If not, downloads and extracts them.
        /// </summary>
        /// <param name="statusCallback">Allows the service to send progress text back to the UI</param>
        public async Task CheckAndDownloadDependenciesAsync(Action<string> statusCallback)
        {
            using var httpClient = new HttpClient();

            // For downloading large files without timing out
            httpClient.Timeout = TimeSpan.FromMinutes(10);

            // 1. Check and Download yt-dlp.exe
            string ytDlpPath = Path.Combine(_baseDir, "yt-dlp.exe");
            if (!File.Exists(ytDlpPath))
            {
                statusCallback("Downloading yt-dlp.exe...");
                try
                {
                    var response = await httpClient.GetAsync(YtDlpUrl);
                    response.EnsureSuccessStatusCode();

                    await using var fs = new FileStream(ytDlpPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fs);
                }
                catch (Exception ex)
                {
                    // If it fails, throw a clear error with the link for manual download
                    throw new Exception($"Failed to auto-download yt-dlp.\nPlease download it manually from:\n{YtDlpUrl}\n\nError: {ex.Message}");
                }
            }

            // 2. Check and Download ffmpeg.exe and ffprobe.exe
            string ffmpegPath = Path.Combine(_baseDir, "ffmpeg.exe");
            string ffprobePath = Path.Combine(_baseDir, "ffprobe.exe");

            if (!File.Exists(ffmpegPath) || !File.Exists(ffprobePath))
            {
                statusCallback("Downloading FFmpeg (this is ~100MB and may take a minute)...");
                string zipPath = Path.Combine(_baseDir, "ffmpeg_temp.zip");

                try
                {
                    // Download the zip file
                    var response = await httpClient.GetAsync(FfmpegZipUrl);
                    response.EnsureSuccessStatusCode();

                    await using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }

                    statusCallback("Extracting FFmpeg tools...");

                    // Open the zip and hunt for just the two .exe files we need
                    using (var archive = ZipFile.OpenRead(zipPath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                entry.ExtractToFile(ffmpegPath, true);
                            }
                            else if (entry.Name.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                entry.ExtractToFile(ffprobePath, true);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // If it fails, throw a clear error with the link for manual download
                    throw new Exception($"Failed to auto-download FFmpeg.\nPlease download it manually from:\n{FfmpegZipUrl}\n(Extract the zip and place ffmpeg.exe and ffprobe.exe in the app folder).\n\nError: {ex.Message}");
                }
                finally
                {
                    // Always clean up the 100MB zip file so we don't waste the user's disk space
                    if (File.Exists(zipPath))
                    {
                        try { File.Delete(zipPath); } catch { }
                    }
                }
            }
        }
    }
}