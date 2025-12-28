using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace UtLoader.Services
{
    public class DownloadService
    {
        private Process? _process;

        public async Task DownloadAsync(
            string url,
            string outputFolder,
            bool isMp3,
            Action<double, string, string> progressCallback)
        {
            // Validate tools
            string ytDlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
            string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            string ffprobePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffprobe.exe");

            if (!File.Exists(ytDlpPath))
                throw new Exception("ERR_YTDLP_MISSING: yt-dlp.exe not found in application folder.");

            if (!File.Exists(ffmpegPath))
                throw new Exception("ERR_FFMPEG_MISSING: ffmpeg.exe not found in application folder.");

            if (!File.Exists(ffprobePath))
                throw new Exception("ERR_FFPROBE_MISSING: ffprobe.exe not found in application folder.");

            // Validate output folder
            if (!Directory.Exists(outputFolder))
                throw new Exception($"ERR_OUTPUT_FOLDER: Output folder does not exist: {outputFolder}");

            // Fix template (CRITICAL)
            string template = Path.GetFullPath(outputFolder)
                .Replace("\\", "/") + "/%(title)s.%(ext)s";

            // Playlist detection
            bool downloadPlaylist = false;
            if (await IsPlaylistAsync(url))
            {
                var result = MessageBox.Show(
                    "This URL is a playlist. Do you want to download all videos?",
                    "Playlist detected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                downloadPlaylist = result == MessageBoxResult.Yes;
            }

            // Build yt-dlp args
            string args;

            if (isMp3)
            {
                args = downloadPlaylist
                    ? $"-x --audio-format mp3 --audio-quality 0 -o \"{template}\" \"{url}\""
                    : $"-x --audio-format mp3 --audio-quality 0 --no-playlist -o \"{template}\" \"{url}\"";
            }
            else
            {
                args = downloadPlaylist
                    ? $"-f \"bv*+ba/b\" -o \"{template}\" \"{url}\""
                    : $"-f \"bv*+ba/b\" --no-playlist -o \"{template}\" \"{url}\"";
            }

            // Start yt-dlp
            var psi = new ProcessStartInfo(ytDlpPath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = new Process { StartInfo = psi };

            bool ytdlpError = false;

            _process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    if (e.Data.Contains("ERROR:"))
                        ytdlpError = true;

                    ParseYtDlpLine(e.Data, progressCallback);
                }
            };

            _process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    if (e.Data.Contains("ERROR:"))
                        ytdlpError = true;

                    ParseYtDlpLine(e.Data, progressCallback);
                }
            };

            try
            {
                _process.Start();
            }
            catch (Exception ex)
            {
                throw new Exception($"ERR_PROCESS_START: Failed to start yt-dlp. {ex.Message}");
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            await _process.WaitForExitAsync();

            if (ytdlpError)
                throw new Exception("ERR_YTDLP_FAILED: yt-dlp reported an error. Check URL or network.");

            if (isMp3)
                return;

            // Detect downloaded file
            string? downloadedFile = FindLatestDownloadedFile(outputFolder);
            if (downloadedFile == null)
                throw new Exception("ERR_NO_FILE_CREATED: yt-dlp finished but no output file was found.");

            string ext = Path.GetExtension(downloadedFile).ToLower();

            if (ext == ".mp4")
                return;

            // Ask for conversion
            var convertResult = MessageBox.Show(
                $"The downloaded video is in format {ext.ToUpper().Trim('.')}. Convert to MP4?",
                "Convert to MP4?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (convertResult == MessageBoxResult.No)
                return;

            // Convert
            string outputMp4 = Path.ChangeExtension(downloadedFile, ".mp4");
            await ConvertToMp4(downloadedFile, outputMp4, progressCallback);

            // Delete original
            try
            {
                File.Delete(downloadedFile);
            }
            catch
            {
                // Not critical
            }
        }

        private string? FindLatestDownloadedFile(string folder)
        {
            var files = Directory.GetFiles(folder);
            if (files.Length == 0)
                return null;

            return new DirectoryInfo(folder)
                .GetFiles()
                .OrderByDescending(f => f.LastWriteTime)
                .First()
                .FullName;
        }

        private async Task<double> GetDuration(string input)
        {
            string ffprobePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffprobe.exe");

            var psi = new ProcessStartInfo(ffprobePath,
                $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{input}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            string output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (double.TryParse(output, out double duration))
                return duration;

            throw new Exception("ERR_FFPROBE_DURATION: Could not read video duration.");
        }

        private async Task ConvertToMp4(
            string input,
            string output,
            Action<double, string, string> progressCallback)
        {
            string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");

            double duration = await GetDuration(input);

            string args =
                $"-i \"{input}\" -c:v libx264 -preset medium -c:a aac " +
                "-progress pipe:1 -nostats -y " +
                $"\"{output}\"";

            var psi = new ProcessStartInfo(ffmpegPath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            bool ffmpegError = false;

            using var proc = new Process { StartInfo = psi };

            proc.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    if (e.Data.Contains("Error"))
                        ffmpegError = true;

                    ParseFfmpegProgress(e.Data, progressCallback, duration);
                }
            };

            proc.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    if (e.Data.Contains("Error"))
                        ffmpegError = true;

                    ParseFfmpegProgress(e.Data, progressCallback, duration);
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync();

            if (ffmpegError)
                throw new Exception("ERR_FFMPEG_FAILED: ffmpeg encountered an error during conversion.");
        }

        private void ParseFfmpegProgress(
            string line,
            Action<double, string, string> progressCallback,
            double durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            if (line.StartsWith("out_time="))
            {
                string timeStr = line.Replace("out_time=", "").Trim();

                if (TimeSpan.TryParse(timeStr, out var ts))
                {
                    double percent = (ts.TotalSeconds / durationSeconds) * 100.0;
                    if (percent > 100) percent = 100;

                    progressCallback(percent, "Converting...", "");
                }
            }

            if (line.Contains("progress=end"))
            {
                progressCallback(100, "Conversion complete", "");
            }
        }

        private async Task<bool> IsPlaylistAsync(string url)
        {
            string ytDlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");

            var psi = new ProcessStartInfo(ytDlpPath, $"--flat-playlist --dump-single-json \"{url}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();
            string output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (string.IsNullOrWhiteSpace(output)) return false;

            try
            {
                using var doc = JsonDocument.Parse(output);
                return doc.RootElement.TryGetProperty("entries", out _);
            }
            catch
            {
                return false;
            }
        }
        private void ParseYtDlpLine(
            string line,
            Action<double, string, string> progressCallback)
        {
            // Extract filename ONLY for download, not extraction
            if (line.Contains("[download]", StringComparison.OrdinalIgnoreCase))
            {
                var fileMatch = Regex.Match(line, @"Destination:\s(.+)");
                if (fileMatch.Success)
                {
                    string fileName = Path.GetFileName(fileMatch.Groups[1].Value);
                    progressCallback(0, fileName, "");
                    return;
                }
            }

            // Extract progress + size
            var progressMatch = Regex.Match(
                line,
                @"\[download\]\s+(\d{1,3}\.\d)%\s+of\s+([\d\.]+(?:KiB|MiB|GiB|KB|MB|GB))",
                RegexOptions.IgnoreCase);

            if (progressMatch.Success)
            {
                double percent = double.Parse(progressMatch.Groups[1].Value);
                string size = progressMatch.Groups[2].Value;
                progressCallback(percent, "", size);
                return;
            }

            // Detect MP3 extraction
            if (line.Contains("[ExtractAudio]", StringComparison.OrdinalIgnoreCase))
            {
                progressCallback(100, "Extracting audio...", "");
                return;
            }

            // Detect merge step (MP4 only)
            if (line.Contains("Merging formats", StringComparison.OrdinalIgnoreCase))
            {
                progressCallback(99, "Merging...", "");
                return;
            }

            // Detect 100% completion
            if (line.Contains("[download] 100%"))
            {
                progressCallback(100, "", "");
                return;
            }
        }

        public void Stop()
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill(true);
            }
        }
    }
}