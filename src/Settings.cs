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


        // ---- Davranış ----
        SortMode _sort = SortMode.Newest;
        public SortMode Sort { get => _sort; set { Set(ref _sort, value); Raise(nameof(SortIndex)); } }
        [JsonIgnore] public int SortIndex { get => (int)Sort; set => Sort = (SortMode)value; }

        bool _singleOpen;
        public bool SingleOpen { get => _singleOpen; set => Set(ref _singleOpen, value); }

        bool _closeOnDesktop = true;
        public bool CloseOnDesktopClick { get => _closeOnDesktop; set => Set(ref _closeOnDesktop, value); }

        bool _hideIcons = true;
        public bool HideDesktopIcons { get => _hideIcons; set => Set(ref _hideIcons, value); }

        // ---- Yığınlar ve klasörler ----
        public List<string> HiddenCategories { get; set; } = new();
        public List<string> ExtraFolders { get; set; } = new();

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
