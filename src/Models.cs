using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public StackItem(string path, bool isDirectory, DateTime modified, long size)
        {
            Path = path;
            IsDirectory = isDirectory;
            Modified = modified;
            Size = size;
            Extension = isDirectory ? "" : System.IO.Path.GetExtension(path).ToLowerInvariant();
            // Kısayollarda ".lnk" / ".url" uzantısını gösterme
            Name = Extension is ".lnk" or ".url"
                ? System.IO.Path.GetFileNameWithoutExtension(path)
                : System.IO.Path.GetFileName(path);
        }

        public string Path { get; }
        public string Name { get; }
        public string Extension { get; }
        public bool IsDirectory { get; }
        public DateTime Modified { get; }
        public long Size { get; }

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
        public int Count => Items.Count;
        public string CountText => Items.Count == 1 ? "1 öğe" : $"{Items.Count} öğe";
        public ImageSource? Cover => Items.Count > 0 ? Items[0].Icon : null;
        public ImageSource? Preview2 => Items.Count > 1 ? Items[1].Icon : null;
        public ImageSource? Preview3 => Items.Count > 2 ? Items[2].Icon : null;

        bool _expanded;
        public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    }
}
