using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StackMeUp
{
    /// <summary>Simgeleri arka planda tek bir STA iş parçacığında yükler ve önbelleğe alır.</summary>
    public sealed class IconLoader
    {
        const int Size = 96; // 48 DIP'i yüksek DPI'da da net göstermek için
        readonly BlockingCollection<StackItem> _queue = new();
        readonly ConcurrentDictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);

        public IconLoader()
        {
            var t = new Thread(Run) { IsBackground = true, Name = "IconLoader" };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        static string Key(StackItem i) => i.Path + "|" + i.Modified.Ticks;

        public void Request(IEnumerable<StackItem> items)
        {
            foreach (var item in items)
            {
                if (_cache.TryGetValue(Key(item), out var img)) item.Icon = img;
                else _queue.Add(item);
            }
        }

        void Run()
        {
            foreach (var item in _queue.GetConsumingEnumerable())
            {
                var key = Key(item);
                if (!_cache.TryGetValue(key, out var img))
                {
                    var r = ShellThumbnail.Get(item.Path, Size);
                    if (r is null) continue;
                    var (px, w, h) = r.Value;
                    var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, px, w * 4);
                    bmp.Freeze();
                    img = bmp;
                    _cache[key] = img;
                }
                var finalImg = img;
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => item.Icon = finalImg);
            }
        }
    }
}
