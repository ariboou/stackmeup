using System;
using System.Collections.Generic;
using System.IO;

namespace StackMeUp
{
    /// <summary>Dosyaları uzantısına göre yığın kategorilerine ayırır.</summary>
    public static class Categorizer
    {
        // Yığınların ekranda görünme sırası
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

        public static string For(string path, bool isDirectory)
        {
            if (isDirectory) return "Klasörler";
            return Map.TryGetValue(Path.GetExtension(path), out var c) ? c : "Diğer";
        }
    }
}
