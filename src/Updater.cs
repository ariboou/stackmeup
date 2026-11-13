using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace StackMeUp
{
    /// <summary>
    /// GitHub Releases üzerinden kendi kendini güncelleme.
    /// Yeni sürüm yayınlamak: csproj'daki &lt;Version&gt;'ı artır, yayinla.ps1 ile exe üret,
    /// GitHub'da "vX.Y.Z" etiketli bir Release açıp StackMeUp.exe'yi ekle. Kullanıcılar kendiliğinden alır.
    /// </summary>
    public static class Updater
    {
        public const string Repo = "ariboou/stackmeup";
        public const string RepoUrl = "https://github.com/" + Repo;
        const string AssetName = "StackMeUp.exe";

        static readonly HttpClient Http = CreateClient();

        static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("StackMeUp-Updater");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return c;
        }

        public sealed record Release(Version Version, string Tag, string PageUrl, string? ExeUrl);

        public static Version Current
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
                return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            }
        }

        public static string CurrentText => $"{Current.Major}.{Current.Minor}.{Current.Build}";

        static string ExePath => Environment.ProcessPath ?? "";

        /// <summary>
        /// Yalnızca tek dosyalık dağıtım exe'si kendini değiştirebilir. Geliştirme sürümünde (yanında DLL olan) yalnızca haber verilir.
        /// </summary>
        public static bool CanSelfUpdate =>
            ExePath.Length > 0 && !File.Exists(Path.Combine(Path.GetDirectoryName(ExePath)!, "StackMeUp.dll"));

        /// <summary>GitHub'daki en son sürümü sorar; bu sürümden yeniyse döndürür.</summary>
        public static async Task<Release?> CheckAsync()
        {
            using var resp = await Http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null; // henüz hiç sürüm yayınlanmamış
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var v)) return null;
            v = new Version(v.Major, v.Minor, Math.Max(0, v.Build));

            string? exeUrl = null;
            if (root.TryGetProperty("assets", out var assets))
                exeUrl = assets.EnumerateArray()
                    .Where(a => string.Equals(a.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase))
                    .Select(a => a.GetProperty("browser_download_url").GetString())
                    .FirstOrDefault();

            var page = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? RepoUrl : RepoUrl;
            return v > Current ? new Release(v, tag, page, exeUrl) : null;
        }

        /// <summary>Yeni exe'yi mevcut exe'nin yanına indirir (aynı sürücüde olsun ki yer değiştirme anlık olsun).</summary>
        public static async Task<string> DownloadAsync(Release r)
        {
            if (r.ExeUrl is null) throw new InvalidOperationException("Bu sürümde StackMeUp.exe dosyası yok.");
            var target = ExePath + ".new";
            using (var resp = await Http.GetAsync(r.ExeUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                await using var fs = File.Create(target);
                await resp.Content.CopyToAsync(fs);
            }
            if (new FileInfo(target).Length < 1_000_000) throw new IOException("İndirilen dosya eksik görünüyor.");
            return target;
        }

        /// <summary>
        /// Çalışan exe'nin adı değiştirilebilir (silinemez): eskisini ".old" yapıp yenisini yerine koyar ve yeniden başlatır.
        /// </summary>
        public static void ApplyAndRestart(string newExe)
        {
            var cur = ExePath;
            var old = cur + ".old";
            if (File.Exists(old)) File.Delete(old);
            File.Move(cur, old);
            try { File.Move(newExe, cur); }
            catch { File.Move(old, cur); throw; } // geri al
            Process.Start(new ProcessStartInfo(cur, "--updated") { UseShellExecute = true });
        }

        /// <summary>Önceki güncellemeden kalan eski exe'yi temizler.</summary>
        public static void CleanupOld()
        {
            try
            {
                foreach (var f in new[] { ExePath + ".old", ExePath + ".new" })
                    if (File.Exists(f)) File.Delete(f);
            }
            catch { /* hâlâ kilitliyse bir sonraki açılışta */ }
        }
    }
}
