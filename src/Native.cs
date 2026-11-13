using System;
using System.Runtime.InteropServices;

namespace StackMeUp
{
    /// <summary>Masaüstüne yerleşme, en altta kalma ve masaüstü simgelerini gizleme.</summary>
    public static class Desktop
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? cls, string? name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? name);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int idx, IntPtr v);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int idx);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

        const int GWL_EXSTYLE = -20, GWLP_HWNDPARENT = -8;
        const long WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000;
        static readonly IntPtr HWND_BOTTOM = new(1);
        const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;
        public const int WM_WINDOWPOSCHANGING = 0x46;

        [StructLayout(LayoutKind.Sequential)]
        struct WINDOWPOS { public IntPtr hwnd, hwndInsertAfter; public int x, y, cx, cy; public uint flags; }

        /// <summary>Pencereyi masaüstüne bağlar: Alt+Tab'da görünmez, Win+D ile kaybolmaz.</summary>
        public static void Attach(IntPtr hwnd)
        {
            var progman = FindWindow("Progman", null);
            if (progman != IntPtr.Zero) SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, progman);
            long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            ex = (ex | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
            SendToBottom(hwnd);
        }

        public static void SendToBottom(IntPtr hwnd) =>
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);

        /// <summary>Pencerenin Windows başlık çubuğunu koyu/açık yapar (Windows 10 20H1+ ve 11).</summary>
        public static void SetDarkTitleBar(IntPtr hwnd, bool dark)
        {
            int v = dark ? 1 : 0;
            // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE; daha eski Windows 10 sürümlerinde 19
            if (DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, 19, ref v, sizeof(int));
        }

        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        static readonly IntPtr HWND_TOP = IntPtr.Zero, HWND_NOTOPMOST = new(-2);
        const uint GW_HWNDPREV = 3;

        /// <summary>
        /// Açılır pencereyi (WPF Popup normalde "her zaman üstte"dir) sıradan bir pencereye çevirip
        /// z-sırasında <paramref name="anchor"/>'un hemen üstüne koyar: diğer uygulamaların üstünde asılı kalmaz.
        /// </summary>
        public static void PlaceJustAbove(IntPtr popup, IntPtr anchor)
        {
            const uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE;
            SetWindowPos(popup, HWND_NOTOPMOST, 0, 0, 0, 0, flags);
            var above = GetWindow(anchor, GW_HWNDPREV);
            if (above == popup) return;
            SetWindowPos(popup, above == IntPtr.Zero ? HWND_TOP : above, 0, 0, 0, 0, flags);
        }

        /// <summary>WndProc içinden çağrılır; pencerenin diğer pencerelerin önüne geçmesini engeller.</summary>
        public static void KeepAtBottom(IntPtr lParam)
        {
            var wp = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            if ((wp.flags & SWP_NOZORDER) != 0) return;
            wp.hwndInsertAfter = HWND_BOTTOM;
            Marshal.StructureToPtr(wp, lParam, false);
        }

        static IntPtr FindDefView()
        {
            var progman = FindWindow("Progman", null);
            var dv = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (dv != IntPtr.Zero) return dv;
            IntPtr w = IntPtr.Zero;
            while ((w = FindWindowEx(IntPtr.Zero, w, "WorkerW", null)) != IntPtr.Zero)
            {
                dv = FindWindowEx(w, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (dv != IntPtr.Zero) return dv;
            }
            return IntPtr.Zero;
        }

        public static bool? IconsVisible()
        {
            var dv = FindDefView();
            if (dv == IntPtr.Zero) return null;
            var lv = FindWindowEx(dv, IntPtr.Zero, "SysListView32", null);
            return lv == IntPtr.Zero ? null : IsWindowVisible(lv);
        }

        /// <summary>Explorer'ın "Masaüstü simgelerini göster" seçeneğini açar/kapatır.</summary>
        public static void SetIconsVisible(bool visible)
        {
            var cur = IconsVisible();
            if (cur is null || cur == visible) return;
            SendMessage(FindDefView(), 0x0111 /*WM_COMMAND*/, new IntPtr(0x7402), IntPtr.Zero);
        }
    }

    /// <summary>Explorer'daki gibi küçük resim / simge üretir (IShellItemImageFactory).</summary>
    public static class ShellThumbnail
    {
        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAP { public int bmType, bmWidth, bmHeight, bmWidthBytes; public ushort bmPlanes, bmBitsPixel; public IntPtr bmBits; }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFO
        {
            public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public uint[] colors;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);
        [DllImport("gdi32.dll")] static extern int GetObject(IntPtr h, int size, out BITMAP bm);
        [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, byte[] bits, ref BITMAPINFO bmi, uint usage);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);

        /// <summary>BGRA (önceden çarpılmış alfa) piksel verisi döndürür. STA iş parçacığında çağrılmalı.</summary>
        public static (byte[] pixels, int width, int height)? Get(string path, int size)
        {
            IShellItemImageFactory? factory = null;
            IntPtr hbm = IntPtr.Zero;
            try
            {
                SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out factory);
                if (factory.GetImage(new SIZE { cx = size, cy = size }, 0 /*SIIGBF_RESIZETOFIT*/, out hbm) != 0 || hbm == IntPtr.Zero)
                    return null;
                GetObject(hbm, Marshal.SizeOf<BITMAP>(), out var bm);
                int w = bm.bmWidth, h = Math.Abs(bm.bmHeight);
                var bmi = new BITMAPINFO { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32, colors = new uint[256] };
                var buf = new byte[w * h * 4];
                var dc = GetDC(IntPtr.Zero);
                try { GetDIBits(dc, hbm, 0, (uint)h, buf, ref bmi, 0); }
                finally { ReleaseDC(IntPtr.Zero, dc); }

                // Alfa kanalı olmayan resimler (çoğu fotoğraf küçük resmi) tamamen opak olmalı
                bool anyAlpha = false;
                for (int i = 3; i < buf.Length; i += 4) if (buf[i] != 0) { anyAlpha = true; break; }
                if (!anyAlpha) for (int i = 3; i < buf.Length; i += 4) buf[i] = 255;
                return (buf, w, h);
            }
            catch { return null; }
            finally
            {
                if (hbm != IntPtr.Zero) DeleteObject(hbm);
                if (factory != null) Marshal.ReleaseComObject(factory);
            }
        }
    }
}
