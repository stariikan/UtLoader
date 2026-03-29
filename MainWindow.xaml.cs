using System.ComponentModel;
using System.Windows;
using UtLoader.ViewModels;

namespace UtLoader
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Retrieve the ViewModel and force a stop to kill any background processes
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.StopDownload();
            }

            base.OnClosing(e);
        }
    }
}