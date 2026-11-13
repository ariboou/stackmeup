using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace StackMeUp
{
    public partial class MainWindow : Window
    {
        const double ScreenPad = 16;

        readonly Settings _s;
        readonly IconLoader _icons = new();
        readonly ObservableCollection<StackGroup> _groups = new();
        readonly List<FileSystemWatcher> _watchers = new();
        readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(400) };
        readonly int _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");

        // Başlık: tıklama mı sürükleme mi?
        Point _hdrDown;
        StackGroup? _hdrGroup;
        // Dosya: tıklama mı sürükle-bırak mı?
        Point _itemDown;
        StackItem? _itemGroupDown;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int RegisterWindowMessage(string msg);

        public event Action? ExplorerRestarted;
        public event Action? SettingsRequested;

        bool Horizontal => _s.Layout == LayoutMode.Horizontal;

        public MainWindow(Settings settings)
        {
            _s = settings;
            InitializeComponent();
            ApplyAppearance();
            ApplyLayout();

            _debounce.Tick += (_, _) => { _debounce.Stop(); Rebuild(); };
            // Masaüstü parçası asla simge durumuna küçülmemeli (Win+D vb.)
            StateChanged += (_, _) => { if (WindowState != WindowState.Normal) WindowState = WindowState.Normal; };
            Deactivated += (_, _) => { if (_s.CloseOnDesktopClick) CollapseAll(); };
            SystemParameters.StaticPropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SystemParameters.WorkArea)) Dispatcher.BeginInvoke(UpdateLimits);
            };
            Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, PlaceInitial);

            ResetWatchers();
            Rebuild();
        }

        // =====================================================================
        //  Masaüstüne yerleşme
        // =====================================================================

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            Desktop.Attach(hwnd);
            HwndSource.FromHwnd(hwnd)!.AddHook(WndProc);
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Desktop.WM_WINDOWPOSCHANGING) Desktop.KeepAtBottom(lParam);
            else if (msg == _taskbarCreatedMsg)
            {
                Desktop.Attach(hwnd);
                ExplorerRestarted?.Invoke();
            }
            return IntPtr.Zero;
        }

        /// <summary>Pencerenin bulunduğu ekranın çalışma alanı (DIP cinsinden).</summary>
        Rect WorkArea()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var scr = hwnd == IntPtr.Zero ? Forms.Screen.PrimaryScreen! : Forms.Screen.FromHandle(hwnd);
            var dpi = VisualTreeHelper.GetDpi(this);
            var wa = scr.WorkingArea;
            return new Rect(wa.Left / dpi.DpiScaleX, wa.Top / dpi.DpiScaleY, wa.Width / dpi.DpiScaleX, wa.Height / dpi.DpiScaleY);
        }

        void PlaceInitial()
        {
            var vs = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                              SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            if (_s.Left is double l && _s.Top is double t && vs.Contains(new Point(l + 20, t + 20)))
            {
                Left = l;
                Top = t;
                UpdateLimits();
            }
            else PlaceDefault();
        }

        /// <summary>Varsayılan konum: ana ekranın sağ üst köşesi.</summary>
        public void PlaceDefault()
        {
            var wa = SystemParameters.WorkArea;
            Top = wa.Top + ScreenPad;
            Left = wa.Right - Math.Max(ActualWidth, 120) - ScreenPad;
            UpdateLimits();
        }

        /// <summary>Panel ekrandan taşmasın: yatay sırayı ve yüksekliği sınırla.</summary>
        void UpdateLimits()
        {
            var wa = WorkArea();
            MaxHeight = Math.Max(200, wa.Bottom - Top - 8);
            HList.MaxWidth = Math.Max(140, wa.Right - Left - 8);
        }

        // =====================================================================
        //  Ayarlar
        // =====================================================================

        public void OnSettingChanged(string? name)
        {
            switch (name)
            {
                case nameof(Settings.Layout):
                    ApplyLayout();
                    break;
                case nameof(Settings.IconSize):
                case nameof(Settings.BackgroundOpacity):
                case nameof(Settings.Accent):
                case nameof(Settings.FanPreview):
                case nameof(Settings.ShowCount):
                    ApplyAppearance();
                    if (Horizontal) RefreshHBody();
                    break;
                case nameof(Settings.Sort):
                case nameof(Settings.HiddenCategories):
                    Rebuild();
                    break;
                case nameof(Settings.ExtraFolders):
                    ResetWatchers();
                    Rebuild();
                    break;
                case nameof(Settings.Left):
                case nameof(Settings.Top):
                    if (_s.Left is null || _s.Top is null) PlaceDefault();
                    break;
            }
        }

        static SolidColorBrush Brush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        static byte A(double v) => (byte)Math.Clamp(v * 255, 0, 255);

        void ApplyAppearance()
        {
            Color accent;
            try { accent = (Color)ColorConverter.ConvertFromString(_s.Accent); }
            catch { accent = Color.FromRgb(0x4C, 0x8D, 0xFF); }

            double o = _s.BackgroundOpacity;
            Resources["HeaderBrush"] = Brush(Color.FromArgb(A(o), 0x1A, 0x1A, 0x1A));
            Resources["HeaderHoverBrush"] = Brush(Color.FromArgb(A(o + 0.15), 0x2A, 0x2A, 0x2A));
            Resources["HeaderActiveBrush"] = Brush(Color.FromArgb(A(o + 0.15),
                (byte)(0x1A + (accent.R - 0x1A) * 0.25), (byte)(0x1A + (accent.G - 0x1A) * 0.25), (byte)(0x1A + (accent.B - 0x1A) * 0.25)));
            Resources["PanelBrush"] = Brush(Color.FromArgb(A(o + 0.2), 0x14, 0x14, 0x14));
            Resources["AccentBrush"] = Brush(accent);

            double s = _s.IconSize;
            Resources["IconSize"] = s;
            Resources["RowIconSize"] = Math.Round(Math.Max(24, s * 0.6));
            Resources["CoverSize"] = s + 18;
            Resources["HeaderWidth"] = Math.Max(96, s + 48);
            Resources["VerticalWidth"] = 300.0;
            Resources["FanVisibility"] = _s.FanPreview ? Visibility.Visible : Visibility.Collapsed;
            Resources["CountVisibility"] = _s.ShowCount ? Visibility.Visible : Visibility.Collapsed;
        }

        void ApplyLayout()
        {
            CollapseAll();
            VList.ItemsSource = Horizontal ? null : _groups;
            HList.ItemsSource = Horizontal ? _groups : null;
            VList.Visibility = Horizontal ? Visibility.Collapsed : Visibility.Visible;
            HRoot.Visibility = Horizontal ? Visibility.Visible : Visibility.Collapsed;
        }

        // =====================================================================
        //  Tarama
        // =====================================================================

        IEnumerable<string> SourceFolders() =>
            new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            }
            .Concat(_s.ExtraFolders)
            .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        void ResetWatchers()
        {
            foreach (var w in _watchers) w.Dispose();
            _watchers.Clear();
            foreach (var dir in SourceFolders())
            {
                try
                {
                    var w = new FileSystemWatcher(dir)
                    {
                        IncludeSubdirectories = false,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Attributes,
                    };
                    w.Created += (_, _) => ScheduleRebuild();
                    w.Deleted += (_, _) => ScheduleRebuild();
                    w.Changed += (_, _) => ScheduleRebuild();
                    w.Renamed += (_, _) => ScheduleRebuild();
                    w.EnableRaisingEvents = true;
                    _watchers.Add(w);
                }
                catch { /* erişilemeyen klasörü atla */ }
            }
        }

        void ScheduleRebuild() => Dispatcher.BeginInvoke(() => { _debounce.Stop(); _debounce.Start(); });

        /// <summary>Klasörleri tarar ve yığınları kurar; açık olan yığın açık kalır.</summary>
        public void Rebuild()
        {
            var expanded = _groups.Where(g => g.IsExpanded).Select(g => g.Name).ToHashSet();
            var entries = new List<(string cat, StackItem item)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in SourceFolders())
            {
                List<FileSystemInfo> infos;
                try { infos = new DirectoryInfo(dir).EnumerateFileSystemInfos().ToList(); }
                catch { continue; }
                foreach (var info in infos)
                {
                    try
                    {
                        if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                        if (!seen.Add(info.FullName)) continue;
                        bool isDir = info is DirectoryInfo;
                        var cat = Categorizer.For(info.FullName, isDir);
                        if (_s.HiddenCategories.Contains(cat)) continue;
                        long size = info is FileInfo fi ? fi.Length : 0;
                        entries.Add((cat, new StackItem(info.FullName, isDir, info.LastWriteTime, size)));
                    }
                    catch { /* dosya taranırken silinmiş olabilir */ }
                }
            }

            _groups.Clear();
            foreach (var cat in Categorizer.Order)
            {
                var items = entries.Where(e => e.cat == cat).Select(e => e.item);
                var cmp = StringComparer.CurrentCultureIgnoreCase;
                items = _s.Sort switch
                {
                    SortMode.Name => items.OrderBy(i => i.Name, cmp),
                    SortMode.Size => items.OrderByDescending(i => i.Size).ThenBy(i => i.Name, cmp),
                    SortMode.Type => items.OrderBy(i => i.Extension).ThenBy(i => i.Name, cmp),
                    _ => items.OrderByDescending(i => i.Modified),
                };
                var list = items.ToList();
                if (list.Count == 0) continue;
                _groups.Add(new StackGroup(cat, list) { IsExpanded = expanded.Contains(cat) });
                _icons.Request(list);
            }

            if (Horizontal) RefreshHBody();
        }

        // =====================================================================
        //  Açma / kapama
        // =====================================================================

        void Toggle(StackGroup g)
        {
            bool open = !g.IsExpanded;
            if (Horizontal || _s.SingleOpen)
                foreach (var other in _groups) if (other != g) other.IsExpanded = false;
            g.IsExpanded = open;
            if (Horizontal) ShowHBody(open ? g : null, animate: true);
        }

        public void CollapseAll()
        {
            foreach (var g in _groups) g.IsExpanded = false;
            if (Horizontal) ShowHBody(null, animate: true);
        }

        /// <summary>Yatay düzende yeniden kurulumdan sonra açık yığını (animasyonsuz) tekrar göster.</summary>
        void RefreshHBody() => ShowHBody(_groups.FirstOrDefault(g => g.IsExpanded), animate: false);

        void ShowHBody(StackGroup? g, bool animate)
        {
            var scale = (ScaleTransform)HBody.LayoutTransform;
            var dur = TimeSpan.FromMilliseconds(animate ? 240 : 0);

            if (g is null)
            {
                if (HBody.Visibility != Visibility.Visible) return;
                if (!animate) { HBody.Visibility = Visibility.Collapsed; return; }
                var close = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
                close.Completed += (_, _) =>
                {
                    if (!_groups.Any(x => x.IsExpanded)) HBody.Visibility = Visibility.Collapsed;
                };
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, close);
                HBody.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)));
                return;
            }

            HBody.DataContext = g;
            HBody.Visibility = Visibility.Visible;
            HList.UpdateLayout();

            // Tek sütunluk liste: dosya adları okunacak kadar geniş
            const double width = 260;
            double rowW = Math.Max(HList.ActualWidth - 8, 0);
            HBody.Width = width;

            // Listeyi tıklanan yığının hemen altından başlat (sıranın dışına taşmasın)
            double left = 0;
            if (HList.ItemContainerGenerator.ContainerFromItem(g) is FrameworkElement c)
            {
                double x = c.TranslatePoint(new Point(0, 0), HRoot).X;
                left = Math.Clamp(x, 0, Math.Max(0, rowW - width));
            }
            HBody.Margin = new Thickness(left, 0, 0, 0);

            // Uzun listeler ekranın altına kadar uzar, sonra kendi içinde kayar
            HBodyScroll.MaxHeight = Math.Max(160, WorkArea().Bottom - Top - HList.ActualHeight - 24);
            HBodyScroll.ScrollToTop();

            if (!animate)
            {
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                HBody.BeginAnimation(OpacityProperty, null);
                scale.ScaleY = 1;
                HBody.Opacity = 1;
                return;
            }
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(0, 1, dur) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            HBody.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            DropRows();
        }

        /// <summary>Satırlar sırayla, yukarıdan aşağı "düşerek" belirir.</summary>
        void DropRows()
        {
            HBody.UpdateLayout();
            for (int i = 0; i < HBodyItems.Items.Count; i++)
            {
                if (HBodyItems.ItemContainerGenerator.ContainerFromIndex(i) is not ContentPresenter cp) continue;
                if (VisualTreeHelper.GetChildrenCount(cp) == 0 || VisualTreeHelper.GetChild(cp, 0) is not Border row) continue;
                // Şablondan gelen dönüşüm dondurulmuştur; canlandırmak için satıra kendi kopyasını ver
                if (row.RenderTransform is not TranslateTransform tt || tt.IsFrozen)
                {
                    tt = new TranslateTransform();
                    row.RenderTransform = tt;
                }

                var delay = TimeSpan.FromMilliseconds(Math.Min(i, 15) * 22);
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                tt.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(-14, 0, TimeSpan.FromMilliseconds(220)) { BeginTime = delay, EasingFunction = ease, FillBehavior = FillBehavior.Stop });
                row.Opacity = 0;
                row.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { BeginTime = delay, FillBehavior = FillBehavior.HoldEnd });
            }
        }

        // =====================================================================
        //  Başlık: tıkla = aç/kapat, sürükle = paneli taşı
        // =====================================================================

        void Header_Down(object sender, MouseButtonEventArgs e)
        {
            _hdrGroup = ((FrameworkElement)sender).DataContext as StackGroup;
            _hdrDown = e.GetPosition(this);
            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
        }

        void Header_Move(object sender, MouseEventArgs e)
        {
            if (_hdrGroup is null || e.LeftButton != MouseButtonState.Pressed || _s.LockPosition) return;
            var p = e.GetPosition(this);
            if (Math.Abs(p.X - _hdrDown.X) < 5 && Math.Abs(p.Y - _hdrDown.Y) < 5) return;

            _hdrGroup = null;
            ((UIElement)sender).ReleaseMouseCapture();
            try { DragMove(); } catch (InvalidOperationException) { return; }
            _s.Left = Left;
            _s.Top = Top;
            UpdateLimits();
            if (Horizontal) RefreshHBody();
        }

        void Header_Up(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)sender).ReleaseMouseCapture();
            var g = _hdrGroup;
            _hdrGroup = null;
            if (g != null) Toggle(g);
            e.Handled = true;
        }

        // =====================================================================
        //  Dosya: tıkla = aç, sürükle = başka yere bırak
        // =====================================================================

        void Item_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _itemGroupDown = ((FrameworkElement)sender).DataContext as StackItem;
            _itemDown = e.GetPosition(this);
            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
        }

        void Item_MouseMove(object sender, MouseEventArgs e)
        {
            if (_itemGroupDown is null || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(this);
            if (Math.Abs(p.X - _itemDown.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(p.Y - _itemDown.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            var item = _itemGroupDown;
            _itemGroupDown = null;
            ((UIElement)sender).ReleaseMouseCapture();
            var data = new DataObject(DataFormats.FileDrop, new[] { item.Path });
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
        }

        void Item_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var fe = (FrameworkElement)sender;
            fe.ReleaseMouseCapture();
            var item = _itemGroupDown;
            _itemGroupDown = null;
            if (item is null) return;
            var p = e.GetPosition(fe);
            if (p.X >= 0 && p.Y >= 0 && p.X <= fe.ActualWidth && p.Y <= fe.ActualHeight) Open(item.Path);
        }

        // =====================================================================
        //  Sağ tık menüleri
        // =====================================================================

        static StackItem? MenuItemOf(object sender) => (sender as FrameworkElement)?.DataContext as StackItem;

        void Menu_Open(object sender, RoutedEventArgs e) { if (MenuItemOf(sender) is { } i) Open(i.Path); }
        void Menu_Reveal(object sender, RoutedEventArgs e) { if (MenuItemOf(sender) is { } i) Start("explorer.exe", $"/select,\"{i.Path}\""); }
        void Menu_CopyPath(object sender, RoutedEventArgs e)
        {
            if (MenuItemOf(sender) is { } i)
                try { Clipboard.SetText(i.Path); } catch { /* pano meşgul olabilir */ }
        }
        void Menu_Settings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
        void Menu_Refresh(object sender, RoutedEventArgs e) => Rebuild();
        void Menu_CollapseAll(object sender, RoutedEventArgs e) => CollapseAll();

        static void Open(string path) => Start(path, null);

        static void Start(string file, string? args)
        {
            try
            {
                var psi = new ProcessStartInfo(file) { UseShellExecute = true };
                if (args != null) psi.Arguments = args;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "StackMeUp", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
