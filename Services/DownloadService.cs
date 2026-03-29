using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;

namespace UtLoader.Services
{
    public class DownloadService
    {
        private Process? _process;
        private string? _currentOutputFolder;

        public async Task DownloadAsync(
            string url,
            string outputFolder,
            string targetFormat, // Changed from bool isMp3 to accept "Mp3", "Mp4", or "Native"
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

            _currentOutputFolder = outputFolder;
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

            bool isMp3 = targetFormat.Equals("Mp3", StringComparison.OrdinalIgnoreCase);
            bool isNative = targetFormat.Equals("Native", StringComparison.OrdinalIgnoreCase);

            // Build yt-dlp args
            string args;

            if (isMp3)
            {
                // Added --embed-metadata and --embed-thumbnail from our earlier upgrade
                args = downloadPlaylist
                    ? $"-x --audio-format mp3 --audio-quality 0 --embed-metadata --embed-thumbnail -o \"{template}\" \"{url}\""
                    : $"-x --audio-format mp3 --audio-quality 0 --embed-metadata --embed-thumbnail --no-playlist -o \"{template}\" \"{url}\"";
            }
            else
            {
                // Both MP4 and Native will use the best video + best audio flags initially
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

            // If it's MP3, yt-dlp handled the audio extraction already.
            // If it's Native, we want whatever format yt-dlp gave us, so we stop here.
            if (isMp3 || isNative)
                return;

            // -------------------------------------------------------------
            // Everything below this line is strictly for MP4 conversion
            // -------------------------------------------------------------

            string? downloadedFile = FindLatestDownloadedFile(outputFolder);
            if (downloadedFile == null)
                throw new Exception("ERR_NO_FILE_CREATED: yt-dlp finished but no output file was found.");

            string ext = Path.GetExtension(downloadedFile).ToLower();

            if (ext == ".mp4")
                return;

            var convertResult = MessageBox.Show(
                $"The downloaded video is in format {ext.ToUpper().Trim('.')}. Convert to MP4?",
                "Convert to MP4?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (convertResult == MessageBoxResult.No)
                return;

            string outputMp4 = Path.ChangeExtension(downloadedFile, ".mp4");
            await ConvertToMp4(downloadedFile, outputMp4, progressCallback);

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

            if (line.Contains("[ExtractAudio]", StringComparison.OrdinalIgnoreCase))
            {
                progressCallback(100, "Extracting audio...", "");
                return;
            }

            if (line.Contains("Merging formats", StringComparison.OrdinalIgnoreCase))
            {
                progressCallback(99, "Merging...", "");
                return;
            }

            if (line.Contains("[download] 100%"))
            {
                progressCallback(100, "", "");
                return;
            }
        }

        public void Stop()
        {
            // 1. Kill the active process
            if (_process != null && !_process.HasExited)
            {
                _process.Kill(true);
            }

            // 2. Clean up partial files left behind
            if (!string.IsNullOrEmpty(_currentOutputFolder) && Directory.Exists(_currentOutputFolder))
            {
                // Run this on a background task so Thread.Sleep doesn't freeze the UI
                Task.Run(() =>
                {
                    try
                    {
                        // Give the OS a tiny moment to release file locks after killing the process
                        System.Threading.Thread.Sleep(500);

                        var directory = new DirectoryInfo(_currentOutputFolder);

                        // yt-dlp usually leaves behind .part or .ytdl files
                        var partialFiles = directory.GetFiles("*.*")
                            .Where(f => f.Extension.Equals(".part", StringComparison.OrdinalIgnoreCase) ||
                                        f.Extension.Equals(".ytdl", StringComparison.OrdinalIgnoreCase));

                        foreach (var file in partialFiles)
                        {
                            file.Delete();
                        }
                    }
                    catch
                    {
                        // If a file is stubbornly locked by the OS, ignore it.
                        // We don't want a cleanup failure to crash the app.
                    }
                });
            }
        }
    }
}