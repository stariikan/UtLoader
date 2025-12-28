using System.Windows;
using UtLoader.ViewModels;

namespace UtLoader
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
