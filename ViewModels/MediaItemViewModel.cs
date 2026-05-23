using UtLoader.Models;

namespace UtLoader.ViewModels
{
    public class MediaItemViewModel : BaseViewModel
    {
        private readonly MediaItem _model;

        // ADDED: UI state for the CheckBox in the playlist window
        private bool _isSelected = true;

        public MediaItemViewModel(MediaItem model)
        {
            _model = model;
        }

        // ADDED: The property the XAML CheckBox will bind to
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public string Url { get => _model.Url; set { _model.Url = value; OnPropertyChanged(); } }
        public string? Title { get => _model.Title; set { _model.Title = value; OnPropertyChanged(); } }
        public string? ThumbnailUrl { get => _model.ThumbnailUrl; set { _model.ThumbnailUrl = value; OnPropertyChanged(); } }
        public MediaFormat Format { get => _model.Format; set { _model.Format = value; OnPropertyChanged(); } }
        public string OutputPath { get => _model.OutputPath; set { _model.OutputPath = value; OnPropertyChanged(); } }
        public double Progress { get => _model.Progress; set { _model.Progress = value; OnPropertyChanged(); } }
        public MediaStatus Status { get => _model.Status; set { _model.Status = value; OnPropertyChanged(); } }
        public string? Message { get => _model.Message; set { _model.Message = value; OnPropertyChanged(); } }
        public string? FileName { get => _model.FileName; set { _model.FileName = value; OnPropertyChanged(); } }
        public long? FileSize { get => _model.FileSize; set { _model.FileSize = value; OnPropertyChanged(); } }
        public MediaItem Model => _model;
    }
}