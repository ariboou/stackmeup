using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace StackMeUp
{
    public partial class SettingsWindow : Window
    {
        static readonly string[] AccentColors =
        {
            "#4C8DFF", "#7C5CFF", "#E5487F", "#FF6B3D", "#F5B700", "#2EBD85", "#14B8C4", "#8E8E93",
        };

        readonly Settings _s;

        public SettingsWindow(Settings settings)
        {
            _s = settings;
            InitializeComponent();
            DataContext = _s;
            AutoStartBox.IsChecked = Settings.AutoStart;
            BuildSwatches();
            BuildCategories();
            RefreshFolders();
        }

        // ---- Vurgu rengi ----
        void BuildSwatches()
        {
            Swatches.Children.Clear();
            foreach (var hex in AccentColors)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                bool selected = string.Equals(hex, _s.Accent, StringComparison.OrdinalIgnoreCase);
                var dot = new Border
                {
                    Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Margin = new Thickness(0, 0, 10, 0),
                    Background = new SolidColorBrush(color), Cursor = System.Windows.Input.Cursors.Hand,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x1F, 0x23, 0x28)),
                    BorderThickness = new Thickness(selected ? 3 : 0), ToolTip = hex,
                };
                dot.MouseLeftButtonUp += (_, _) => { _s.Accent = hex; BuildSwatches(); };
                Swatches.Children.Add(dot);
            }
        }

        // ---- Yığınlar ----
        void BuildCategories()
        {
            CategoriesPanel.Children.Clear();
            foreach (var cat in Categorizer.Order)
            {
                var cb = new CheckBox { Content = cat, IsChecked = !_s.HiddenCategories.Contains(cat) };
                cb.Click += (_, _) =>
                {
                    _s.HiddenCategories.Remove(cat);
                    if (cb.IsChecked != true) _s.HiddenCategories.Add(cat);
                    _s.Touch(nameof(Settings.HiddenCategories));
                };
                CategoriesPanel.Children.Add(cb);
            }
        }

        // ---- Klasörler ----
        void RefreshFolders()
        {
            FoldersList.ItemsSource = null;
            FoldersList.ItemsSource = _s.ExtraFolders.ToList();
        }

        void AddFolderPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            if (_s.ExtraFolders.Contains(path, StringComparer.OrdinalIgnoreCase)) return;
            _s.ExtraFolders.Add(path);
            _s.Touch(nameof(Settings.ExtraFolders));
            RefreshFolders();
        }

        void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new Forms.FolderBrowserDialog { Description = "Yığınlara katılacak klasörü seç", UseDescriptionForTitle = true };
            if (dlg.ShowDialog() == Forms.DialogResult.OK) AddFolderPath(dlg.SelectedPath);
        }

        void AddDownloads_Click(object sender, RoutedEventArgs e) =>
            AddFolderPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));

        void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FoldersList.SelectedItem is not string path) return;
            _s.ExtraFolders.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            _s.Touch(nameof(Settings.ExtraFolders));
            RefreshFolders();
        }

        // ---- Diğer ----
        void ResetPosition_Click(object sender, RoutedEventArgs e)
        {
            _s.Left = null;
            _s.Top = null;
        }

        void AutoStart_Click(object sender, RoutedEventArgs e)
        {
            try { Settings.AutoStart = AutoStartBox.IsChecked == true; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "StackMeUp"); }
            AutoStartBox.IsChecked = Settings.AutoStart;
        }

        void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
