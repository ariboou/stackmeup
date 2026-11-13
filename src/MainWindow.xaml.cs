using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        StackItem? _itemDownItem;
        // Boşluk tuşuyla önizleme için fare altındaki dosya
        StackItem? _hoverItem;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int RegisterWindowMessage(string msg);

        public event Action? ExplorerRestarted;
        public event Action? SettingsRequested;

        bool Horizontal => _s.Layout == LayoutMode.Horizontal;
        IntPtr Hwnd => new WindowInteropHelper(this).Handle;

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
            Desktop.Attach(Hwnd);
            HwndSource.FromHwnd(Hwnd)!.AddHook(WndProc);
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
            var scr = Hwnd == IntPtr.Zero ? Forms.Screen.PrimaryScreen! : Forms.Screen.FromHandle(Hwnd);
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

        /// <summary>Dikey düzende şerit, ekranda daha çok yer olan tarafa açılır.</summary>
        bool OpensLeft()
        {
            var wa = WorkArea();
            return Left + ActualWidth / 2 > wa.Left + wa.Width / 2;
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
                case nameof(Settings.ItemSize):
                case nameof(Settings.BackgroundOpacity):
                case nameof(Settings.Accent):
                case nameof(Settings.FanPreview):
                case nameof(Settings.ShowCount):
                    ApplyAppearance();
                    RefreshBody();
                    break;
                case nameof(Settings.Sort):
                case nameof(Settings.HiddenCategories):
                case nameof(Settings.ShowRecent):
                case nameof(Settings.RecentHours):
                case nameof(Settings.CustomRules):
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

            // Yığın kapakları
            double s = _s.IconSize;
            Resources["IconSize"] = s;
            Resources["CoverSize"] = s + 18;
            Resources["HeaderWidth"] = Math.Max(96, s + 48);
            Resources["FanVisibility"] = _s.FanPreview ? Visibility.Visible : Visibility.Collapsed;
            Resources["CountVisibility"] = _s.ShowCount ? Visibility.Visible : Visibility.Collapsed;

            // Açılan dosyalar
            double i = _s.ItemSize;
            double font = Math.Round(Math.Clamp(11 + (i - 24) / 10, 11, 15), 1);
            Resources["ItemIconSize"] = i;
            Resources["ItemFontSize"] = font;
            Resources["ItemTileWidth"] = Math.Max(76, i + 44);
            Resources["TileTextHeight"] = Math.Ceiling(font * 2.75);
        }

        void ApplyLayout()
        {
            foreach (var g in _groups) g.IsExpanded = false;
            ShowHBody(null, animate: false);
            ShowSide(null, animate: false);
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

        List<StackItem> Sorted(IEnumerable<StackItem> items)
        {
            var cmp = StringComparer.CurrentCultureIgnoreCase;
            return (_s.Sort switch
            {
                SortMode.Name => items.OrderBy(i => i.Name, cmp),
                SortMode.Size => items.OrderByDescending(i => i.Size).ThenBy(i => i.Name, cmp),
                SortMode.Type => items.OrderBy(i => i.Extension).ThenBy(i => i.Name, cmp),
                _ => items.OrderByDescending(i => i.Modified),
            }).ToList();
        }

        /// <summary>
        /// Klasörleri tarar. İçeriği değişmeyen yığınlar olduğu gibi korunur; yalnızca değişenler yenilenir,
        /// böylece açık yığın titremez ve kaydırma yeri kaybolmaz.
        /// </summary>
        public void Rebuild()
        {
            var rules = _s.CustomRules.ToList();
            var byCat = new Dictionary<string, List<StackItem>>(StringComparer.CurrentCultureIgnoreCase);
            var all = new List<StackItem>();
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
                        var item = new StackItem(info.FullName, isDir, info.LastWriteTime, info.CreationTime, info is FileInfo fi ? fi.Length : 0);
                        all.Add(item);
                        var cat = Categorizer.For(info.FullName, isDir, rules);
                        if (!byCat.TryGetValue(cat, out var list)) byCat[cat] = list = new List<StackItem>();
                        list.Add(item);
                    }
                    catch { /* dosya taranırken silinmiş olabilir */ }
                }
            }

            // Yeni yığın listesi (ekrandaki sırayla)
            var fresh = new List<StackGroup>();
            foreach (var name in Categorizer.AllNames(rules))
            {
                if (_s.HiddenCategories.Contains(name)) continue;
                List<StackItem> items;
                if (name == Categorizer.Recent)
                {
                    if (!_s.ShowRecent) continue;
                    var since = DateTime.Now.AddHours(-_s.RecentHours);
                    items = all.Where(i => i.Created >= since).OrderByDescending(i => i.Created).ToList();
                }
                else items = byCat.TryGetValue(name, out var l) ? Sorted(l) : new List<StackItem>();
                if (items.Count > 0) fresh.Add(new StackGroup(name, items));
            }

            // Değişmeyenleri koru, değişenleri değiştir
            var old = _groups.ToDictionary(g => g.Name, StringComparer.CurrentCultureIgnoreCase);
            var result = new List<StackGroup>();
            bool openChanged = false;
            foreach (var g in fresh)
            {
                if (old.TryGetValue(g.Name, out var prev) && prev.Signature == g.Signature)
                {
                    result.Add(prev);
                    continue;
                }
                if (prev?.IsExpanded == true) { g.IsExpanded = true; openChanged = true; }
                result.Add(g);
                _icons.Request(g.Items);
            }
            bool openRemoved = _groups.Any(g => g.IsExpanded) && !result.Any(g => g.IsExpanded);

            for (int i = 0; i < result.Count; i++)
            {
                int idx = _groups.IndexOf(result[i]);
                if (idx == i) continue;
                if (idx >= 0) _groups.Move(idx, i);
                else _groups.Insert(i, result[i]);
            }
            while (_groups.Count > result.Count) _groups.RemoveAt(_groups.Count - 1);

            _icons.Prune(result.SelectMany(g => g.Items).Select(i => i.Key).ToHashSet());

            if (openRemoved) CollapseAll();
            else if (openChanged) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, RefreshBody);
        }

        // =====================================================================
        //  Açma / kapama
        // =====================================================================

        void Toggle(StackGroup g)
        {
            bool open = !g.IsExpanded;
            foreach (var other in _groups) if (other != g) other.IsExpanded = false;
            g.IsExpanded = open;
            ShowBody(open ? g : null, animate: true);
        }

        public void CollapseAll()
        {
            foreach (var g in _groups) g.IsExpanded = false;
            ShowBody(null, animate: true);
        }

        void ShowBody(StackGroup? g, bool animate)
        {
            if (Horizontal) ShowHBody(g, animate);
            else ShowSide(g, animate);
        }

        /// <summary>Yeniden kurulumdan ya da ayar değişiminden sonra açık yığını (animasyonsuz) tekrar göster.</summary>
        void RefreshBody() => ShowBody(_groups.FirstOrDefault(g => g.IsExpanded), animate: false);

        // ---- Hareket eğrileri ----
        // Açılış: hafifçe esneyip yerine oturur. Öğeler: hızlı başlayıp yumuşakça durur. Kapanış: kısa ve sakin.
        static IEasingFunction Spring => new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.22 };
        static IEasingFunction Glide => new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5 };
        static IEasingFunction Settle => new QuadraticEase { EasingMode = EasingMode.EaseIn };

        static DoubleAnimation Anim(double? from, double to, int ms, IEasingFunction ease, int delayMs = 0) => new()
        {
            From = from, To = to, Duration = TimeSpan.FromMilliseconds(ms),
            EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(delayMs),
        };

        /// <summary>Açılan panelin animasyonlarını durdurup tam açık hâline getirir.</summary>
        static void SnapOpen(UIElement body, ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            body.BeginAnimation(OpacityProperty, null);
            scale.ScaleX = scale.ScaleY = 1;
            body.Opacity = 1;
        }

        /// <summary>Panelin açılışı: ana eksende uzar, diğer eksende hafifçe büyür, belirginleşir.</summary>
        static void AnimateOpen(UIElement body, ScaleTransform scale, bool alongY)
        {
            scale.BeginAnimation(alongY ? ScaleTransform.ScaleYProperty : ScaleTransform.ScaleXProperty, Anim(0.3, 1, 400, Spring));
            scale.BeginAnimation(alongY ? ScaleTransform.ScaleXProperty : ScaleTransform.ScaleYProperty, Anim(0.94, 1, 400, Spring));
            body.BeginAnimation(OpacityProperty, Anim(0, 1, 180, Glide));
        }

        /// <summary>Panelin kapanışı; bittiğinde <paramref name="done"/> çağrılır.</summary>
        static void AnimateClose(UIElement body, ScaleTransform scale, bool alongY, Action done)
        {
            var main = Anim(null, 0.55, 170, Settle);
            main.Completed += (_, _) => done();
            scale.BeginAnimation(alongY ? ScaleTransform.ScaleYProperty : ScaleTransform.ScaleXProperty, main);
            scale.BeginAnimation(alongY ? ScaleTransform.ScaleXProperty : ScaleTransform.ScaleYProperty, Anim(null, 0.97, 170, Settle));
            body.BeginAnimation(OpacityProperty, Anim(null, 0, 150, Settle));
        }

        // ---- Yatay düzen: başlığın altına tek sütun liste ----
        void ShowHBody(StackGroup? g, bool animate)
        {
            var scale = (ScaleTransform)HBody.RenderTransform;

            if (g is null)
            {
                if (HBody.Visibility != Visibility.Visible) return;
                if (!animate) { HBody.Visibility = Visibility.Collapsed; return; }
                AnimateClose(HBody, scale, alongY: true, () =>
                {
                    if (!_groups.Any(x => x.IsExpanded)) HBody.Visibility = Visibility.Collapsed;
                });
                return;
            }

            bool sameGroup = HBody.DataContext == g && HBody.Visibility == Visibility.Visible;
            HBody.DataContext = g;
            HBody.Visibility = Visibility.Visible;
            HList.UpdateLayout();

            double width = Math.Max(240, _s.ItemSize * 2 + 200);
            double rowW = Math.Max(HList.ActualWidth - 8, 0);
            HBody.Width = width;

            double left = 0;
            if (HList.ItemContainerGenerator.ContainerFromItem(g) is FrameworkElement c)
            {
                double x = c.TranslatePoint(new Point(0, 0), HRoot).X;
                left = Math.Clamp(x, 0, Math.Max(0, rowW - width));
            }
            HBody.Margin = new Thickness(left, 0, 0, 0);
            HBodyScroll.MaxHeight = Math.Max(160, WorkArea().Bottom - Top - HList.ActualHeight - 24);
            if (!sameGroup) HBodyScroll.ScrollToTop();

            if (!animate) { SnapOpen(HBody, scale); return; }
            AnimateOpen(HBody, scale, alongY: true);
            Cascade(HBodyItems, vertical: true, fromStart: true);
        }

        // ---- Dikey düzen: başlığın yanına yatay şerit ----
        void ShowSide(StackGroup? g, bool animate)
        {
            var scale = (ScaleTransform)SideBody.RenderTransform;

            if (g is null)
            {
                if (!SidePopup.IsOpen) return;
                if (!animate) { SidePopup.IsOpen = false; return; }
                AnimateClose(SideBody, scale, alongY: false, () =>
                {
                    if (!_groups.Any(x => x.IsExpanded)) SidePopup.IsOpen = false;
                });
                return;
            }

            VList.UpdateLayout();
            if (VList.ItemContainerGenerator.ContainerFromItem(g) is not FrameworkElement header) return;

            bool left = OpensLeft();
            var wa = WorkArea();
            double avail = left ? Left - wa.Left : wa.Right - (Left + ActualWidth);
            SideScroll.MaxWidth = Math.Max(200, avail - 40);

            bool sameGroup = SideBody.DataContext == g && SidePopup.IsOpen;
            SideBody.DataContext = g;
            SidePopup.PlacementTarget = header;
            SidePopup.Placement = left ? PlacementMode.Left : PlacementMode.Right;
            SideBody.RenderTransformOrigin = left ? new Point(1, 0.5) : new Point(0, 0.5);

            // Konumu yenilemek için kapatıp aç; şeridi başlığa dikeyde ortala
            SidePopup.IsOpen = false;
            SideBody.UpdateLayout();
            SidePopup.VerticalOffset = 0;
            SidePopup.IsOpen = true;
            SideBody.UpdateLayout();
            double headerH = header.ActualHeight - 8; // alt boşluk hariç
            SidePopup.VerticalOffset = (headerH - SideBody.ActualHeight) / 2;
            if (!sameGroup) SideScroll.ScrollToLeftEnd();
            KeepPopupNearDesktop();

            if (!animate) { SnapOpen(SideBody, scale); return; }
            AnimateOpen(SideBody, scale, alongY: false);
            Cascade(SideItems, vertical: false, fromStart: !left);
        }

        /// <summary>Açılan şerit diğer uygulamaların üstünde asılı kalmasın; panelin hemen üstünde dursun.</summary>
        void KeepPopupNearDesktop()
        {
            if (PresentationSource.FromVisual(SideBody) is HwndSource src)
                Desktop.PlaceJustAbove(src.Handle, Hwnd);
        }

        void SideScroll_Wheel(object sender, MouseWheelEventArgs e)
        {
            SideScroll.ScrollToHorizontalOffset(SideScroll.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        /// <summary>Öğeler sırayla, açılış yönünde kayarak belirir.</summary>
        static void Cascade(ItemsControl list, bool vertical, bool fromStart)
        {
            list.UpdateLayout();
            for (int i = 0; i < list.Items.Count; i++)
            {
                if (list.ItemContainerGenerator.ContainerFromIndex(i) is not ContentPresenter cp) continue;
                if (VisualTreeHelper.GetChildrenCount(cp) == 0 || VisualTreeHelper.GetChild(cp, 0) is not Border row) continue;
                // Şablondan gelen dönüşüm dondurulmuş olabilir; her öğeye kendi dönüşümünü ver
                if (row.RenderTransform is not TranslateTransform tt || tt.IsFrozen)
                {
                    tt = new TranslateTransform();
                    row.RenderTransform = tt;
                }
                int delay = 40 + Math.Min(i, 14) * 24;
                var move = Anim(fromStart ? -18 : 18, 0, 340, Glide, delay);
                move.FillBehavior = FillBehavior.Stop;
                tt.BeginAnimation(vertical ? TranslateTransform.YProperty : TranslateTransform.XProperty, move);
                row.Opacity = 0;
                row.BeginAnimation(OpacityProperty, Anim(0, 1, 240, Glide, delay));
            }
        }

        /// <summary>Başlığa basınca hafifçe içeri çöker, bırakınca yaylanarak geri gelir.</summary>
        static void Press(object sender, bool down)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.RenderTransform is not ScaleTransform st || st.IsFrozen)
            {
                st = new ScaleTransform(1, 1);
                fe.RenderTransform = st;
                fe.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            var a = down
                ? Anim(null, 0.95, 110, Glide)
                : Anim(null, 1, 360, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 });
            st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, a.Clone());
        }

        // =====================================================================
        //  Başlık: tıkla = aç/kapat, sürükle = paneli taşı
        // =====================================================================

        void Header_Down(object sender, MouseButtonEventArgs e)
        {
            _hdrGroup = ((FrameworkElement)sender).DataContext as StackGroup;
            _hdrDown = e.GetPosition(this);
            ((UIElement)sender).CaptureMouse();
            Press(sender, down: true);
            e.Handled = true;
        }

        void Header_Move(object sender, MouseEventArgs e)
        {
            if (_hdrGroup is null || e.LeftButton != MouseButtonState.Pressed || _s.LockPosition) return;
            var p = e.GetPosition(this);
            if (Math.Abs(p.X - _hdrDown.X) < 5 && Math.Abs(p.Y - _hdrDown.Y) < 5) return;

            _hdrGroup = null;
            ((UIElement)sender).ReleaseMouseCapture();
            Press(sender, down: false);
            ShowSide(null, animate: false);
            try { DragMove(); } catch (InvalidOperationException) { return; }
            _s.Left = Left;
            _s.Top = Top;
            UpdateLimits();
            RefreshBody();
        }

        void Header_Up(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)sender).ReleaseMouseCapture();
            Press(sender, down: false);
            var g = _hdrGroup;
            _hdrGroup = null;
            if (g != null) Toggle(g);
            e.Handled = true;
        }

        // =====================================================================
        //  Dosya: tıkla = aç, sürükle = başka yere bırak, Boşluk = önizle
        // =====================================================================

        void Item_Enter(object sender, MouseEventArgs e) => _hoverItem = ((FrameworkElement)sender).DataContext as StackItem;
        void Item_Leave(object sender, MouseEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext == _hoverItem) _hoverItem = null;
        }

        void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && _hoverItem != null)
            {
                Preview(_hoverItem.Path);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CollapseAll();
                e.Handled = true;
            }
        }

        void Item_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _itemDownItem = ((FrameworkElement)sender).DataContext as StackItem;
            _itemDown = e.GetPosition(this);
            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
        }

        void Item_MouseMove(object sender, MouseEventArgs e)
        {
            if (_itemDownItem is null || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(this);
            if (Math.Abs(p.X - _itemDown.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(p.Y - _itemDown.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            var item = _itemDownItem;
            _itemDownItem = null;
            ((UIElement)sender).ReleaseMouseCapture();
            var data = new DataObject(DataFormats.FileDrop, new[] { item.Path });
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
        }

        void Item_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var fe = (FrameworkElement)sender;
            fe.ReleaseMouseCapture();
            var item = _itemDownItem;
            _itemDownItem = null;
            if (item is null) return;
            var p = e.GetPosition(fe);
            if (p.X >= 0 && p.Y >= 0 && p.X <= fe.ActualWidth && p.Y <= fe.ActualHeight) Open(item.Path);
        }

        // =====================================================================
        //  Sağ tık menüleri
        // =====================================================================

        static StackItem? MenuItemOf(object sender) => (sender as FrameworkElement)?.DataContext as StackItem;

        void Menu_Open(object sender, RoutedEventArgs e) { if (MenuItemOf(sender) is { } i) Open(i.Path); }
        void Menu_Preview(object sender, RoutedEventArgs e) { if (MenuItemOf(sender) is { } i) Preview(i.Path); }
        void Menu_Reveal(object sender, RoutedEventArgs e) { if (MenuItemOf(sender) is { } i) Start("explorer.exe", $"/select,\"{i.Path}\""); }
        void Menu_CopyPath(object sender, RoutedEventArgs e)
        {
            if (MenuItemOf(sender) is { } i)
                try { Clipboard.SetText(i.Path); } catch { /* pano meşgul olabilir */ }
        }

        void Menu_Rename(object sender, RoutedEventArgs e)
        {
            if (MenuItemOf(sender) is not { } item) return;
            var current = System.IO.Path.GetFileName(item.Path);
            var newName = RenameDialog.Ask(current, item.IsDirectory);
            if (string.IsNullOrWhiteSpace(newName) || newName == current) return;
            var target = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(item.Path)!, newName);
            try
            {
                if (File.Exists(target) || Directory.Exists(target))
                    throw new IOException($"\"{newName}\" adında bir öğe zaten var.");
                if (item.IsDirectory) Directory.Move(item.Path, target);
                else File.Move(item.Path, target);
            }
            catch (Exception ex) { Warn(ex.Message); }
        }

        void Menu_Recycle(object sender, RoutedEventArgs e)
        {
            if (MenuItemOf(sender) is not { } item) return;
            var ok = MessageBox.Show($"\"{item.Name}\" Geri Dönüşüm Kutusu'na atılsın mı?", "StackMeUp",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ok != MessageBoxResult.Yes) return;
            try
            {
                if (item.IsDirectory)
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(item.Path,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                else
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(item.Path,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            catch (Exception ex) { Warn(ex.Message); }
        }

        /// <summary>Yığındaki her şeyi masaüstünde yığın adıyla bir klasöre taşır (masaüstünü gerçekten toplar).</summary>
        void Menu_Gather(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not StackGroup g) return;
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var folderName = string.Concat(g.Name.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
            if (folderName.Length == 0) folderName = "Toplanan";
            var target = System.IO.Path.Combine(desktop, folderName);

            var ok = MessageBox.Show(
                $"\"{g.Name}\" yığınındaki {g.Count} öğe şu klasöre taşınacak:\n{target}\n\nDevam edilsin mi?",
                "StackMeUp", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ok != MessageBoxResult.Yes) return;

            int failed = 0;
            try { Directory.CreateDirectory(target); }
            catch (Exception ex) { Warn(ex.Message); return; }
            foreach (var item in g.Items.ToList())
            {
                if (string.Equals(item.Path, target, StringComparison.OrdinalIgnoreCase)) continue;
                var dest = UniquePath(System.IO.Path.Combine(target, System.IO.Path.GetFileName(item.Path)));
                try
                {
                    if (item.IsDirectory) Directory.Move(item.Path, dest);
                    else File.Move(item.Path, dest);
                }
                catch { failed++; }
            }
            if (failed > 0)
                Warn($"{failed} öğe taşınamadı (açık olabilir ya da \"Ortak Masaüstü\"nde olduğu için yönetici izni gerekebilir).");
        }

        static string UniquePath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return path;
            var dir = System.IO.Path.GetDirectoryName(path)!;
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var ext = System.IO.Path.GetExtension(path);
            for (int n = 2; ; n++)
            {
                var p = System.IO.Path.Combine(dir, $"{name} ({n}){ext}");
                if (!File.Exists(p) && !Directory.Exists(p)) return p;
            }
        }

        void Menu_Settings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
        void Menu_Refresh(object sender, RoutedEventArgs e) => Rebuild();
        void Menu_CollapseAll(object sender, RoutedEventArgs e) => CollapseAll();

        // =====================================================================
        //  Yardımcılar
        // =====================================================================

        static readonly string? QuickLookExe = new[]
        {
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "QuickLook", "QuickLook.exe"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "QuickLook", "QuickLook.exe"),
        }.FirstOrDefault(File.Exists);

        /// <summary>Dosyayı açmadan önizler (QuickLook kuruluysa).</summary>
        static void Preview(string path)
        {
            if (QuickLookExe is null)
            {
                Warn("Önizleme için QuickLook uygulaması gerekiyor (Microsoft Store'da ücretsiz). Kurulu değilse dosya normal şekilde açılabilir.");
                return;
            }
            Start(QuickLookExe, $"\"{path}\"");
        }

        static void Open(string path) => Start(path, null);

        static void Start(string file, string? args)
        {
            try
            {
                var psi = new ProcessStartInfo(file) { UseShellExecute = true };
                if (args != null) psi.Arguments = args;
                Process.Start(psi);
            }
            catch (Exception ex) { Warn(ex.Message); }
        }

        static void Warn(string msg) => MessageBox.Show(msg, "StackMeUp", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
