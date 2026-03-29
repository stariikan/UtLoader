using System;
using System.Collections.Generic;
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
        private string _outputPath;
        private string _status = "Initializing...";
        private string _fileName = "";
        private string _fileSize = "";
        private string _format = "";
        private double _progress;

        // Format selection states
        private bool _isMp3;
        private bool _isMp4;
        private bool _isNative;

        private bool _isDownloading = false;

        private readonly DownloadService _downloadService;
        private readonly MetadataService _metadataService;
        private readonly SettingsService _settingsService;
        private readonly DependencyService _dependencyService; // Added Dependency Service

        public MainViewModel()
        {
            _downloadService = new DownloadService();
            _metadataService = new MetadataService();
            _settingsService = new SettingsService();
            _dependencyService = new DependencyService();

            // Load settings on startup
            var settings = _settingsService.LoadSettings();
            _outputPath = settings.OutputPath;
            _isMp3 = settings.IsMp3;
            _isMp4 = settings.IsMp4;
            _isNative = settings.IsNative;

            BrowseCommand = new RelayCommand(_ => BrowseFolder());
            DownloadCommand = new RelayCommand(async _ => await DownloadAsync(), _ => CanDownload());
            StopCommand = new RelayCommand(_ => StopDownload());

            // Run the dependency check and auto-updater silently on launch
            _ = InitializeApplicationAsync();
        }

        private async Task InitializeApplicationAsync()
        {
            try
            {
                // 1. Check for missing dependencies and download if necessary
                await _dependencyService.CheckAndDownloadDependenciesAsync(statusMessage =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Status = statusMessage;
                    });
                });

                // 2. Once we guarantee yt-dlp exists, check if it needs an update
                Status = "Checking for yt-dlp updates...";
                string updateResult = await _metadataService.UpdateYtDlpAsync();
                Status = updateResult;
            }
            catch (Exception ex)
            {
                // If the auto-download fails (e.g., firewall block), show the manual links
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Status = "Dependency check failed.";
                    MessageBox.Show(ex.Message, "Missing Files", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        // Helper method to write the current state to the JSON file
        private void SaveCurrentSettings()
        {
            _settingsService.SaveSettings(new AppSettings
            {
                OutputPath = this.OutputPath,
                IsMp3 = this.IsMp3,
                IsMp4 = this.IsMp4,
                IsNative = this.IsNative
            });
        }

        public string Url
        {
            get => _url;
            set { _url = value; OnPropertyChanged(nameof(Url)); RefreshCommands(); }
        }

        public string OutputPath
        {
            get => _outputPath;
            set { _outputPath = value; OnPropertyChanged(nameof(OutputPath)); SaveCurrentSettings(); }
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
            set { _isMp3 = value; OnPropertyChanged(nameof(IsMp3)); SaveCurrentSettings(); }
        }

        public bool IsMp4
        {
            get => _isMp4;
            set { _isMp4 = value; OnPropertyChanged(nameof(IsMp4)); SaveCurrentSettings(); }
        }

        public bool IsNative
        {
            get => _isNative;
            set { _isNative = value; OnPropertyChanged(nameof(IsNative)); SaveCurrentSettings(); }
        }

        public ICommand BrowseCommand { get; }
        public ICommand DownloadCommand { get; }
        public ICommand StopCommand { get; }

        private void BrowseFolder()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OutputPath = dlg.SelectedPath;
            }
        }

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

            string targetFormat = IsMp3 ? "Mp3" : IsMp4 ? "Mp4" : "Native";

            Status = "Analyzing link...";
            Format = targetFormat;
            FileName = "";
            FileSize = "";
            Progress = 0;

            try
            {
                List<string>? selectedUrls = null;

                var playlistItems = await _downloadService.GetPlaylistItemsAsync(Url);

                if (playlistItems != null && playlistItems.Count > 1)
                {
                    Status = "Waiting for track selection...";
                    bool proceed = false;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var selectionWindow = new PlaylistSelectionWindow(playlistItems)
                        {
                            Owner = Application.Current.MainWindow
                        };

                        if (selectionWindow.ShowDialog() == true)
                        {
                            selectedUrls = selectionWindow.GetSelectedUrls();
                            proceed = true;
                        }
                    });

                    if (!proceed || selectedUrls == null || selectedUrls.Count == 0)
                    {
                        Status = "Download cancelled by user.";
                        return;
                    }
                }

                Status = "Starting download...";

                await _downloadService.DownloadAsync(
                    Url,
                    OutputPath,
                    targetFormat,
                    selectedUrls,
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