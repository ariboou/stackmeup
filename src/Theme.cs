using System;
using System.Windows.Media;

namespace StackMeUp
{
    /// <summary>Ayarlar penceresinin renkleri (tema + kullanıcının seçtiği arka plan renginden türetilir).</summary>
    public sealed class Palette
    {
        public bool Dark;
        public Color Window, Sidebar, Surface, Border, Text, SubText, Control, ControlHover, Track, Accent, OnAccent;
    }

    /// <summary>Masaüstü paneli ve ayarlar penceresi renklerini aynı yerden hesaplar; ikisi hep uyumlu kalır.</summary>
    public static class Theme
    {
        public static readonly Color DarkBase = Color.FromRgb(0x1C, 0x1C, 0x1F);
        public static readonly Color LightBase = Color.FromRgb(0xF3, 0xF4, 0xF6);
        static readonly Color DefaultAccent = Color.FromRgb(0x4C, 0x8D, 0xFF);

        public static Color Parse(string? hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch { return fallback; }
        }

        public static Color Mix(Color a, Color b, double t) => Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

        public static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

        /// <summary>Windows "uygulama modu" koyu mu? (Ayarlar > Kişiselleştirme > Renkler)</summary>
        public static bool WindowsDark()
        {
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return k?.GetValue("AppsUseLightTheme") is int v ? v == 0 : true;
            }
            catch { return true; }
        }

        public static bool ThemeIsDark(Settings s) => s.Theme switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => WindowsDark(),
        };

        /// <summary>Zemin rengi (kullanıcının rengi ya da temanınki) ve bu zeminin koyu olup olmadığı.</summary>
        public static (Color Base, bool IsDark) Base(Settings s)
        {
            var c = ThemeIsDark(s) ? DarkBase : LightBase;
            c = Parse(s.BackgroundColor, c);
            return (c, Luminance(c) < 0.5);
        }

        public static Color Accent(Settings s) => Parse(s.Accent, DefaultAccent);

        public static Palette ForSettings(Settings s)
        {
            var (b, dark) = Base(s);
            var accent = Accent(s);
            var toward = dark ? Colors.White : Colors.Black;
            return new Palette
            {
                Dark = dark,
                Window = b,
                Sidebar = Mix(b, Colors.Black, dark ? 0.22 : 0.04),
                Surface = dark ? Mix(b, Colors.White, 0.05) : Mix(b, Colors.White, 0.85),
                Border = Mix(dark ? Mix(b, Colors.White, 0.05) : b, toward, dark ? 0.10 : 0.10),
                Text = dark ? Color.FromRgb(0xF3, 0xF3, 0xF5) : Color.FromRgb(0x1F, 0x23, 0x28),
                SubText = dark ? Color.FromRgb(0xA1, 0xA1, 0xAA) : Color.FromRgb(0x6B, 0x72, 0x80),
                Control = dark ? Mix(b, Colors.White, 0.09) : Mix(b, Colors.White, 0.95),
                ControlHover = Mix(dark ? Mix(b, Colors.White, 0.09) : b, toward, dark ? 0.08 : 0.06),
                Track = Mix(b, toward, dark ? 0.22 : 0.14),
                Accent = accent,
                OnAccent = Luminance(accent) > 0.62 ? Color.FromRgb(0x1F, 0x23, 0x28) : Colors.White,
            };
        }
    }
}
