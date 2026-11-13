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
            bool justUpdated = Array.Exists(e.Args, a => a.Equals("--updated", StringComparison.OrdinalIgnoreCase));
            _mutex = new Mutex(true, "StackMeUp.SingleInstance", out bool created);
            if (!created && justUpdated)
            {
                // Güncellemeden sonra: eski sürümün kapanmasını bekle
                try { created = _mutex.WaitOne(TimeSpan.FromSeconds(20)); }
                catch (AbandonedMutexException) { created = true; }
            }
            if (!created) { Shutdown(); return; }

            base.OnStartup(e);
            _settings = Settings.Load();
            Updater.CleanupOld();

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

            // Güncellemeler: açılıştan biraz sonra, sonra 6 saatte bir GitHub'a sor
            if (justUpdated)
                _tray!.ShowBalloonTip(4000, "StackMeUp güncellendi", $"Sürüm {Updater.CurrentText} kullanılıyor.", Forms.ToolTipIcon.Info);
            var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            updateTimer.Tick += async (_, _) =>
            {
                updateTimer.Interval = TimeSpan.FromHours(6);
                if (_settings.CheckUpdates) await CheckForUpdatesAsync(manual: false);
            };
            updateTimer.Start();

            _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); _settings.Save(); };
            _settings.PropertyChanged += (_, a) =>
            {
                _saveTimer.Stop();
                _saveTimer.Start();
                if (a.PropertyName == nameof(Settings.HideDesktopIcons)) ApplyIconHiding();
                else _win?.OnSettingChanged(a.PropertyName);
            };
        }

        // ---- Güncelleme ----
        Updater.Release? _pendingUpdate;
        bool _updating, _restartingForUpdate;

        /// <summary>GitHub'da yeni sürüm var mı bakar; varsa ayara göre kurar ya da haber verir. Sonucu metin olarak döndürür.</summary>
        public async System.Threading.Tasks.Task<string> CheckForUpdatesAsync(bool manual)
        {
            if (_updating) return "Güncelleme zaten sürüyor…";
            Updater.Release? r;
            try { r = await Updater.CheckAsync(); }
            catch (Exception ex)
            {
                if (manual) return "Denetlenemedi: internet bağlantısını kontrol et.";
                Log(ex);
                return "";
            }
            if (r is null) return $"En güncel sürümü kullanıyorsun ({Updater.CurrentText}).";

            _pendingUpdate = r;
            if (Updater.CanSelfUpdate && r.ExeUrl != null && (_settings.AutoInstallUpdates || manual))
            {
                await InstallUpdateAsync(r);
                return $"Sürüm {r.Version} indiriliyor…";
            }
            _tray?.ShowBalloonTip(6000, $"StackMeUp {r.Version} hazır",
                Updater.CanSelfUpdate ? "Kurmak için tıkla." : "İndirme sayfasını açmak için tıkla.", Forms.ToolTipIcon.Info);
            return $"Yeni sürüm var: {r.Version}.";
        }

        async System.Threading.Tasks.Task InstallUpdateAsync(Updater.Release r)
        {
            if (_updating) return;
            _updating = true;
            try
            {
                var file = await Updater.DownloadAsync(r);
                _settings.Save();
                Updater.ApplyAndRestart(file);
                _restartingForUpdate = true;
                Shutdown();
            }
            catch (Exception ex)
            {
                Log(ex);
                _tray?.ShowBalloonTip(6000, "Güncelleme kurulamadı",
                    "İndirme sayfası açılıyor; yeni sürümü elle indirebilirsin.", Forms.ToolTipIcon.Warning);
                OpenUrl(r.PageUrl);
            }
            finally { _updating = false; }
        }

        void OnBalloonClicked()
        {
            if (_pendingUpdate is not { } r) return;
            if (Updater.CanSelfUpdate && r.ExeUrl != null) _ = InstallUpdateAsync(r);
            else OpenUrl(r.PageUrl);
        }

        static void OpenUrl(string url)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
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
            menu.Items.Add("Güncellemeleri denetle", null, async (_, _) =>
            {
                var msg = await CheckForUpdatesAsync(manual: true);
                if (msg.Length > 0 && !_updating) _tray?.ShowBalloonTip(4000, "StackMeUp", msg, Forms.ToolTipIcon.Info);
            });
            menu.Items.Add("Çıkış", null, (_, _) => Shutdown());

            _tray = new Forms.NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Text = "StackMeUp",
                Visible = true,
                ContextMenuStrip = menu,
            };
            _tray.MouseDoubleClick += (_, a) => { if (a.Button == Forms.MouseButtons.Left) ShowSettings(); };
            _tray.BalloonTipClicked += (_, _) => OnBalloonClicked();
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
            // Güncelleme için yeniden başlarken simgeleri bir an açıp kapatmayalım
            if (!_restartingForUpdate) RestoreIcons();
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
