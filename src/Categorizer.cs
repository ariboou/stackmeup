using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StackMeUp
{
    /// <summary>Kullanıcının tanımladığı yığın: uzantılar (".aep") ve/veya ad içindeki kelimeler ("fatura").</summary>
    public sealed class CustomRule
    {
        public string Name { get; set; } = "";
        /// <summary>Virgülle ayrılmış: "." ile başlayanlar uzantı, diğerleri dosya adında aranan kelime.</summary>
        public string Match { get; set; } = "";

        public override string ToString() => $"{Name}  —  {Match}";

        IEnumerable<string> Tokens() =>
            Match.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        public bool IsMatch(string path, bool isDirectory)
        {
            var name = Path.GetFileName(path);
            var ext = isDirectory ? "" : Path.GetExtension(path);
            foreach (var t in Tokens())
            {
                if (t.StartsWith('.'))
                {
                    if (!isDirectory && string.Equals(ext, t, StringComparison.OrdinalIgnoreCase)) return true;
                }
                else if (name.Contains(t, StringComparison.CurrentCultureIgnoreCase)) return true;
            }
            return false;
        }
    }

    /// <summary>Dosyaları yığınlara ayırır: önce kullanıcı kuralları, sonra uzantıya göre hazır türler.</summary>
    public static class Categorizer
    {
        public const string Recent = "Son eklenenler";

        // Hazır yığınların ekranda görünme sırası
        public static readonly string[] Order =
        {
            "Klasörler", "Kısayollar", "Resimler", "Videolar", "Müzik", "PDF'ler",
            "Belgeler", "Tablolar", "Sunumlar", "Arşivler", "Uygulamalar", "Kod", "Diğer"
        };

        static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase);

        static Categorizer()
        {
            Add("Kısayollar", ".lnk", ".url", ".appref-ms");
            Add("Resimler", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic", ".heif", ".tif", ".tiff", ".svg", ".ico", ".psd", ".ai", ".raw", ".cr2", ".nef", ".avif");
            Add("Videolar", ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".webm", ".m4v", ".flv", ".mpg", ".mpeg", ".prproj", ".aep");
            Add("Müzik", ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".wma", ".aiff");
            Add("PDF'ler", ".pdf");
            Add("Belgeler", ".doc", ".docx", ".txt", ".rtf", ".odt", ".md", ".pages", ".epub");
            Add("Tablolar", ".xls", ".xlsx", ".xlsm", ".csv", ".ods", ".numbers");
            Add("Sunumlar", ".ppt", ".pptx", ".odp", ".key");
            Add("Arşivler", ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso");
            Add("Uygulamalar", ".exe", ".msi", ".bat", ".cmd", ".ps1");
            Add("Kod", ".cs", ".js", ".ts", ".py", ".html", ".css", ".json", ".xml", ".java", ".cpp", ".c", ".h", ".go", ".rs", ".php", ".sql", ".yml", ".yaml");
        }

        static void Add(string cat, params string[] exts)
        {
            foreach (var e in exts) Map[e] = cat;
        }

        /// <summary>Tüm yığın adları, ekrandaki sırayla: Son eklenenler, kullanıcı kuralları, hazır türler.</summary>
        public static List<string> AllNames(IEnumerable<CustomRule> rules) =>
            new[] { Recent }
                .Concat(rules.Select(r => r.Name).Where(n => !string.IsNullOrWhiteSpace(n)))
                .Concat(Order)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        public static string For(string path, bool isDirectory, IEnumerable<CustomRule> rules)
        {
            foreach (var r in rules)
                if (!string.IsNullOrWhiteSpace(r.Name) && r.IsMatch(path, isDirectory)) return r.Name;
            if (isDirectory) return "Klasörler";
            return Map.TryGetValue(Path.GetExtension(path), out var c) ? c : "Diğer";
        }
    }
}
