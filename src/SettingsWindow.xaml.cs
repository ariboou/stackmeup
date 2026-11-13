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
        Palette _p;

        public SettingsWindow(Settings settings)
        {
            _s = settings;
            _p = Theme.ForSettings(_s);
            InitializeComponent();
            DataContext = _s;
            AutoStartBox.IsChecked = Settings.AutoStart;
            ApplyTheme();
            BuildCategories();
            RefreshFolders();
            RefreshRules();

            // Tema/renk değişince pencere anında yeniden boyansın
            _s.PropertyChanged += OnSettingChanged;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
            SourceInitialized += (_, _) => Desktop.SetDarkTitleBar(new System.Windows.Interop.WindowInteropHelper(this).Handle, _p.Dark);
            Closed += (_, _) =>
            {
                _s.PropertyChanged -= OnSettingChanged;
                Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
            };
        }

        void OnSettingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(Settings.Theme) or nameof(Settings.BackgroundColor) or nameof(Settings.Accent))
                ApplyTheme();
        }

        void OnSystemPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (e.Category == Microsoft.Win32.UserPreferenceCategory.General && _s.Theme == ThemeMode.System)
                Dispatcher.BeginInvoke(ApplyTheme);
        }

        static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

        /// <summary>Paleti pencerenin kaynaklarına yazar; tüm kontroller DynamicResource ile anında güncellenir.</summary>
        void ApplyTheme()
        {
            _p = Theme.ForSettings(_s);
            Resources["B.Window"] = B(_p.Window);
            Resources["B.Sidebar"] = B(_p.Sidebar);
            Resources["B.Surface"] = B(_p.Surface);
            Resources["B.Border"] = B(_p.Border);
            Resources["B.Text"] = B(_p.Text);
            Resources["B.SubText"] = B(_p.SubText);
            Resources["B.Control"] = B(_p.Control);
            Resources["B.ControlHover"] = B(_p.ControlHover);
            Resources["B.Track"] = B(_p.Track);
            Resources["B.Accent"] = B(_p.Accent);
            Resources["B.OnAccent"] = B(_p.OnAccent);
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero) Desktop.SetDarkTitleBar(hwnd, _p.Dark);
            BuildSwatches();
            BuildBgSwatches();
        }

        void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape) { Close(); e.Handled = true; }
        }

        // ---- Vurgu rengi ----
        void BuildSwatches()
        {
            Swatches.Children.Clear();
            foreach (var hex in AccentColors)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var dot = Dot(new SolidColorBrush(color), string.Equals(hex, _s.Accent, StringComparison.OrdinalIgnoreCase), hex);
                dot.MouseLeftButtonUp += (_, _) => _s.Accent = hex;
                Swatches.Children.Add(dot);
            }
        }

        // ---- Arka plan rengi ----
        static readonly (string Hex, string Name)[] BackgroundColors =
        {
            ("#1A1A1A", "Grafit"), ("#F6F6F7", "Beyaz"), ("#1E2A3A", "Gece mavisi"), ("#2B2238", "Mürdüm"),
            ("#3A2A1E", "Kahve"), ("#12332B", "Orman"), ("#F5EBDD", "Krem"), ("#E7F0FF", "Buz mavisi"), ("#FFE8DF", "Şeftali"),
        };

        /// <summary>Renk seçme dairesi. Seçili olan, temanın yazı renginde bir halkayla belirtilir.</summary>
        Border Dot(Brush fill, bool selected, string tip)
        {
            var inner = new Border
            {
                CornerRadius = new CornerRadius(13), Background = fill,
                BorderBrush = B(_p.Track), BorderThickness = new Thickness(1.5),
            };
            return new Border
            {
                Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Margin = new Thickness(0, 0, 8, 6),
                Padding = new Thickness(selected ? 3 : 2), Child = inner,
                Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = tip,
                BorderBrush = selected ? B(_p.Text) : Brushes.Transparent, BorderThickness = new Thickness(2),
            };
        }

        void BuildBgSwatches()
        {
            BgSwatches.Children.Clear();

            // Otomatik: temanın kendi rengi (yarı koyu yarı açık daire)
            var auto = new LinearGradientBrush(Color.FromRgb(0x1A, 0x1A, 0x1A), Color.FromRgb(0xF6, 0xF6, 0xF7), 45)
            {
                GradientStops = { new GradientStop(Color.FromRgb(0x1A, 0x1A, 0x1A), 0.5), new GradientStop(Color.FromRgb(0xF6, 0xF6, 0xF7), 0.5) },
            };
            var autoDot = Dot(auto, string.IsNullOrWhiteSpace(_s.BackgroundColor), "Temaya göre (otomatik)");
            autoDot.MouseLeftButtonUp += (_, _) => { _s.BackgroundColor = ""; BuildBgSwatches(); };
            BgSwatches.Children.Add(autoDot);

            var all = BackgroundColors.ToList();
            // Paletten olmayan özel bir renk seçildiyse onu da göster
            if (!string.IsNullOrWhiteSpace(_s.BackgroundColor) &&
                !all.Any(c => string.Equals(c.Hex, _s.BackgroundColor, StringComparison.OrdinalIgnoreCase)))
                all.Add((_s.BackgroundColor, "Özel renk"));

            foreach (var (hex, name) in all)
            {
                Color color;
                try { color = (Color)ColorConverter.ConvertFromString(hex); } catch { continue; }
                var dot = Dot(new SolidColorBrush(color), string.Equals(hex, _s.BackgroundColor, StringComparison.OrdinalIgnoreCase), $"{name}  {hex}");
                dot.MouseLeftButtonUp += (_, _) => { _s.BackgroundColor = hex; BuildBgSwatches(); };
                BgSwatches.Children.Add(dot);
            }

            var custom = new Button { Content = "Özel renk…", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(4, 0, 0, 6), VerticalAlignment = VerticalAlignment.Center };
            custom.Click += (_, _) => PickCustomBackground();
            BgSwatches.Children.Add(custom);
        }

        void PickCustomBackground()
        {
            using var dlg = new Forms.ColorDialog { FullOpen = true, AnyColor = true };
            try
            {
                if (!string.IsNullOrWhiteSpace(_s.BackgroundColor))
                {
                    var c = (Color)ColorConverter.ConvertFromString(_s.BackgroundColor);
                    dlg.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
                }
            }
            catch { }
            if (dlg.ShowDialog() != Forms.DialogResult.OK) return;
            _s.BackgroundColor = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
            BuildBgSwatches();
        }

        // ---- Kurallar ----
        void RefreshRules(int select = -1)
        {
            RulesList.ItemsSource = null;
            RulesList.ItemsSource = _s.CustomRules.ToList();
            if (select >= 0 && select < _s.CustomRules.Count) RulesList.SelectedIndex = select;
        }

        void RulesChanged(int select = -1)
        {
            _s.Touch(nameof(Settings.CustomRules));
            RefreshRules(select);
            BuildCategories();
        }

        CustomRule? ReadRuleInputs()
        {
            var name = RuleName.Text.Trim();
            var match = RuleMatch.Text.Trim();
            if (name.Length == 0 || match.Length == 0)
            {
                MessageBox.Show(this, "Yığın adı ve eşleşenler boş olamaz.\nÖrnek: Projeler  →  .aep, .prproj, .c4d", "StackMeUp");
                return null;
            }
            return new CustomRule { Name = name, Match = match };
        }

        void RulesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RulesList.SelectedItem is not CustomRule r) return;
            RuleName.Text = r.Name;
            RuleMatch.Text = r.Match;
        }

        void AddRule_Click(object sender, RoutedEventArgs e)
        {
            if (ReadRuleInputs() is not { } rule) return;
            _s.CustomRules.Add(rule);
            RulesChanged(_s.CustomRules.Count - 1);
        }

        void UpdateRule_Click(object sender, RoutedEventArgs e)
        {
            int i = RulesList.SelectedIndex;
            if (i < 0 || ReadRuleInputs() is not { } rule) return;
            _s.CustomRules[i] = rule;
            RulesChanged(i);
        }

        void RemoveRule_Click(object sender, RoutedEventArgs e)
        {
            int i = RulesList.SelectedIndex;
            if (i < 0) return;
            _s.CustomRules.RemoveAt(i);
            RuleName.Text = RuleMatch.Text = "";
            RulesChanged();
        }

        void MoveRuleUp_Click(object sender, RoutedEventArgs e)
        {
            int i = RulesList.SelectedIndex;
            if (i <= 0) return;
            (_s.CustomRules[i - 1], _s.CustomRules[i]) = (_s.CustomRules[i], _s.CustomRules[i - 1]);
            RulesChanged(i - 1);
        }

        // ---- Yığınlar ----
        void BuildCategories()
        {
            CategoriesPanel.Children.Clear();
            foreach (var cat in Categorizer.AllNames(_s.CustomRules))
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
