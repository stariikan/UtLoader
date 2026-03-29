using System.Collections.Generic;
using System.Linq;
using System.Windows;
using UtLoader.Services;

namespace UtLoader
{
    public partial class PlaylistSelectionWindow : Window
    {
        // The list that the XAML UI will bind to
        public List<PlaylistItem> Items { get; set; }

        public PlaylistSelectionWindow(List<PlaylistItem> playlistItems)
        {
            InitializeComponent();

            Items = playlistItems;

            // Set the DataContext to this window so the XAML can find the Items list
            DataContext = this;
        }

        // A helper method to easily extract only the URLs the user checked
        public List<string> GetSelectedUrls()
        {
            return Items.Where(track => track.IsSelected).Select(track => track.Url).ToList();
        }

        // Select All Logic
        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            if (Items == null) return;
            foreach (var item in Items)
            {
                item.IsSelected = true;
            }
            TracksListBox.Items.Refresh(); // Forces the UI to visually update
        }

        // Deselect All Logic
        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            if (Items == null) return;
            foreach (var item in Items)
            {
                item.IsSelected = false;
            }
            TracksListBox.Items.Refresh(); // Forces the UI to visually update
        }

        // Closes the window and tells the app the user clicked Confirm
        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        // Closes the window and tells the app the user clicked Cancel
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}