🚀 UtLoader

UtLoader is a smart, lightweight desktop application built with WPF and .NET. It acts as a powerful, user-friendly wrapper for yt-dlp and ffmpeg, providing real-time progress, batch playlist downloading, and automatic dependency management inside a modern interface.

✨ Key Features
🎵 Three-Tier Downloading:
1. MP3: Audio-only extraction with automatic ID3 metadata and thumbnail embedding.
2. MP4: Standard video with audio, automatically merged via ffmpeg.
3. Native (Fastest): Grabs the raw, highest-quality format directly from the source to skip conversion time.

- 🗂️ Smart Playlist Management: Automatically detects playlist URLs and launches a custom selection window, allowing you to check or uncheck specific tracks before downloading.
- 🤖 Zero-Setup Dependencies: No more hunting for executable files. If yt-dlp, ffmpeg, or ffprobe are missing, UtLoader automatically fetches and extracts the latest official versions in the background on launch.
- 🔄 Self-Updating Engine: Silently checks the official repositories to ensure yt-dlp is always on the latest release before you start downloading.
- 💾 Persistent Settings: Automatically saves your preferred output folder and format choices so you don't have to reconfigure them every time you open the app.
- 🧹 Graceful Interruption & Cleanup: Non-blocking UI allows you to stop downloads instantly, automatically sweeping up any leftover .part or .ytdl junk files left behind by aborted processes.

📊 Real-Time UI Feedback
- Live progress percentage and dynamic status messages (e.g., "Downloading...", "Extracting audio...", "Merging...").
- Real-time file size updates and file name detection.
- Responsive Grid UI with a dedicated status dashboard.

🧩 Architecture Overview
UtLoader follows a strict MVVM (Model-View-ViewModel) pattern for clean separation of concerns:
- WPF UI (XAML): Modern, responsive Grid layout with native .NET 8 folder dialogs.
- MainViewModel: Manages data binding, command execution, and thread-safe UI updates (Dispatcher.Invoke).
- DownloadService: Orchestrates yt-dlp execution, stream parsing (Regex), batch file creation, and ffmpeg conversions.
- DependencyService: Handles the HTTP downloads and zip extraction for missing external executables.
- SettingsService: Reads and writes user preferences to a local settings.json.
- MetadataService: Manages playlist parsing and background yt-dlp version updates.

📦 Installation & Usage
Because UtLoader is compiled as a Single-File Executable, installation is incredibly simple:

1. Download UtLoader.exe: https://drive.google.com/file/d/1KArqikJiZL2MxOW4x72Vr8qEVdsDALy5/view?usp=sharing
2. Place it in any folder you like.
3. Run it.

(The application will automatically download yt-dlp.exe and ffmpeg into the same folder the first time you run it!)

🔧 Build Instructions (For Developers)
1. Clone the repository.
2. Open the solution in Visual Studio.
3. Restore NuGet packages.
4. Ensure the Publish Profile is set to Framework-Dependent with <PublishSingleFile> and <IncludeNativeLibrariesForSelfExtract> enabled in the .csproj.
5. Build and run.

by Murat Khabriev.
