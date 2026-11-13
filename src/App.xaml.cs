using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace StackMeUp
{
    public partial class App : Application
    {
        Mutex? _mutex;
        MainWindow? _win;
        SettingsWindow? _settingsWin;
        Settings _settings = new();
        Forms.NotifyIcon? _tray;
        readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

        static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StackMeUp", "error.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, "StackMeUp.SingleInstance", out bool created);
            if (!created) { Shutdown(); return; }

            base.OnStartup(e);
            _settings = Settings.Load();

            // Ne olursa olsun masaüstü simgeleri gizli kalmasın
            AppDomain.CurrentDomain.UnhandledException += (_, a) => { Log(a.ExceptionObject); RestoreIcons(); };
            DispatcherUnhandledException += (_, a) => { Log(a.Exception); a.Handled = true; };

            _win = new MainWindow(_settings);
            _win.ExplorerRestarted += ApplyIconHiding;
            _win.SettingsRequested += ShowSettings;
            _win.Show();
            ApplyIconHiding();
            CreateTray();

            // "StackMeUp.exe --ayarlar": açılır açılmaz ayarlar penceresini göster
            if (Array.Exists(e.Args, a => a.Equals("--ayarlar", StringComparison.OrdinalIgnoreCase)))
                Dispatcher.BeginInvoke(ShowSettings);

            // Başka bir masaüstü aracı kapanırken simgeleri geri açarsa tekrar gizle
            var guard = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            guard.Tick += (_, _) =>
            {
                if (_settings.HideDesktopIcons && Desktop.IconsVisible() == true) Desktop.SetIconsVisible(false);
            };
            guard.Start();

            _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); _settings.Save(); };
            _settings.PropertyChanged += (_, a) =>
            {
                _saveTimer.Stop();
                _saveTimer.Start();
                if (a.PropertyName == nameof(Settings.HideDesktopIcons)) ApplyIconHiding();
                else _win?.OnSettingChanged(a.PropertyName);
            };
        }

        void ApplyIconHiding() => Desktop.SetIconsVisible(!_settings.HideDesktopIcons);

        void RestoreIcons()
        {
            if (_settings.HideDesktopIcons) Desktop.SetIconsVisible(true);
        }

        void ShowSettings()
        {
            if (_settingsWin is { IsLoaded: true })
            {
                _settingsWin.Activate();
                return;
            }
            _settingsWin = new SettingsWindow(_settings);
            _settingsWin.Closed += (_, _) => _settingsWin = null;
            _settingsWin.Show();
            _settingsWin.Activate();
        }

        void CreateTray()
        {
            var menu = new Forms.ContextMenuStrip();
            var settingsItem = menu.Items.Add("Ayarlar…", null, (_, _) => ShowSettings());
            settingsItem.Font = new System.Drawing.Font(settingsItem.Font, System.Drawing.FontStyle.Bold);
            menu.Items.Add("Yenile", null, (_, _) => _win!.Rebuild());
            menu.Items.Add("Tümünü kapat", null, (_, _) => _win!.CollapseAll());
            menu.Items.Add("Masaüstü klasörünü aç", null, (_, _) =>
                System.Diagnostics.Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)));
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Çıkış", null, (_, _) => Shutdown());

            _tray = new Forms.NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Text = "StackMeUp",
                Visible = true,
                ContextMenuStrip = menu,
            };
            _tray.MouseDoubleClick += (_, a) => { if (a.Button == Forms.MouseButtons.Left) ShowSettings(); };
        }

        /// <summary>Tepsi için ICO'dan tam tepsi boyutundaki (DPI'ya göre 16/20/24 px) kareyi seçer.</summary>
        static System.Drawing.Icon LoadTrayIcon()
        {
            try
            {
                var res = GetResourceStream(new Uri("pack://application:,,,/assets/StackMeUp.ico"));
                if (res != null)
                    using (res.Stream)
                        return new System.Drawing.Icon(res.Stream, Forms.SystemInformation.SmallIconSize);
            }
            catch { /* kaynak bulunamazsa exe ikonuna düş */ }
            return System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _settings.Save();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            RestoreIcons();
            base.OnExit(e);
        }

        static void Log(object? ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"[{DateTime.Now:u}] {ex}\n\n");
            }
            catch { }
        }
    }
}
