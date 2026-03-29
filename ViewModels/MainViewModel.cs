using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using UtLoader.Services;

namespace UtLoader.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _url = "";
        private string _outputPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        private string _status = "Checking for updates...";
        private string _fileName = "";
        private string _fileSize = "";
        private string _format = "";
        private double _progress;

        // Format selection states
        private bool _isMp3 = true;
        private bool _isMp4 = false;
        private bool _isNative = false;

        private bool _isDownloading = false;

        private readonly DownloadService _downloadService;
        private readonly MetadataService _metadataService; // Added for updates

        public MainViewModel()
        {
            _downloadService = new DownloadService();
            _metadataService = new MetadataService();

            BrowseCommand = new RelayCommand(_ => BrowseFolder());
            DownloadCommand = new RelayCommand(async _ => await DownloadAsync(), _ => CanDownload());
            StopCommand = new RelayCommand(_ => StopDownload());

            // Run the auto-updater silently on launch
            _ = CheckForUpdatesAsync();
        }

        public string Url
        {
            get => _url;
            set { _url = value; OnPropertyChanged(nameof(Url)); RefreshCommands(); }
        }

        public string OutputPath
        {
            get => _outputPath;
            set { _outputPath = value; OnPropertyChanged(nameof(OutputPath)); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(nameof(FileName)); }
        }

        public string FileSize
        {
            get => _fileSize;
            set { _fileSize = value; OnPropertyChanged(nameof(FileSize)); }
        }

        public string Format
        {
            get => _format;
            set { _format = value; OnPropertyChanged(nameof(Format)); }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(nameof(Progress)); }
        }

        public bool IsMp3
        {
            get => _isMp3;
            set { _isMp3 = value; OnPropertyChanged(nameof(IsMp3)); }
        }

        public bool IsMp4
        {
            get => _isMp4;
            set { _isMp4 = value; OnPropertyChanged(nameof(IsMp4)); }
        }

        public bool IsNative
        {
            get => _isNative;
            set { _isNative = value; OnPropertyChanged(nameof(IsNative)); }
        }

        public ICommand BrowseCommand { get; }
        public ICommand DownloadCommand { get; }
        public ICommand StopCommand { get; }

        private async Task CheckForUpdatesAsync()
        {
            Status = "Checking for yt-dlp updates...";
            string updateResult = await _metadataService.UpdateYtDlpAsync();
            Status = updateResult; // Shows "yt-dlp updated successfully!" or "is already up to date"
        }

        private void BrowseFolder()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OutputPath = dlg.SelectedPath;
            }
        }

        // Added URL validation so the button only enables for actual web links
        private bool CanDownload()
        {
            bool isValidUrl = Uri.TryCreate(Url, UriKind.Absolute, out var uriResult)
                              && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            return !_isDownloading && isValidUrl;
        }

        private void RefreshCommands()
        {
            if (DownloadCommand is RelayCommand rc)
                rc.RaiseCanExecuteChanged();
        }

        private async Task DownloadAsync()
        {
            _isDownloading = true;
            RefreshCommands();

            // Determine target format string to pass to the updated service
            string targetFormat = IsMp3 ? "Mp3" : IsMp4 ? "Mp4" : "Native";

            Status = "Starting...";
            Format = targetFormat;
            FileName = "";
            FileSize = "";
            Progress = 0;

            try
            {
                await _downloadService.DownloadAsync(
                    Url,
                    OutputPath,
                    targetFormat,
                    UpdateProgress);

                Status = "Completed";
            }
            catch (Exception ex)
            {
                Status = "Error: " + ex.Message;
            }
            finally
            {
                _isDownloading = false;
                RefreshCommands();
            }
        }

        // Made thread-safe using Dispatcher.Invoke
        private void UpdateProgress(double progress, string fileName, string fileSize)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Progress = progress;

                if (!string.IsNullOrWhiteSpace(fileName))
                    FileName = fileName;

                if (!string.IsNullOrWhiteSpace(fileSize))
                    FileSize = fileSize;

                if (fileName == "Converting...")
                    Status = $"Converting... {progress:0.0}%";
                else if (fileName == "Merging...")
                    Status = "Merging...";
                else if (fileName == "Extracting audio...")
                    Status = "Extracting audio...";
                else if (progress < 100)
                    Status = $"Downloading... {progress:0.0}%";
                else
                    Status = "Finishing...";
            });
        }

        // Exposing the Stop method publicly so the View can call it on Window Close
        public void StopDownload()
        {
            _downloadService.Stop();
            Status = "Download stopped";
            _isDownloading = false;
            RefreshCommands();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}