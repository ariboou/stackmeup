using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace StackMeUp
{
    public enum LayoutMode { Horizontal, Vertical }
    public enum SortMode { Newest, Name, Size, Type }
    public enum ThemeMode { System, Dark, Light }

    /// <summary>Tüm kullanıcı ayarları. Her değişiklik PropertyChanged ile anında uygulanır ve kaydedilir.</summary>
    public sealed class Settings : Observable
    {
        // ---- Düzen ve konum ----
        LayoutMode _layout = LayoutMode.Horizontal;
        public LayoutMode Layout
        {
            get => _layout;
            set { Set(ref _layout, value); Raise(nameof(IsHorizontal)); Raise(nameof(IsVertical)); }
        }
        [JsonIgnore] public bool IsHorizontal { get => Layout == LayoutMode.Horizontal; set { if (value) Layout = LayoutMode.Horizontal; } }
        [JsonIgnore] public bool IsVertical { get => Layout == LayoutMode.Vertical; set { if (value) Layout = LayoutMode.Vertical; } }

        double? _left, _top;
        public double? Left { get => _left; set => Set(ref _left, value); }
        public double? Top { get => _top; set => Set(ref _top, value); }

        bool _lock;
        public bool LockPosition { get => _lock; set => Set(ref _lock, value); }

        // ---- Görünüm ----
        ThemeMode _theme = ThemeMode.System;
        public ThemeMode Theme
        {
            get => _theme;
            set { Set(ref _theme, value); Raise(nameof(IsThemeSystem)); Raise(nameof(IsThemeDark)); Raise(nameof(IsThemeLight)); }
        }
        [JsonIgnore] public bool IsThemeSystem { get => Theme == ThemeMode.System; set { if (value) Theme = ThemeMode.System; } }
        [JsonIgnore] public bool IsThemeDark { get => Theme == ThemeMode.Dark; set { if (value) Theme = ThemeMode.Dark; } }
        [JsonIgnore] public bool IsThemeLight { get => Theme == ThemeMode.Light; set { if (value) Theme = ThemeMode.Light; } }

        /// <summary>Başlık ve panel arka plan rengi ("#RRGGBB"). Boşsa temanın kendi rengi kullanılır.</summary>
        string _bgColor = "";
        public string BackgroundColor { get => _bgColor; set => Set(ref _bgColor, value ?? ""); }

        double _iconSize = 48;
        public double IconSize { get => _iconSize; set => Set(ref _iconSize, value); }

        double _bgOpacity = 0.65;
        public double BackgroundOpacity { get => _bgOpacity; set => Set(ref _bgOpacity, value); }

        string _accent = "#4C8DFF";
        public string Accent { get => _accent; set => Set(ref _accent, value); }

        bool _fan = true;
        public bool FanPreview { get => _fan; set => Set(ref _fan, value); }

        bool _showCount = true;
        public bool ShowCount { get => _showCount; set => Set(ref _showCount, value); }

        /// <summary>Yığın açılınca görünen dosyaların simge boyutu.</summary>
        double _itemSize = 32;
        public double ItemSize { get => _itemSize; set => Set(ref _itemSize, value); }

        // ---- Davranış ----
        SortMode _sort = SortMode.Newest;
        public SortMode Sort { get => _sort; set { Set(ref _sort, value); Raise(nameof(SortIndex)); } }
        [JsonIgnore] public int SortIndex { get => (int)Sort; set => Sort = (SortMode)value; }

        bool _showRecent = true;
        public bool ShowRecent { get => _showRecent; set => Set(ref _showRecent, value); }

        int _recentHours = 24;
        public int RecentHours { get => _recentHours; set => Set(ref _recentHours, Math.Clamp(value, 1, 24 * 30)); }

        /// <summary>Listede klasöre tıklayınca içeriği yanda yeni bir panelde açılsın (Mac gibi); kapalıysa Dosya Gezgini'nde açılır.</summary>
        bool _folderCascade = true;
        public bool FolderCascade { get => _folderCascade; set => Set(ref _folderCascade, value); }

        bool _closeOnDesktop = true;
        public bool CloseOnDesktopClick { get => _closeOnDesktop; set => Set(ref _closeOnDesktop, value); }

        /// <summary>Windows açılınca başlat. Kayıt her açılışta exe'nin güncel yoluna göre senkronlanır.</summary>
        bool _startWithWindows = true;
        public bool StartWithWindows { get => _startWithWindows; set => Set(ref _startWithWindows, value); }

        bool _checkUpdates = true;
        public bool CheckUpdates { get => _checkUpdates; set => Set(ref _checkUpdates, value); }

        bool _autoInstall = true;
        public bool AutoInstallUpdates { get => _autoInstall; set => Set(ref _autoInstall, value); }

        bool _hideIcons = true;
        public bool HideDesktopIcons { get => _hideIcons; set => Set(ref _hideIcons, value); }

        // ---- Yığınlar ve klasörler ----
        public List<string> HiddenCategories { get; set; } = new();
        public List<string> ExtraFolders { get; set; } = new();
        public List<CustomRule> CustomRules { get; set; } = new();

        /// <summary>Liste gibi içten değişen ayarlar için değişiklik bildirimi.</summary>
        public void Touch(string name) => Raise(name);

        // ---- Kalıcılık ----
        static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StackMeUp");
        static readonly string FilePath = Path.Combine(Dir, "settings.json");

        public static Settings Load()
        {
            MigrateLegacy();
            try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings(); }
            catch { return new Settings(); }
        }

        /// <summary>Eski adla ("WinStack") kaydedilmiş ayarları ve otomatik başlatma kaydını yeni ada taşır.</summary>
        static void MigrateLegacy()
        {
            try
            {
                var oldFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinStack", "settings.json");
                if (!File.Exists(FilePath) && File.Exists(oldFile))
                {
                    Directory.CreateDirectory(Dir);
                    File.Copy(oldFile, FilePath);
                }

                using var k = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (k?.GetValue("WinStack") is string)
                {
                    k.DeleteValue("WinStack", false);
                    AutoStart = true;
                }
            }
            catch { /* taşıma başarısızsa varsayılanlarla devam et */ }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* disk meşgul olabilir; bir sonraki değişiklikte tekrar denenir */ }
        }

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>Başlangıç kaydını ayara ve exe'nin şu anki yoluna göre düzeltir.</summary>
        public void SyncAutoStart()
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(RunKey);
                var want = $"\"{Environment.ProcessPath}\"";
                if (StartWithWindows)
                {
                    if (!string.Equals(k.GetValue("StackMeUp") as string, want, StringComparison.OrdinalIgnoreCase))
                        k.SetValue("StackMeUp", want);
                }
                else k.DeleteValue("StackMeUp", false);
            }
            catch { /* kayıt defterine yazılamazsa sessizce geç */ }
        }

        public static bool AutoStart
        {
            get
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey);
                return k?.GetValue("StackMeUp") is string;
            }
            set
            {
                using var k = Registry.CurrentUser.CreateSubKey(RunKey);
                if (value) k.SetValue("StackMeUp", $"\"{Environment.ProcessPath}\"");
                else k.DeleteValue("StackMeUp", false);
            }
        }
    }
}
