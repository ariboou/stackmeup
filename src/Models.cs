using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace StackMeUp
{
    public abstract class Observable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class StackItem : Observable
    {
        public StackItem(string path, bool isDirectory, DateTime modified, DateTime created, long size)
        {
            Path = path;
            IsDirectory = isDirectory;
            Modified = modified;
            Created = created;
            Size = size;
            Extension = isDirectory ? "" : System.IO.Path.GetExtension(path).ToLowerInvariant();
            // Kısayollarda ".lnk" / ".url" uzantısını gösterme
            Name = Extension is ".lnk" or ".url"
                ? System.IO.Path.GetFileNameWithoutExtension(path)
                : System.IO.Path.GetFileName(path);
        }

        /// <summary>
        /// Sanal Windows öğesi (Geri Dönüşüm Kutusu, Bu bilgisayar…). Path "::{CLSID}" biçimindedir.
        /// Simgesi her taramada yeniden alınır (ör. çöp kutusu dolu/boş).
        /// </summary>
        public StackItem(string shellPath, string name)
        {
            Path = shellPath;
            Name = name;
            Extension = "";
            Modified = DateTime.Now;
            Created = DateTime.MinValue;
        }

        public string Path { get; }
        public string Name { get; }
        public bool IsVirtual => Path.StartsWith("::", StringComparison.Ordinal);
        /// <summary>Gerçek dosya/klasör mü? Sağ tık menüsünde dosya işlemlerini açıp kapatmak için.</summary>
        public bool IsRealFile => !IsVirtual;
        public string Extension { get; }
        public bool IsDirectory { get; }
        public DateTime Modified { get; }
        public DateTime Created { get; }
        public long Size { get; }

        /// <summary>Dosya değişmediği sürece aynı kalır; simge önbelleği ve değişiklik tespiti bunu kullanır.</summary>
        public string Key => Path + "|" + Modified.Ticks;

        ImageSource? _icon;
        public ImageSource? Icon { get => _icon; set => Set(ref _icon, value); }
    }

    public sealed class StackGroup : Observable
    {
        public StackGroup(string name, List<StackItem> items)
        {
            Name = name;
            Items = items;
            // Kapaktaki yelpaze önizlemesi ilk üç öğenin simgesini kullanır
            for (int i = 0; i < Math.Min(3, items.Count); i++)
            {
                int idx = i;
                items[i].PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(StackItem.Icon)) return;
                    Raise(idx switch { 0 => nameof(Cover), 1 => nameof(Preview2), _ => nameof(Preview3) });
                };
            }
        }

        public string Name { get; }
        public List<StackItem> Items { get; }
        public bool IsRecent => Name == Categorizer.Recent;

        /// <summary>İçerik aynıysa aynı imza: yeniden taramada değişmeyen yığınlar olduğu gibi korunur.</summary>
        public string Signature => string.Join("\n", Items.Select(i => i.Key));
        public int Count => Items.Count;
        public string CountText => Items.Count == 1 ? "1 öğe" : $"{Items.Count} öğe";
        public ImageSource? Cover => Items.Count > 0 ? Items[0].Icon : null;
        public ImageSource? Preview2 => Items.Count > 1 ? Items[1].Icon : null;
        public ImageSource? Preview3 => Items.Count > 2 ? Items[2].Icon : null;

        bool _expanded;
        public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    }
}
