using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using UtLoader.Services;

namespace UtLoader.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _url = "";
        private string _outputPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        private string _status = "";
        private string _fileName = "";
        private string _fileSize = "";
        private string _format = "";
        private double _progress;
        private bool _isMp3 = true;
        private bool _isDownloading = false;

        private readonly DownloadService _downloadService;

        public MainViewModel()
        {
            _downloadService = new DownloadService();

            BrowseCommand = new RelayCommand(_ => BrowseFolder());
            DownloadCommand = new RelayCommand(async _ => await DownloadAsync(), _ => CanDownload());
            StopCommand = new RelayCommand(_ => StopDownload());
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
            set
            {
                _isMp3 = value;
                OnPropertyChanged(nameof(IsMp3));
                OnPropertyChanged(nameof(IsMp4));
            }
        }

        public bool IsMp4
        {
            get => !_isMp3;
            set
            {
                _isMp3 = !value;
                OnPropertyChanged(nameof(IsMp3));
                OnPropertyChanged(nameof(IsMp4));
            }
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

        private bool CanDownload() =>
            !_isDownloading && !string.IsNullOrWhiteSpace(Url);

        private void RefreshCommands()
        {
            if (DownloadCommand is RelayCommand rc)
                rc.RaiseCanExecuteChanged();
        }

        private async Task DownloadAsync()
        {
            _isDownloading = true;
            RefreshCommands();

            Status = "Starting...";
            Format = IsMp3 ? "MP3" : "MP4";
            FileName = "";
            FileSize = "";
            Progress = 0;

            try
            {
                await _downloadService.DownloadAsync(
                    Url,
                    OutputPath,
                    IsMp3,
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
        }


        private void StopDownload()
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