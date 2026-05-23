using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

        private bool _isMp3;
        private bool _isMp4;
        private bool _isNative;

        private bool _isDownloading = false;
        private bool _stopRequested = false;

        // --- NEW PAUSE STATE ---
        private bool _isPaused = false;
        public string PauseButtonText => _isPaused ? "Resume" : "Pause";

        private bool _isPlaylistVisible = false;
        public bool IsPlaylistVisible
        {
            get => _isPlaylistVisible;
            set { _isPlaylistVisible = value; OnPropertyChanged(nameof(IsPlaylistVisible)); RefreshCommands(); }
        }

        private List<PlaylistItem> _playlistItems = new List<PlaylistItem>();
        public List<PlaylistItem> PlaylistItems
        {
            get => _playlistItems;
            set
            {
                _playlistItems = value;
                OnPropertyChanged(nameof(PlaylistItems));
                OnPropertyChanged(nameof(FilteredPlaylistItems));
            }
        }

        private string _searchQuery = "";
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged(nameof(SearchQuery));
                OnPropertyChanged(nameof(FilteredPlaylistItems));
            }
        }

        public IEnumerable<PlaylistItem> FilteredPlaylistItems =>
            string.IsNullOrWhiteSpace(SearchQuery)
                ? PlaylistItems
                : PlaylistItems.Where(p => p.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

        private bool _selectAll = true;
        public bool SelectAll
        {
            get => _selectAll;
            set
            {
                _selectAll = value;
                OnPropertyChanged(nameof(SelectAll));

                if (PlaylistItems != null)
                {
                    foreach (var item in FilteredPlaylistItems)
                    {
                        item.IsSelected = _selectAll;
                    }
                }
            }
        }

        private readonly DownloadService _downloadService;
        private readonly MetadataService _metadataService;
        private readonly SettingsService _settingsService;
        private readonly DependencyService _dependencyService;

        public MainViewModel()
        {
            _downloadService = new DownloadService();
            _metadataService = new MetadataService();
            _settingsService = new SettingsService();
            _dependencyService = new DependencyService();

            var settings = _settingsService.LoadSettings();
            _outputPath = settings.OutputPath;
            _isMp3 = settings.IsMp3;
            _isMp4 = settings.IsMp4;
            _isNative = settings.IsNative;

            BrowseCommand = new RelayCommand(_ => BrowseFolder());
            DownloadCommand = new RelayCommand(async _ => await DownloadAsync(), _ => CanDownload());
            StopCommand = new RelayCommand(_ => StopDownload());
            StartPlaylistCommand = new RelayCommand(async _ => await StartPlaylistAsync(), _ => CanStartPlaylist());

            // --- NEW PAUSE COMMAND ---
            PauseCommand = new RelayCommand(_ => TogglePause(), _ => _isDownloading && IsPlaylistVisible);

            _ = InitializeApplicationAsync();
        }

        private async Task InitializeApplicationAsync()
        {
            try
            {
                await _dependencyService.CheckAndDownloadDependenciesAsync(statusMessage =>
                {
                    Application.Current.Dispatcher.Invoke(() => Status = statusMessage);
                });

                Status = "Checking for yt-dlp updates...";
                string updateResult = await _metadataService.UpdateYtDlpAsync();
                Status = updateResult;
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Status = "Dependency check failed.";
                    MessageBox.Show(ex.Message, "Missing Files", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

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

        public string Url { get => _url; set { _url = value; OnPropertyChanged(nameof(Url)); RefreshCommands(); } }
        public string OutputPath { get => _outputPath; set { _outputPath = value; OnPropertyChanged(nameof(OutputPath)); SaveCurrentSettings(); } }
        public string Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }
        public string FileName { get => _fileName; set { _fileName = value; OnPropertyChanged(nameof(FileName)); } }
        public string FileSize { get => _fileSize; set { _fileSize = value; OnPropertyChanged(nameof(FileSize)); } }
        public string Format { get => _format; set { _format = value; OnPropertyChanged(nameof(Format)); } }
        public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(nameof(Progress)); } }
        public bool IsMp3 { get => _isMp3; set { _isMp3 = value; OnPropertyChanged(nameof(IsMp3)); SaveCurrentSettings(); } }
        public bool IsMp4 { get => _isMp4; set { _isMp4 = value; OnPropertyChanged(nameof(IsMp4)); SaveCurrentSettings(); } }
        public bool IsNative { get => _isNative; set { _isNative = value; OnPropertyChanged(nameof(IsNative)); SaveCurrentSettings(); } }

        public ICommand BrowseCommand { get; }
        public ICommand DownloadCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand StartPlaylistCommand { get; }
        public ICommand PauseCommand { get; } // Hooked to UI

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

        private bool CanStartPlaylist()
        {
            return IsPlaylistVisible && !_isDownloading;
        }

        private void RefreshCommands()
        {
            if (DownloadCommand is RelayCommand dc) dc.RaiseCanExecuteChanged();
            if (StartPlaylistCommand is RelayCommand spc) spc.RaiseCanExecuteChanged();
            if (PauseCommand is RelayCommand pc) pc.RaiseCanExecuteChanged();
        }

        // --- NEW PAUSE TOGGLE LOGIC ---
        private void TogglePause()
        {
            _isPaused = !_isPaused;
            OnPropertyChanged(nameof(PauseButtonText));

            if (!_isPaused)
            {
                Status = "Resuming queue...";
            }
        }

        private async Task DownloadAsync()
        {
            _isDownloading = true;
            _stopRequested = false;
            _isPaused = false; // Reset pause state
            OnPropertyChanged(nameof(PauseButtonText));
            IsPlaylistVisible = false;
            RefreshCommands();

            string targetFormat = IsMp3 ? "Mp3" : IsMp4 ? "Mp4" : "Native";
            Status = "Analyzing link...";
            Format = targetFormat;
            FileName = "";
            FileSize = "";
            Progress = 0;

            try
            {
                var fetchedItems = await _downloadService.GetPlaylistItemsAsync(Url);

                if (fetchedItems != null && fetchedItems.Count > 1)
                {
                    var promptResult = MessageBox.Show(
                        $"This URL contains a playlist with {fetchedItems.Count} videos.\n\n" +
                        "Do you want to open the Playlist Manager?\n\n" +
                        "Yes = Open Playlist Manager\n" +
                        "No = Just download this single video link",
                        "Playlist Detected",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (promptResult == MessageBoxResult.Cancel)
                    {
                        Status = "Cancelled.";
                        _isDownloading = false;
                        RefreshCommands();
                        return;
                    }

                    if (promptResult == MessageBoxResult.Yes)
                    {
                        PlaylistItems = fetchedItems;
                        IsPlaylistVisible = true;
                        Status = "Playlist loaded. Select tracks and click Start.";
                        _isDownloading = false;
                        RefreshCommands();
                        return;
                    }
                }

                Status = "Starting download...";
                await _downloadService.DownloadAsync(Url, OutputPath, targetFormat, UpdateProgress);
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

        private async Task StartPlaylistAsync()
        {
            _isDownloading = true;
            _stopRequested = false;
            _isPaused = false;
            OnPropertyChanged(nameof(PauseButtonText));
            RefreshCommands();

            string targetFormat = IsMp3 ? "Mp3" : IsMp4 ? "Mp4" : "Native";
            int completedCount = 0;

            var itemsToDownload = PlaylistItems.Where(p => p.IsSelected).ToList();

            foreach (var track in PlaylistItems)
            {
                if (_stopRequested) break;

                // --- NEW PAUSE TRAP ---
                // If the user clicked Pause, this while loop holds the thread here indefinitely
                // checking every 500ms until they click Resume (or Stop)
                while (_isPaused && !_stopRequested)
                {
                    Status = "Queue Paused (Waiting to resume...)";
                    await Task.Delay(500);
                }

                // Check again in case they clicked Stop while it was paused
                if (_stopRequested) break;
                // ----------------------

                if (!track.IsSelected)
                {
                    track.Status = "Skipped";
                    continue;
                }

                if (track.Status == "Finished!")
                {
                    completedCount++;
                    continue;
                }

                track.Status = "Downloading...";
                Status = $"Processing {completedCount + 1}/{itemsToDownload.Count}...";

                try
                {
                    await _downloadService.DownloadAsync(track.Url, OutputPath, targetFormat, UpdateProgress);
                    track.Status = "Finished!";
                    completedCount++;
                }
                catch (Exception innerEx)
                {
                    track.Status = "Failed";
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Status = $"Error on {track.Title}: {innerEx.Message}";
                    });
                }
            }

            Status = _stopRequested ? "Batch Stopped Early" : "Batch Completed!";
            _isDownloading = false;
            _isPaused = false;
            OnPropertyChanged(nameof(PauseButtonText));
            RefreshCommands();
        }

        private void UpdateProgress(double progress, string fileName, string fileSize)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Progress = progress;
                if (!string.IsNullOrWhiteSpace(fileName)) FileName = fileName;
                if (!string.IsNullOrWhiteSpace(fileSize)) FileSize = fileSize;

                if (fileName == "Converting...") Status = $"Converting... {progress:0.0}%";
                else if (fileName == "Merging...") Status = "Merging...";
                else if (fileName == "Extracting audio...") Status = "Extracting audio...";
                else if (progress < 100) Status = $"Downloading... {progress:0.0}%";
                else Status = "Finishing...";
            });
        }

        public void StopDownload()
        {
            _stopRequested = true;
            _isPaused = false; // Release the pause trap if they hit stop
            OnPropertyChanged(nameof(PauseButtonText));
            _downloadService.Stop();

            Status = "Stopping...";
            _isDownloading = false;
            RefreshCommands();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}