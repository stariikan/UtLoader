UtLoader
Google drive: https://drive.google.com/file/d/1KArqikJiZL2MxOW4x72Vr8qEVdsDALy5/view?usp=sharing
UtLoader is a lightweight desktop application that wraps yt‑dlp and ffmpeg inside a responsive WPF interface.
It provides real‑time progress, MP3/MP4 output, post‑processing status, and a smooth user experience powered by a custom download pipeline.

🚀 Features
🎧 Audio & Video Downloads
- Download MP3 (audio‑only)
- Download MP4 (video + audio)
- Automatic post‑processing using ffmpeg
  
📊 Real‑Time UI Feedback
- Live progress percentage
- File size updates
- File name detection
- Status messages
  
🧠 Smart Behavior
- Playlist detection with user confirmation
- Automatic MP3 extraction
- Automatic MP4 merging
- Error handling with messages
- Non‑blocking UI (async/await)
  
🧱 Architecture
- Full MVVM pattern
- DownloadService handles:
- yt‑dlp execution
- ffmpeg conversion
- output parsing
- progress callbacks

🏗️ How It Works
1. User enters a YouTube URL
The ViewModel validates input and enables the Download button.

3. DownloadService launches yt‑dlp
- Standard output and error streams are parsed in real time
- Regex extracts:
- percentage
- file size
- file name
- extraction/merging steps

3. UI updates live
The ViewModel receives callbacks and updates:
- Progress
- FileName
- FileSize
- Status

4. Post‑processing
Depending on the format:
- MP3 → audio extraction
- MP4 → merging video + audio
- Optional conversion if the downloaded file is not MP4

🧩 Architecture Overview
WPF UI (XAML) > MainViewModel (Commands, Bindings, Status Updates) > DownloadService (yt-dlp + ffmpeg execution) > Process Output Parsing (Regex, state detection).

📦 Requirements
Place these executables in the same folder as the app:
- yt-dlp.exe
- ffmpeg.exe
- ffprobe.exe

🔧 Build Instructions
- Clone the repository
- Open the solution in Visual Studio
- Restore NuGet packages
- Build and run

by Murat Khabriev.
