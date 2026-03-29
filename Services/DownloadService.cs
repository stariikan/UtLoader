using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UtLoader.Services
{
    // 1. New class to hold playlist item data for the UI
    public class PlaylistItem
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true; // Checked by default
    }

    public class DownloadService
    {
        private Process? _process;
        private string? _currentOutputFolder;

        public async Task DownloadAsync(
            string url,
            string outputFolder,
            string targetFormat,
            List<string>? selectedUrls, // 2. New parameter for specific videos
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

            // Fix template
            string template = Path.GetFullPath(outputFolder)
                .Replace("\\", "/") + "/%(title)s.%(ext)s";

            bool isMp3 = targetFormat.Equals("Mp3", StringComparison.OrdinalIgnoreCase);
            bool isNative = targetFormat.Equals("Native", StringComparison.OrdinalIgnoreCase);

            // Build yt-dlp args
            string args = "";

            if (isMp3)
            {
                args = $"-x --audio-format mp3 --audio-quality 0 --embed-metadata --embed-thumbnail -o \"{template}\"";
            }
            else
            {
                args = $"-f \"bv*+ba/b\" -o \"{template}\"";
            }

            // 3. Handle Batch Downloading vs Single URL
            string? batchFilePath = null;
            if (selectedUrls != null && selectedUrls.Any())
            {
                // Write the selected URLs to a text file for yt-dlp to read
                batchFilePath = Path.Combine(outputFolder, "batch_temp.txt");
                File.WriteAllLines(batchFilePath, selectedUrls);
                args += $" -a \"{batchFilePath}\"";
            }
            else
            {
                // Single video download or full playlist download
                args += $" \"{url}\"";
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

            // Cleanup batch file if we created one
            if (batchFilePath != null && File.Exists(batchFilePath))
            {
                try { File.Delete(batchFilePath); } catch { }
            }

            if (ytdlpError)
                throw new Exception("ERR_YTDLP_FAILED: yt-dlp reported an error. Check URL or network.");

            if (isMp3 || isNative)
                return;

            // -------------------------------------------------------------
            // MP4 conversion logic
            // -------------------------------------------------------------
            string? downloadedFile = FindLatestDownloadedFile(outputFolder);
            if (downloadedFile == null)
                throw new Exception("ERR_NO_FILE_CREATED: yt-dlp finished but no output file was found.");

            string ext = Path.GetExtension(downloadedFile).ToLower();

            if (ext == ".mp4")
                return;

            string outputMp4 = Path.ChangeExtension(downloadedFile, ".mp4");
            await ConvertToMp4(downloadedFile, outputMp4, progressCallback);

            try
            {
                File.Delete(downloadedFile);
            }
            catch { }
        }

        // 4. New method to fetch all items in a playlist
        public async Task<List<PlaylistItem>> GetPlaylistItemsAsync(string url)
        {
            var playlistItems = new List<PlaylistItem>();
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

            if (string.IsNullOrWhiteSpace(output)) return playlistItems;

            try
            {
                using var doc = JsonDocument.Parse(output);
                if (doc.RootElement.TryGetProperty("entries", out var entries))
                {
                    foreach (var entry in entries.EnumerateArray())
                    {
                        string title = entry.TryGetProperty("title", out var t) ? t.GetString() ?? "Unknown Title" : "Unknown Title";
                        string id = entry.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                        string videoUrl = entry.TryGetProperty("url", out var u) ? u.GetString() ?? $"https://www.youtube.com/watch?v={id}" : $"https://www.youtube.com/watch?v={id}";

                        // Ignore private/deleted videos which often have no title or a specific placeholder
                        if (!string.IsNullOrEmpty(id) && title != "[Private video]" && title != "[Deleted video]")
                        {
                            playlistItems.Add(new PlaylistItem
                            {
                                Title = title,
                                Url = videoUrl,
                                IsSelected = true
                            });
                        }
                    }
                }
            }
            catch
            {
                // Fallback to returning empty list if JSON parsing fails
            }

            return playlistItems;
        }

        private string? FindLatestDownloadedFile(string folder)
        {
            var files = Directory.GetFiles(folder);
            if (files.Length == 0) return null;

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
                    if (e.Data.Contains("Error")) ffmpegError = true;
                    ParseFfmpegProgress(e.Data, progressCallback, duration);
                }
            };

            proc.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    if (e.Data.Contains("Error")) ffmpegError = true;
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
            if (string.IsNullOrWhiteSpace(line)) return;

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
            if (_process != null && !_process.HasExited)
            {
                _process.Kill(true);
            }

            if (!string.IsNullOrEmpty(_currentOutputFolder) && Directory.Exists(_currentOutputFolder))
            {
                Task.Run(() =>
                {
                    try
                    {
                        System.Threading.Thread.Sleep(500);
                        var directory = new DirectoryInfo(_currentOutputFolder);
                        var partialFiles = directory.GetFiles("*.*")
                            .Where(f => f.Extension.Equals(".part", StringComparison.OrdinalIgnoreCase) ||
                                        f.Extension.Equals(".ytdl", StringComparison.OrdinalIgnoreCase));

                        foreach (var file in partialFiles)
                        {
                            file.Delete();
                        }
                    }
                    catch { }
                });
            }
        }
    }
}