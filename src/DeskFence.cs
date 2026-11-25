using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace DeskFence
{
    // =====================================================================
    //  Native helpers
    // =====================================================================
    internal static class Native
    {
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const int WS_CHILD = 0x40000000;
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WM_MOUSEACTIVATE = 0x0021;
        public const int MA_NOACTIVATE = 3;

        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_LARGEICON = 0x0;
        public const uint SHGFI_SYSICONINDEX = 0x4000;

        public const int SHIL_EXTRALARGE = 2;
        public const int SHIL_JUMBO = 4;
        public const int ILD_TRANSPARENT = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IImageList
        {
            [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, out int pi);
            [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, out int pi);
            [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
            [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
            [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, out int pi);
            [PreserveSig] int Draw(IntPtr pimldp);
            [PreserveSig] int Remove(int i);
            [PreserveSig] int GetIcon(int i, int flags, out IntPtr picon);
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint attrs, ref SHFILEINFO psfi, uint cbFileInfo, uint flags);
        [DllImport("shell32.dll")]
        public static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

        // --- IShellItemImageFactory: render an item's icon at an exact size ---
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER2
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO2
        {
            public BITMAPINFOHEADER2 bmiHeader;
            public int bmiColors;
        }

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        public static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, IntPtr lpvBits,
            ref BITMAPINFO2 lpbmi, uint usage);
        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate pfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        // --- per-pixel alpha (layered window) ---
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public int bmiColors;
        }

        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObj);
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);
        [DllImport("user32.dll")]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        /// <summary>Push an ARGB bitmap to a layered window, premultiplying alpha on the way.</summary>
        public static bool PresentLayered(IntPtr hwnd, Bitmap bmp, int screenX, int screenY)
        {
            int w = bmp.Width, h = bmp.Height;
            if (w <= 0 || h <= 0) return false;
            IntPtr screenDc = IntPtr.Zero, memDc = IntPtr.Zero, dib = IntPtr.Zero, oldObj = IntPtr.Zero;
            try
            {
                screenDc = GetDC(IntPtr.Zero);
                memDc = CreateCompatibleDC(screenDc);
                var bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = w;
                bmi.bmiHeader.biHeight = -h; // top-down
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0;
                IntPtr bits;
                dib = CreateDIBSection(memDc, ref bmi, 0, out bits, IntPtr.Zero, 0);
                if (dib == IntPtr.Zero || bits == IntPtr.Zero) return false;
                oldObj = SelectObject(memDc, dib);

                System.Drawing.Imaging.BitmapData data = bmp.LockBits(
                    new Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    unsafe
                    {
                        byte* src = (byte*)data.Scan0;
                        byte* dst = (byte*)bits;
                        int srcStride = data.Stride;
                        int dstStride = w * 4;
                        for (int y = 0; y < h; y++)
                        {
                            byte* s = src + y * srcStride;
                            byte* d = dst + y * dstStride;
                            for (int x = 0; x < w * 4; x += 4)
                            {
                                byte a = s[x + 3];
                                if (a == 255)
                                {
                                    d[x] = s[x]; d[x + 1] = s[x + 1]; d[x + 2] = s[x + 2]; d[x + 3] = 255;
                                }
                                else if (a == 0)
                                {
                                    d[x] = 0; d[x + 1] = 0; d[x + 2] = 0; d[x + 3] = 0;
                                }
                                else
                                {
                                    d[x] = (byte)(s[x] * a / 255);
                                    d[x + 1] = (byte)(s[x + 1] * a / 255);
                                    d[x + 2] = (byte)(s[x + 2] * a / 255);
                                    d[x + 3] = a;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }

                var dstPt = new POINT { X = screenX, Y = screenY };
                var srcPt = new POINT { X = 0, Y = 0 };
                var size = new SIZE { cx = w, cy = h };
                var blend = new BLENDFUNCTION();
                blend.BlendOp = 0;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = 1; // AC_SRC_ALPHA
                return UpdateLayeredWindow(hwnd, screenDc, ref dstPt, ref size, memDc, ref srcPt, 0, ref blend, 2);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (oldObj != IntPtr.Zero && memDc != IntPtr.Zero) SelectObject(memDc, oldObj);
                if (dib != IntPtr.Zero) DeleteObject(dib);
                if (memDc != IntPtr.Zero) DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }

    // =====================================================================
    //  Desktop pinning (keep the window above the wallpaper, below every app)
    // =====================================================================
    internal static class DesktopPin
    {
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;

        public static void Sink(Form f)
        {
            if (f == null || f.IsDisposed || !f.IsHandleCreated) return;
            // HWND_BOTTOM = lowest normal window: above the wallpaper, below every app.
            Native.SetWindowPos(f.Handle, HWND_BOTTOM, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public static void Configure(Form f)
        {
            IntPtr h = f.Handle;
            int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            ex |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
            Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex);
            Sink(f);
        }
    }

    // =====================================================================
    //  Model / store
    // =====================================================================
    public class BoxData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public bool Collapsed { get; set; }
        public bool Locked { get; set; }
        public int ExpandedH { get; set; }
        public List<string> Order { get; set; }

        public BoxData() { Order = new List<string>(); }
    }

    public class AppConfig
    {
        public int Version { get; set; }
        public bool AutoStart { get; set; }
        public string Theme { get; set; }
        public int Opacity { get; set; }
        public int IconSize { get; set; }
        public List<BoxData> Boxes { get; set; }

        public AppConfig() { Version = 1; Theme = "dark"; Opacity = 22; IconSize = 48; Boxes = new List<BoxData>(); }
    }

    public static class Store
    {
        public static string RootDir
        {
            get
            {
                string custom = Environment.GetEnvironmentVariable("DESKFENCE_HOME");
                if (!string.IsNullOrEmpty(custom)) return custom;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskFence");
            }
        }
        public static string BoxesDir { get { return Path.Combine(RootDir, "boxes"); } }
        public static string ConfigPath { get { return Path.Combine(RootDir, "config.json"); } }

        public static AppConfig Config = new AppConfig();
        private static readonly object Gate = new object();
        private static System.Threading.Timer _saveTimer;

        public static void Load()
        {
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(BoxesDir);
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var ser = new JavaScriptSerializer();
                    var cfg = ser.Deserialize<AppConfig>(File.ReadAllText(ConfigPath, Encoding.UTF8));
                    if (cfg != null) Config = cfg;
                }
            }
            catch { }
            if (Config.Boxes == null) Config.Boxes = new List<BoxData>();
            if (string.IsNullOrEmpty(Config.Theme)) Config.Theme = "dark";
            if (Config.Opacity < 5 || Config.Opacity > 100) Config.Opacity = 22;
            if (Config.IconSize < 32 || Config.IconSize > 96) Config.IconSize = 48;
            foreach (var b in Config.Boxes)
            {
                if (b.Order == null) b.Order = new List<string>();
                if (string.IsNullOrEmpty(b.Id)) b.Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            }
        }

        public static void SaveNow()
        {
            lock (Gate)
            {
                try
                {
                    Directory.CreateDirectory(RootDir);
                    var ser = new JavaScriptSerializer();
                    string json = ser.Serialize(Config);
                    string tmp = ConfigPath + ".tmp";
                    File.WriteAllText(tmp, json, new UTF8Encoding(false));
                    if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
                    File.Move(tmp, ConfigPath);
                }
                catch { }
            }
        }

        public static void SaveSoon()
        {
            lock (Gate)
            {
                if (_saveTimer != null) _saveTimer.Dispose();
                _saveTimer = new System.Threading.Timer(delegate { SaveNow(); }, null, 700, Timeout.Infinite);
            }
        }

        public static string FolderFor(BoxData b)
        {
            string p = Path.Combine(BoxesDir, b.Id);
            Directory.CreateDirectory(p);
            return p;
        }

        public static string DesktopDir
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); }
        }

        public static bool SetAutoStart(bool on)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return false;
                    if (on) key.SetValue("DeskFence", "\"" + Application.ExecutablePath + "\"");
                    else if (key.GetValue("DeskFence") != null) key.DeleteValue("DeskFence", false);
                }
                return true;
            }
            catch { return false; }
        }

        public static bool IsAutoStart()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key != null && key.GetValue("DeskFence") != null;
                }
            }
            catch { return false; }
        }
    }

    // =====================================================================
    //  Icon cache
    // =====================================================================
    internal static class IconCache
    {
        private static readonly Dictionary<string, Bitmap> Cache =
            new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        public static Bitmap Get(string path, int size)
        {
            int list = size > 56 ? Native.SHIL_JUMBO : Native.SHIL_EXTRALARGE;
            string key = path + "|" + list.ToString() + "|" + size.ToString();
            Bitmap bmp;
            lock (Cache) { if (Cache.TryGetValue(key, out bmp)) return bmp; }
            bmp = LoadExact(path, size) ?? Load(path, list);
            lock (Cache) { Cache[key] = bmp; }
            return bmp;
        }

        /// <summary>Ask the shell to render the icon at exactly this size.
        /// Avoids the jumbo image-list quirk where small artwork sits in a canvas corner.</summary>
        private static Bitmap LoadExact(string path, int size)
        {
            if (size < 16 || size > 512) return null;
            try
            {
                Guid iid = typeof(Native.IShellItemImageFactory).GUID;
                Native.IShellItemImageFactory factory;
                if (Native.SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory) != 0 || factory == null)
                    return null;
                var sz = new Native.SIZE { cx = size, cy = size };
                IntPtr hbm;
                // SIIGBF_ICONONLY | SIIGBF_SCALEUP
                if (factory.GetImage(sz, 0x4 | 0x100, out hbm) != 0 || hbm == IntPtr.Zero) return null;
                try
                {
                    var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    var data = bmp.LockBits(new Rectangle(0, 0, size, size),
                        System.Drawing.Imaging.ImageLockMode.WriteOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    try
                    {
                        var bmi = new Native.BITMAPINFO2();
                        bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER2));
                        bmi.bmiHeader.biWidth = size;
                        bmi.bmiHeader.biHeight = -size;
                        bmi.bmiHeader.biPlanes = 1;
                        bmi.bmiHeader.biBitCount = 32;
                        bmi.bmiHeader.biCompression = 0;
                        IntPtr hdc = Native.GetDC(IntPtr.Zero);
                        Native.GetDIBits(hdc, hbm, 0, (uint)size, data.Scan0, ref bmi, 0);
                        Native.ReleaseDC(IntPtr.Zero, hdc);
                    }
                    finally
                    {
                        bmp.UnlockBits(data);
                    }
                    return bmp;
                }
                finally
                {
                    Native.DeleteObject(hbm);
                }
            }
            catch { return null; }
        }

        private static string ResolveShortcut(string path)
        {
            if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return path;
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return path;
                object sh = Activator.CreateInstance(t);
                object lnk = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { path });
                Type lt = lnk.GetType();
                string target = lt.InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, lnk, null) as string;
                if (!string.IsNullOrEmpty(target) && (File.Exists(target) || Directory.Exists(target))) return target;
            }
            catch { }
            return path;
        }

        private static Bitmap Load(string path, int imageList)
        {
            string iconSource = ResolveShortcut(path);
            try
            {
                var sfi = new Native.SHFILEINFO();
                Native.SHGetFileInfo(iconSource, 0, ref sfi, (uint)Marshal.SizeOf(typeof(Native.SHFILEINFO)),
                    Native.SHGFI_SYSICONINDEX);
                if (sfi.iIcon >= 0)
                {
                    Guid iid = typeof(Native.IImageList).GUID;
                    Native.IImageList list;
                    if (Native.SHGetImageList(imageList, ref iid, out list) == 0 && list != null)
                    {
                        IntPtr h;
                        if (list.GetIcon(sfi.iIcon, Native.ILD_TRANSPARENT, out h) == 0 && h != IntPtr.Zero)
                        {
                            using (var ico = Icon.FromHandle(h))
                            {
                                Bitmap copy = ico.ToBitmap();
                                Native.DestroyIcon(h);
                                if (copy.Width >= 32) return copy;
                                copy.Dispose();
                            }
                        }
                    }
                }
            }
            catch { }

            // Fallback: classic 32px shell icon
            try
            {
                var sfi = new Native.SHFILEINFO();
                IntPtr r = Native.SHGetFileInfo(iconSource, 0, ref sfi, (uint)Marshal.SizeOf(typeof(Native.SHFILEINFO)),
                    Native.SHGFI_ICON | Native.SHGFI_LARGEICON);
                if (r != IntPtr.Zero && sfi.hIcon != IntPtr.Zero)
                {
                    using (var ico = Icon.FromHandle(sfi.hIcon))
                    {
                        Bitmap b = ico.ToBitmap();
                        Native.DestroyIcon(sfi.hIcon);
                        return b;
                    }
                }
            }
            catch { }
            return null;
        }
    }

    // =====================================================================
    //  Theme palette
    // =====================================================================
    [Flags]
    internal enum ResizeEdge { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

    internal static class Palette
    {
        public static bool Light
        {
            get { return string.Equals(Store.Config.Theme, "light", StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>Veil alpha, 0-255, derived from the configured percentage.</summary>
        public static int VeilAlpha
        {
            get
            {
                int pct = Store.Config.Opacity;
                if (pct < 5 || pct > 100) pct = 22;
                return (int)Math.Round(pct * 255.0 / 100.0);
            }
        }

        public static Color Veil
        {
            get { return Light ? Color.FromArgb(VeilAlpha, 250, 251, 253) : Color.FromArgb(VeilAlpha, 24, 26, 32); }
        }

        public static Color Border { get { return Light ? Color.FromArgb(34, 20, 26, 40) : Color.FromArgb(46, 255, 255, 255); } }
        public static Color TitleText { get { return Light ? Color.FromArgb(245, 28, 32, 40) : Color.FromArgb(248, 244, 247, 252); } }
        public static Color CountText { get { return Light ? Color.FromArgb(150, 100, 106, 120) : Color.FromArgb(160, 210, 217, 228); } }
        public static Color Separator { get { return Light ? Color.FromArgb(28, 20, 26, 40) : Color.FromArgb(40, 255, 255, 255); } }
        public static Color LabelText { get { return Light ? Color.FromArgb(245, 34, 38, 48) : Color.FromArgb(245, 242, 246, 252); } }
        public static Color TextShadow { get { return Light ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(130, 0, 0, 0); } }
        public static Color HoverFill { get { return Light ? Color.FromArgb(28, 20, 26, 40) : Color.FromArgb(34, 255, 255, 255); } }
        public static Color SelectedFill { get { return Color.FromArgb(80, 96, 150, 235); } }
        public static Color Ghost { get { return Light ? Color.FromArgb(38, 20, 26, 40) : Color.FromArgb(60, 255, 255, 255); } }
        public static Color ScrollBar { get { return Light ? Color.FromArgb(60, 20, 26, 40) : Color.FromArgb(90, 255, 255, 255); } }
        public static Color Grip { get { return Light ? Color.FromArgb(90, 60, 68, 88) : Color.FromArgb(110, 240, 244, 250); } }
        public static Color LockIcon { get { return Light ? Color.FromArgb(170, 70, 78, 96) : Color.FromArgb(200, 235, 240, 248); } }
    }

    // =====================================================================
    //  Fence window
    // =====================================================================
    internal class FenceForm : Form
    {
        private readonly FenceApp _app;
        private readonly FileSystemWatcher _watcher;
        private readonly List<string> _items = new List<string>();
        private readonly List<Rectangle> _cells = new List<Rectangle>();
        private readonly Font _titleFont = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        private readonly Font _labelFont = new Font("Microsoft YaHei UI", 8f, FontStyle.Regular, GraphicsUnit.Point);

        private float S { get { return DeviceDpi / 96f; } }

        private int TitleH { get { return (int)(34 * S); } }
        private int Pad { get { return (int)(10 * S); } }
        private int IconSize
        {
            get
            {
                int px = Store.Config.IconSize;
                if (px < 32 || px > 96) px = 48;
                return (int)(px * S);
            }
        }
        private int CellW { get { return IconSize + (int)(46 * S); } }
        private int CellH { get { return IconSize + (int)(60 * S); } }

        private int _hover = -1;
        private int _selected = -1;
        private int _scrollRows;
        private bool _draggingWindow;
        private bool _draggingItem;
        private bool _resizing;
        private ResizeEdge _resizeEdge;
        private Point _dragStart;
        private Point _winStart;
        private Rectangle _startRect;
        private bool _pinned;
        private bool _titleHover;
        private bool _reordering;
        private bool _dragOver;
        private bool _mouseInside;
        private int _dropIndex = -1;
        private Native.WinEventDelegate _winEventProc;
        private IntPtr _winEventHook = IntPtr.Zero;

        private Rectangle PencilRect
        {
            get
            {
                Point c = PencilCenter;
                int r = (int)(12 * S);
                return new Rectangle(c.X - r, c.Y - r, r * 2, r * 2);
            }
        }

        private Point LockCenter
        {
            get
            {
                int countW = TextRenderer.MeasureText(_items.Count.ToString(), _titleFont).Width;
                return new Point(Width - Pad - countW - (int)(12 * S), TitleH / 2);
            }
        }

        private Point PencilCenter
        {
            get
            {
                Point lc = LockCenter;
                return Data.Locked ? new Point(lc.X - (int)(22 * S), lc.Y) : lc;
            }
        }

        public BoxData Data { get; private set; }

        public FenceForm(FenceApp app, BoxData data)
        {
            _app = app;
            Data = data;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            AllowDrop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            Bounds = new Rectangle(Data.X, Data.Y, Math.Max(220, Data.W), Math.Max(160, Data.H));
            if (Data.Collapsed) Height = TitleH + 2;
            ClampToScreen();

            _watcher = new FileSystemWatcher(Store.FolderFor(Data));
            _watcher.IncludeSubdirectories = false;
            _watcher.EnableRaisingEvents = true;
            _watcher.Created += delegate { PostReload(); };
            _watcher.Deleted += delegate { PostReload(); };
            _watcher.Renamed += delegate { PostReload(); };
            _watcher.Changed += delegate { PostReload(); };

            Reload();
        }

        private void PostReload()
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(Reload));
            }
            catch { }
        }

        public string Folder { get { return Store.FolderFor(Data); } }

        public void Reload()
        {
            _items.Clear();
            try
            {
                var files = new List<string>();
                foreach (var f in Directory.GetFileSystemEntries(Folder))
                {
                    string name = Path.GetFileName(f);
                    if (name.StartsWith("~$", StringComparison.Ordinal)) continue;
                    files.Add(f);
                }
                var ordered = new List<string>();
                foreach (var key in Data.Order)
                {
                    foreach (var f in files)
                    {
                        if (string.Equals(Path.GetFileName(f), key, StringComparison.OrdinalIgnoreCase) && !ordered.Contains(f))
                            ordered.Add(f);
                    }
                }
                foreach (var f in files)
                {
                    if (!ordered.Contains(f)) ordered.Add(f);
                }
                _items.AddRange(ordered);
                Data.Order = new List<string>();
                foreach (var f in _items) Data.Order.Add(Path.GetFileName(f));
            }
            catch { }
            if (_selected >= _items.Count) _selected = -1;
            Invalidate();
        }

        // ---------- layout ----------
        private void ClampToScreen()
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            int x = Location.X, y = Location.Y;
            if (x + 80 > vs.Right) x = vs.Right - 100;
            if (y + 40 > vs.Bottom) y = vs.Bottom - 60;
            if (x < vs.Left) x = vs.Left;
            if (y < vs.Top) y = vs.Top;
            if (x != Location.X || y != Location.Y)
            {
                Location = new Point(x, y);
                Data.X = x;
                Data.Y = y;
            }
        }

        private int Columns
        {
            get { return Math.Max(1, (ClientSize.Width - 2 * Pad) / Math.Max(1, CellW)); }
        }

        private int RowsVisible
        {
            get { return Math.Max(1, (ClientSize.Height - TitleH - Pad) / Math.Max(1, CellH)); }
        }

        private void LayoutCells()
        {
            _cells.Clear();
            int cols = Columns;
            int startY = TitleH + Pad - _scrollRows * CellH;
            for (int i = 0; i < _items.Count; i++)
            {
                int c = i % cols, r = i / cols;
                _cells.Add(new Rectangle(Pad + c * CellW, startY + r * CellH, CellW, CellH));
            }
        }

        private int RowCount { get { return (int)Math.Ceiling(_items.Count / (double)Math.Max(1, Columns)); } }

        // ---------- painting ----------
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Layered window: the surface comes from UpdateLayeredWindow.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Render();
        }

        /// <summary>Layered windows do not get WM_PAINT reliably, so paint eagerly.
        /// Public + hiding so callers outside the class repaint too.</summary>
        public new void Invalidate()
        {
            Render();
        }

        private void Render()
        {
            if (!IsHandleCreated || Width <= 0 || Height <= 0) return;
            LayoutCells();
            try
            {
                using (var bmp = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        g.Clear(Color.Transparent);
                        DrawSurface(g);
                    }
                    Native.PresentLayered(Handle, bmp, Left, Top);
                }
            }
            catch { }
        }

        private void DrawSurface(Graphics g)
        {
            var body = new Rectangle(0, 0, Width, Height);
            using (var path = RoundRect(body, (int)(10 * S)))
            {
                using (var brush = new SolidBrush(Palette.Veil))
                {
                    g.FillPath(brush, path);
                }
                using (var pen = new Pen(Palette.Border, Math.Max(1, S)))
                {
                    g.DrawPath(pen, path);
                }
            }

            // title
            var titleRect = new Rectangle(Pad, 0, Width - 2 * Pad, TitleH);
            var titleShadow = new Rectangle(titleRect.X, titleRect.Y + Math.Max(1, (int)(1 * S)), titleRect.Width, titleRect.Height);
            string count = _items.Count.ToString();
            int countW = TextRenderer.MeasureText(g, count, _titleFont).Width;
            int textRight = Width - Pad - countW - (int)(26 * S);
            var nameRect = new Rectangle(Pad, 0, Math.Max((int)(20 * S), textRight - Pad), TitleH);
            var nameShadow = new Rectangle(nameRect.X, nameRect.Y + Math.Max(1, (int)(1 * S)), nameRect.Width, nameRect.Height);

            TextRenderer.DrawText(g, Data.Name, _titleFont, nameShadow, Palette.TextShadow,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, Data.Name, _titleFont, nameRect, Palette.TitleText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, count, _titleFont, titleShadow, Palette.TextShadow,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, count, _titleFont, titleRect, Palette.CountText,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

            if (Data.Locked)
            {
                Point lc = LockCenter;
                DrawLock(g, lc.X, lc.Y);
            }
            if (_titleHover)
            {
                Point pc = PencilCenter;
                DrawPencil(g, pc.X, pc.Y);
            }

            // separator
            using (var pen = new Pen(Palette.Separator, Math.Max(1, S)))
            {
                g.DrawLine(pen, Pad, TitleH, Width - Pad, TitleH);
            }

            if (!Data.Collapsed)
            {
                g.SetClip(new Rectangle(0, TitleH, Width, Height - TitleH));
                for (int i = 0; i < _items.Count && i < _cells.Count; i++)
                {
                    DrawItem(g, i, _cells[i]);
                }
                if (_reordering || _dragOver) DrawInsertMark(g);
                g.ResetClip();

                if (RowCount > RowsVisible)
                {
                    int trackTop = TitleH + Pad;
                    int trackH = Height - TitleH - 2 * Pad;
                    int thumbH = Math.Max(24, trackH * RowsVisible / Math.Max(1, RowCount));
                    int maxScroll = Math.Max(1, RowCount - RowsVisible);
                    int thumbTop = trackTop + (trackH - thumbH) * _scrollRows / maxScroll;
                    using (var b = new SolidBrush(Palette.ScrollBar))
                    {
                        g.FillRectangle(b, Width - (int)(6 * S), thumbTop, (int)(3 * S), thumbH);
                    }
                }
            }

            // resize grip
            if (!Data.Collapsed && !Data.Locked)
            {
                using (var b = new SolidBrush(Palette.Grip))
                {
                    for (int i = 0; i < 3; i++)
                    {
                        for (int j = 0; j <= i; j++)
                        {
                            g.FillEllipse(b, Width - (int)(14 * S) + j * (int)(4 * S),
                                Height - (int)(14 * S) + (2 - i) * (int)(4 * S), (int)(2f * S), (int)(2f * S));
                        }
                    }
                }
            }
        }

        private void DrawPencil(Graphics g, int cx, int cy)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(-45f);
            using (var b = new SolidBrush(Palette.LockIcon))
            {
                g.FillRectangle(b, (float)(-2 * S), (float)(-6 * S), (float)(4 * S), (float)(10 * S));
                g.FillPolygon(b, new PointF[] {
                    new PointF((float)(-2 * S), (float)(4 * S)),
                    new PointF((float)(2 * S), (float)(4 * S)),
                    new PointF(0f, (float)(8 * S)) });
            }
            g.Restore(st);
        }

        private void DrawLock(Graphics g, int cx, int cy)
        {
            int w = (int)(11 * S), h = (int)(8 * S);
            var body = new Rectangle(cx - w / 2, cy - h / 2 + (int)(2 * S), w, h);
            using (var b = new SolidBrush(Palette.LockIcon))
            using (var p = RoundRect(body, (int)(2 * S)))
            {
                g.FillPath(b, p);
            }
            using (var pen = new Pen(Palette.LockIcon, Math.Max(1.2f, (float)(1.6 * S))))
            {
                g.DrawArc(pen, body.X + (int)(2 * S), body.Y - (int)(7 * S),
                    w - (int)(4 * S), (int)(10 * S), 180, 180);
            }
        }

        private void DrawItem(Graphics g, int index, Rectangle cell)
        {
            string path = _items[index];
            bool hovered = _mouseInside && _hover >= 0 && index == _hover;
            if (hovered || index == _selected)
            {
                var hl = new Rectangle(cell.X + (int)(3 * S), cell.Y + (int)(3 * S),
                    cell.Width - (int)(6 * S), cell.Height - (int)(6 * S));
                using (var p = RoundRect(hl, (int)(8 * S)))
                using (var b = new SolidBrush(index == _selected ? Palette.SelectedFill : Palette.HoverFill))
                {
                    g.FillPath(b, p);
                }
            }

            Bitmap icon = IconCache.Get(path, IconSize);
            var iconRect = new Rectangle(cell.X + (cell.Width - IconSize) / 2, cell.Y + (int)(8 * S), IconSize, IconSize);
            if (icon != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(icon, iconRect);
            }
            else
            {
                using (var b = new SolidBrush(Palette.Ghost))
                using (var p = RoundRect(iconRect, (int)(6 * S)))
                {
                    g.FillPath(b, p);
                }
            }

            string label = Path.GetFileName(path);
            if (label.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) label = label.Substring(0, label.Length - 4);
            int textTop = cell.Y + (int)(8 * S) + IconSize + (int)(3 * S);
            int textH = Math.Max((int)(24 * S), cell.Bottom - textTop - (int)(3 * S));
            var textRect = new Rectangle(cell.X + (int)(2 * S), textTop, cell.Width - (int)(4 * S), textH);
            var labelFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top |
                TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            var shadowRect = new Rectangle(textRect.X, textRect.Y + Math.Max(1, (int)(1 * S)), textRect.Width, textRect.Height);
            TextRenderer.DrawText(g, label, _labelFont, shadowRect, Palette.TextShadow, labelFlags);
            TextRenderer.DrawText(g, label, _labelFont, textRect, Palette.LabelText, labelFlags);
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED;
                return cp;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        // ---------- mouse ----------
        private int HitTest(Point p)
        {
            for (int i = 0; i < _cells.Count; i++)
                if (_cells[i].Contains(p) && i < _items.Count) return i;
            return -1;
        }

        private int DropIndexAt(Point p)
        {
            LayoutCells();
            if (_cells.Count == 0) return 0;
            int best = 0;
            double bestDist = double.MaxValue;
            int n = Math.Min(_cells.Count, _items.Count);
            for (int i = 0; i < n; i++)
            {
                Rectangle c = _cells[i];
                double dx = p.X - (c.Left + c.Width / 2.0);
                double dy = p.Y - (c.Top + c.Height / 2.0);
                double d = dx * dx + dy * dy;
                if (d < bestDist) { bestDist = d; best = i; }
            }
            Rectangle bc = _cells[best];
            int idx = p.X < bc.Left + bc.Width / 2 ? best : best + 1;
            return Math.Max(0, Math.Min(_items.Count, idx));
        }

        private void DrawInsertMark(Graphics g)
        {
            if (_cells.Count == 0) return;
            int k = Math.Max(0, Math.Min(_cells.Count - 1, _dropIndex < 0 ? 0 : _dropIndex));
            int x, top, bottom;
            Rectangle c = _cells[k];
            if (_dropIndex >= _cells.Count)
            {
                c = _cells[_cells.Count - 1];
                x = c.Right - (int)(6 * S);
            }
            else
            {
                x = c.Left - (int)(4 * S);
            }
            top = c.Top + (int)(6 * S);
            bottom = c.Bottom - (int)(14 * S);

            using (var b = new SolidBrush(Color.FromArgb(235, 82, 140, 248)))
            {
                int w = Math.Max(2, (int)(3 * S));
                g.FillRectangle(b, x - w / 2, top, w, bottom - top);
                int d = (int)(7 * S);
                g.FillEllipse(b, x - d / 2, top - d / 2, d, d);
                g.FillEllipse(b, x - d / 2, bottom - d / 2, d, d);
            }
        }

        private void BeginExternalDrag()
        {
            int idx = _selected;
            _draggingItem = false;
            _reordering = false;
            _dragOver = false;
            _dropIndex = -1;
            _hover = -1;
            _titleHover = false;
            Capture = false;
            Invalidate();
            if (idx < 0 || idx >= _items.Count) return;

            string src = _items[idx];
            string[] files = new string[] { src };
            var data = new DataObject(DataFormats.FileDrop, files);
            try
            {
                // Without this Explorer treats a drop from another app as a COPY.
                data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(2))); // DROPEFFECT_MOVE
            }
            catch { }

            DragDropEffects eff = DragDropEffects.None;
            try
            {
                eff = DoDragDrop(data, DragDropEffects.Move | DragDropEffects.Copy);
            }
            catch { }

            // Target claimed a move but left our copy behind: finish the move.
            if ((eff & DragDropEffects.Move) != 0)
            {
                try
                {
                    if (File.Exists(src)) File.Delete(src);
                    else if (Directory.Exists(src)) Directory.Delete(src, true);
                }
                catch { }
            }

            _selected = -1;
            Reload();
        }

        private void CommitReorder()
        {
            int from = _selected;
            int to = _dropIndex;
            _reordering = false;
            _dragOver = false;
            Capture = false;
            _dropIndex = -1;
            _hover = -1;
            _titleHover = false;
            if (from >= 0 && from < _items.Count && to >= 0)
            {
                if (to > from) to--;
                if (to < 0) to = 0;
                if (to > _items.Count - 1) to = _items.Count - 1;
                if (to != from)
                {
                    string moved = _items[from];
                    _items.RemoveAt(from);
                    _items.Insert(to, moved);
                    Data.Order.Clear();
                    foreach (string f in _items) Data.Order.Add(Path.GetFileName(f));
                    Store.SaveSoon();
                }
            }
            _selected = -1;
            Invalidate();
        }

        private ResizeEdge HitEdge(Point p)        {
            if (Data.Collapsed || Data.Locked) return ResizeEdge.None;
            int m = Math.Max(4, (int)(6 * S));
            ResizeEdge e = ResizeEdge.None;
            if (p.X <= m) e |= ResizeEdge.Left;
            else if (p.X >= Width - m) e |= ResizeEdge.Right;
            if (p.Y <= m) e |= ResizeEdge.Top;
            else if (p.Y >= Height - m) e |= ResizeEdge.Bottom;
            return e;
        }

        private static Cursor CursorFor(ResizeEdge e)
        {
            if (e == (ResizeEdge.Left | ResizeEdge.Top) || e == (ResizeEdge.Right | ResizeEdge.Bottom)) return Cursors.SizeNWSE;
            if (e == (ResizeEdge.Right | ResizeEdge.Top) || e == (ResizeEdge.Left | ResizeEdge.Bottom)) return Cursors.SizeNESW;
            if (e == ResizeEdge.Left || e == ResizeEdge.Right) return Cursors.SizeWE;
            if (e == ResizeEdge.Top || e == ResizeEdge.Bottom) return Cursors.SizeNS;
            return Cursors.Default;
        }

        // ---------- snapping ----------
        private const int SnapDistance = 10;

        private List<int> VerticalLines()
        {
            var list = new List<int>();
            Rectangle vs = SystemInformation.VirtualScreen;
            list.Add(vs.Left);
            list.Add(vs.Right);
            foreach (Rectangle b in _app.OtherBounds(this))
            {
                list.Add(b.Left);
                list.Add(b.Right);
            }
            return list;
        }

        private List<int> HorizontalLines()
        {
            var list = new List<int>();
            Rectangle vs = SystemInformation.VirtualScreen;
            list.Add(vs.Top);
            list.Add(vs.Bottom);
            foreach (Rectangle b in _app.OtherBounds(this))
            {
                list.Add(b.Top);
                list.Add(b.Bottom);
            }
            return list;
        }

        private static int SnapValue(int value, List<int> lines, int tolerance)
        {
            int best = 0, bestDist = tolerance + 1;
            foreach (int line in lines)
            {
                int d = line - value;
                if (Math.Abs(d) < bestDist) { bestDist = Math.Abs(d); best = d; }
            }
            return bestDist <= tolerance ? best : 0;
        }

        private Rectangle SnapMove(Rectangle r)
        {
            int t = Math.Max(6, (int)(SnapDistance * S));
            var xs = VerticalLines();
            var ys = HorizontalLines();
            int dx = SnapValue(r.Left, xs, t);
            int dx2 = SnapValue(r.Right, xs, t);
            if (Math.Abs(dx2) < Math.Abs(dx) && dx2 != 0) dx = dx2;
            else if (dx == 0) dx = dx2;
            int dy = SnapValue(r.Top, ys, t);
            int dy2 = SnapValue(r.Bottom, ys, t);
            if (Math.Abs(dy2) < Math.Abs(dy) && dy2 != 0) dy = dy2;
            else if (dy == 0) dy = dy2;
            r.X += dx;
            r.Y += dy;
            return r;
        }

        private Rectangle SnapResize(Rectangle r, ResizeEdge e)
        {
            int t = Math.Max(6, (int)(SnapDistance * S));
            int minW = (int)(240 * S), minH = (int)(170 * S);
            if ((e & ResizeEdge.Left) != 0)
            {
                int d = SnapValue(r.Left, VerticalLines(), t);
                if (d != 0 && r.Width - d >= minW) { r.X += d; r.Width -= d; }
            }
            if ((e & ResizeEdge.Right) != 0)
            {
                int d = SnapValue(r.Right, VerticalLines(), t);
                if (d != 0 && r.Width + d >= minW) { r.Width += d; }
            }
            if ((e & ResizeEdge.Top) != 0)
            {
                int d = SnapValue(r.Top, HorizontalLines(), t);
                if (d != 0 && r.Height - d >= minH) { r.Y += d; r.Height -= d; }
            }
            if ((e & ResizeEdge.Bottom) != 0)
            {
                int d = SnapValue(r.Bottom, HorizontalLines(), t);
                if (d != 0 && r.Height + d >= minH) { r.Height += d; }
            }
            return r;
        }

        private Rectangle ClampRect(Rectangle r)
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            int minVisible = (int)(80 * S);
            if (r.Right < vs.Left + minVisible) r.X = vs.Left + minVisible - r.Width;
            if (r.Left > vs.Right - minVisible) r.X = vs.Right - minVisible;
            if (r.Bottom < vs.Top + minVisible) r.Y = vs.Top + minVisible - r.Height;
            if (r.Top > vs.Bottom - minVisible) r.Y = vs.Bottom - minVisible;
            return r;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (!Data.Collapsed && e.Y < TitleH && PencilRect.Contains(e.Location))
            {
                _app.RenameBox(this);
                return;
            }

            ResizeEdge edge = HitEdge(e.Location);
            if (edge != ResizeEdge.None)
            {
                _resizing = true;
                _resizeEdge = edge;
                _dragStart = Cursor.Position;
                _startRect = Bounds;
                return;
            }

            if (e.Y >= TitleH)
            {
                int hit = HitTest(e.Location);
                if (hit >= 0)
                {
                    _selected = hit;
                    _draggingItem = true;
                    _dragStart = Cursor.Position;
                    Capture = true;
                    Invalidate();
                    return;
                }
            }

            if (_selected >= 0)
            {
                _selected = -1;
                Invalidate();
            }

            if (Data.Locked) return;

            _draggingWindow = true;
            _dragStart = Cursor.Position;
            _winStart = Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Safety net: if the physical button is already up but we never got the
            // matching message (capture lost), finish the interaction here.
            if ((_reordering || _draggingItem) && (Control.MouseButtons & MouseButtons.Left) == 0)
            {
                if (_reordering) CommitReorder();
                else
                {
                    _draggingItem = false;
                    _hover = -1;
                    Capture = false;
                    Invalidate();
                }
                return;
            }
            if (_draggingWindow)
            {
                Point d = new Point(Cursor.Position.X - _dragStart.X, Cursor.Position.Y - _dragStart.Y);
                Rectangle r = new Rectangle(_winStart.X + d.X, _winStart.Y + d.Y, Width, Height);
                r = ClampRect(SnapMove(r));
                Location = new Point(r.X, r.Y);
                return;
            }
            if (_resizing)
            {
                Point d = new Point(Cursor.Position.X - _dragStart.X, Cursor.Position.Y - _dragStart.Y);
                Rectangle r = _startRect;
                int minW = (int)(240 * S), minH = (int)(170 * S);
                if ((_resizeEdge & ResizeEdge.Left) != 0) { r.X = _startRect.X + d.X; r.Width = _startRect.Width - d.X; }
                if ((_resizeEdge & ResizeEdge.Right) != 0) r.Width = _startRect.Width + d.X;
                if ((_resizeEdge & ResizeEdge.Top) != 0) { r.Y = _startRect.Y + d.Y; r.Height = _startRect.Height - d.Y; }
                if ((_resizeEdge & ResizeEdge.Bottom) != 0) r.Height = _startRect.Height + d.Y;
                if (r.Width < minW) { if ((_resizeEdge & ResizeEdge.Left) != 0) r.X = _startRect.Right - minW; r.Width = minW; }
                if (r.Height < minH) { if ((_resizeEdge & ResizeEdge.Top) != 0) r.Y = _startRect.Bottom - minH; r.Height = minH; }
                Bounds = SnapResize(r, _resizeEdge);
                return;
            }
            if (_draggingItem && !_reordering)
            {
                if (Math.Abs(Cursor.Position.X - _dragStart.X) > 4 * S || Math.Abs(Cursor.Position.Y - _dragStart.Y) > 4 * S)
                {
                    if (ClientRectangle.Contains(e.Location))
                    {
                        _reordering = true;
                        _dropIndex = DropIndexAt(e.Location);
                        Invalidate();
                    }
                    else
                    {
                        BeginExternalDrag();
                    }
                }
                return;
            }

            if (_reordering)
            {
                if (!ClientRectangle.Contains(e.Location))
                {
                    BeginExternalDrag();
                    return;
                }
                int di = DropIndexAt(e.Location);
                if (di != _dropIndex)
                {
                    _dropIndex = di;
                    Invalidate();
                }
                return;
            }

            ResizeEdge edge = HitEdge(e.Location);
            bool overTitle = !Data.Collapsed && e.Y < TitleH;
            Cursor wanted = edge != ResizeEdge.None ? CursorFor(edge)
                : (overTitle && PencilRect.Contains(e.Location) ? Cursors.Hand
                : (HitTest(e.Location) >= 0 ? Cursors.Hand : Cursors.Default));
            int hover = HitTest(e.Location);
            if (hover != _hover || Cursor != wanted || overTitle != _titleHover)
            {
                _hover = hover;
                _titleHover = overTitle;
                Cursor = wanted;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_reordering)
            {
                CommitReorder();
                return;
            }
            if (_draggingWindow || _resizing)
            {
                _draggingWindow = false;
                _resizing = false;
                Data.X = Location.X;
                Data.Y = Location.Y;
                Data.W = Width;
                if (!Data.Collapsed) { Data.H = Height; Data.ExpandedH = Height; }
                Store.SaveSoon();
                DesktopPin.Sink(this);
            }
            _draggingItem = false;
            _reordering = false;
            _dragOver = false;
            _dropIndex = -1;
            Capture = false;
            if (!ClientRectangle.Contains(e.Location))
            {
                // released outside: MouseLeave was suppressed during the drag
                _hover = -1;
                _titleHover = false;
            }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _mouseInside = true;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _mouseInside = false;
            if (_draggingItem || _reordering) return;
            bool dirty = _hover >= 0 || _titleHover || _dragOver || _dropIndex >= 0;
            _hover = -1;
            _titleHover = false;
            _dragOver = false;
            _dropIndex = -1;
            Cursor = Cursors.Default;
            if (_selected >= 0) { _selected = -1; dirty = true; }
            if (dirty) Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (e.Y < TitleH)
            {
                ToggleCollapse();
                return;
            }
            int hit = HitTest(e.Location);
            if (hit >= 0 && hit < _items.Count) OpenItem(_items[hit]);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int maxScroll = Math.Max(0, RowCount - RowsVisible);
            _scrollRows = Math.Max(0, Math.Min(maxScroll, _scrollRows - Math.Sign(e.Delta)));
            Invalidate();
        }

        public void ToggleCollapse()
        {
            if (!Data.Collapsed)
            {
                Data.ExpandedH = Height;
                Data.Collapsed = true;
                Height = TitleH + 2;
            }
            else
            {
                Data.Collapsed = false;
                Height = Math.Max((int)(170 * S), Data.ExpandedH > 0 ? Data.ExpandedH : (int)(320 * S));
            }
            Data.H = Height;
            Store.SaveSoon();
            Invalidate();
        }

        private void OpenItem(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开：\n" + ex.Message, "DeskFence");
            }
        }

        // ---------- drop in ----------
        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Move : DragDropEffects.None;
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            bool ok = e.Data.GetDataPresent(DataFormats.FileDrop);
            e.Effect = ok ? DragDropEffects.Move : DragDropEffects.None;
            if (!ok)
            {
                if (_dragOver) { _dragOver = false; _dropIndex = -1; Invalidate(); }
                return;
            }
            Point p = PointToClient(new Point(e.X, e.Y));
            int di = DropIndexAt(p);
            if (di != _dropIndex || !_dragOver)
            {
                _dropIndex = di;
                _dragOver = true;
                Invalidate();
            }
        }

        protected override void OnDragLeave(EventArgs e)
        {
            base.OnDragLeave(e);
            if (!_reordering && (_dragOver || _dropIndex >= 0))
            {
                _dragOver = false;
                _dropIndex = -1;
                Invalidate();
            }
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            int insertAt = _dropIndex;
            _dropIndex = -1;
            _dragOver = false;
            if (files == null)
            {
                Invalidate();
                return;
            }

            var imported = new List<string>();
            foreach (string f in files)
            {
                string name = ImportFile(f, e.KeyState, false);
                if (name != null) imported.Add(name);
            }
            // Nothing was actually taken: tell the source "no move" so it keeps its file.
            if (imported.Count == 0) e.Effect = DragDropEffects.None;
            Reload();

            if (insertAt >= 0 && imported.Count > 0)
            {
                var moved = new List<string>();
                foreach (string nm in imported)
                {
                    int idx = _items.FindIndex(x => string.Equals(Path.GetFileName(x), nm, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0)
                    {
                        moved.Add(_items[idx]);
                        _items.RemoveAt(idx);
                    }
                }
                insertAt = Math.Max(0, Math.Min(insertAt, _items.Count));
                _items.InsertRange(insertAt, moved);
                Data.Order.Clear();
                foreach (string f in _items) Data.Order.Add(Path.GetFileName(f));
            }
            Store.SaveSoon();
            Invalidate();
        }

        public void CollectDesktopShortcuts()
        {
            string desktop = Store.DesktopDir;
            int moved = 0;
            try
            {
                foreach (string file in Directory.GetFiles(desktop, "*.lnk"))
                {
                    try
                    {
                        ImportFile(file, 0, true);
                        moved++;
                    }
                    catch { }
                }
            }
            catch { }
            Reload();
            Store.SaveSoon();
            if (moved == 0)
            {
                MessageBox.Show(this, "桌面上没有可收纳的快捷方式（.lnk）。\n" +
                    "「此电脑」「回收站」这类系统图标不是文件，无法移动。",
                    "DeskFence");
            }
        }

        public void ImportFile(string src, int keyState)
        {
            ImportFile(src, keyState, false);
        }

        public string ImportFile(string src, int keyState, bool quiet)
        {
            try
            {
                if (!File.Exists(src) && !Directory.Exists(src)) return null;
                string name = Path.GetFileName(src);
                string dest = Path.Combine(Folder, name);
                if (string.Equals(Path.GetFullPath(src), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase)) return null;
                dest = MakeUnique(dest);
                if (Directory.Exists(src)) MoveDirectory(src, dest);
                else File.Move(src, dest);
                return Path.GetFileName(dest);
            }
            catch (Exception ex)
            {
                if (!quiet) MessageBox.Show(this, "移入失败：" + Path.GetFileName(src) + "\n" + ex.Message, "DeskFence");
                return null;
            }
        }

        private static string MakeUnique(string dest)
        {
            if (!File.Exists(dest) && !Directory.Exists(dest)) return dest;
            string dir = Path.GetDirectoryName(dest);
            string name = Path.GetFileNameWithoutExtension(dest);
            string ext = Path.GetExtension(dest);
            for (int i = 2; i < 1000; i++)
            {
                string cand = Path.Combine(dir, name + " (" + i + ")" + ext);
                if (!File.Exists(cand) && !Directory.Exists(cand)) return cand;
            }
            return dest;
        }

        private static void MoveDirectory(string src, string dest)
        {
            try { Directory.Move(src, dest); }
            catch { }
        }

        // ---------- context menu ----------
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)Native.MA_NOACTIVATE;
                return;
            }
            // "Show desktop" (Win+D) must not swallow the fence.
            if (m.Msg == 0x0112 && ((m.WParam.ToInt64() & 0xFFF0) == 0xF020))
            {
                m.Result = IntPtr.Zero;
                return;
            }
            // Capture lost before the button-up arrived: settle the drag state.
            if (m.Msg == 0x0215)
            {
                if (_reordering) CommitReorder();
                else if (_draggingItem)
                {
                    _draggingItem = false;
                    _hover = -1;
                    Invalidate();
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Right)
            {
                int hit = HitTest(e.Location);
                if (hit >= 0 && hit < _items.Count) ShowItemMenu(_items[hit]);
                else ShowBoxMenu();
            }
        }

        private static ToolStripMenuItem Mi(string text, EventHandler h)
        {
            var it = new ToolStripMenuItem(text);
            if (h != null) it.Click += h;
            return it;
        }

        private void ShowBoxMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(Mi("重命名此盒…", delegate { _app.RenameBox(this); }));

            var lockItem = Mi(Data.Locked ? "解锁（允许移动和缩放）" : "锁定位置和大小", delegate { ToggleLock(); });
            lockItem.Checked = Data.Locked;
            menu.Items.Add(lockItem);

            var themeItem = new ToolStripMenuItem("外观");
            var colorItem = new ToolStripMenuItem("蒙版颜色");
            var lightItem = new ToolStripMenuItem("浅灰") { Checked = Palette.Light };
            lightItem.Click += delegate { _app.SetTheme("light"); };
            var darkItem = new ToolStripMenuItem("暗灰（默认）") { Checked = !Palette.Light };
            darkItem.Click += delegate { _app.SetTheme("dark"); };
            colorItem.DropDownItems.Add(lightItem);
            colorItem.DropDownItems.Add(darkItem);
            themeItem.DropDownItems.Add(colorItem);

            var opacityItem = new ToolStripMenuItem("蒙版浓度");
            foreach (int pct in new int[] { 8, 15, 22, 35, 55, 100 })
            {
                int p = pct;
                var it = new ToolStripMenuItem(p + "%" + (p == 22 ? "（默认）" : "")) { Checked = Store.Config.Opacity == p };
                it.Click += delegate { _app.SetOpacity(p); };
                opacityItem.DropDownItems.Add(it);
            }
            themeItem.DropDownItems.Add(opacityItem);

            var sizeItem = new ToolStripMenuItem("图标大小");
            foreach (int px in new int[] { 36, 48, 64, 80 })
            {
                int p = px;
                var it = new ToolStripMenuItem(px + " px" + (p == 48 ? "（默认）" : "")) { Checked = Store.Config.IconSize == p };
                it.Click += delegate { _app.SetIconSize(p); };
                sizeItem.DropDownItems.Add(it);
            }
            themeItem.DropDownItems.Add(sizeItem);
            menu.Items.Add(themeItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Mi("新建收纳盒", delegate { _app.NewBox(); }));
            menu.Items.Add(Mi("收纳桌面上的全部快捷方式", delegate { CollectDesktopShortcuts(); }));
            menu.Items.Add(Mi("整理此盒（按名称排序）", delegate
            {
                _items.Sort(StringComparer.CurrentCultureIgnoreCase);
                Data.Order.Clear();
                foreach (var f in _items) Data.Order.Add(Path.GetFileName(f));
                Store.SaveSoon();
                Invalidate();
            }));
            menu.Items.Add(Mi("打开此盒文件夹", delegate
            {
                try { Process.Start("explorer.exe", "\"" + Folder + "\""); } catch { }
            }));

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Mi(Data.Collapsed ? "展开" : "折叠", delegate { ToggleCollapse(); }));
            menu.Items.Add(Mi("把内容移回桌面并删除此盒", delegate { _app.RemoveBox(this); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Mi("设置…", delegate { _app.ShowSettings(); }));
            menu.Show(Cursor.Position);
        }

        public void ToggleLock()
        {
            Data.Locked = !Data.Locked;
            if (Data.Locked && _resizing) _resizing = false;
            Store.SaveSoon();
            Cursor = Cursors.Default;
            Invalidate();
        }

        public void ResetScroll()
        {
            _scrollRows = 0;
        }

        private void ShowItemMenu(string path)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(Mi("打开", delegate { OpenItem(path); }));
            menu.Items.Add(Mi("打开文件所在位置", delegate
            {
                try { Process.Start("explorer.exe", "/select,\"" + path + "\""); } catch { }
            }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Mi("移回桌面", delegate
            {
                try
                {
                    string dest = MakeUnique(Path.Combine(Store.DesktopDir, Path.GetFileName(path)));
                    if (Directory.Exists(path)) Directory.Move(path, dest);
                    else File.Move(path, dest);
                    Reload();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "DeskFence"); }
            }));
            menu.Items.Add(Mi("重命名…", delegate { _app.RenameItem(this, path); }));
            menu.Items.Add(Mi("删除到回收站", delegate
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    else
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    Reload();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "DeskFence"); }
            }));
            menu.Show(Cursor.Position);
        }

        // ---------- lifetime ----------
        public void SaveState()
        {
            if (Data.Collapsed)
            {
                Data.X = Location.X;
                Data.Y = Location.Y;
                Data.W = Width;
            }
            else
            {
                Data.X = Location.X;
                Data.Y = Location.Y;
                Data.W = Width;
                Data.H = Height;
                Data.ExpandedH = Height;
            }
            Store.SaveSoon();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                _winEventProc = delegate
                {
                    if (IsDisposed) return;
                    if (_draggingItem || _reordering || _dragOver) return;
                    if (_selected >= 0 || _hover >= 0 || _dropIndex >= 0)
                    {
                        _selected = -1;
                        _hover = -1;
                        _dropIndex = -1;
                        Invalidate();
                    }
                };
                // EVENT_SYSTEM_FOREGROUND: another window got focus -> drop the selection.
                _winEventHook = Native.SetWinEventHook(3, 3, IntPtr.Zero, _winEventProc, 0, 0, 2 | 4);
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_pinned) return;
            DesktopPin.Configure(this);
            _pinned = true;
            Render();

            // At logon the shell may still be starting up: re-anchor a few times.
            var sinkTimer = new System.Windows.Forms.Timer();
            int ticks = 0;
            sinkTimer.Interval = 1500;
            sinkTimer.Tick += delegate
            {
                ticks++;
                DesktopPin.Sink(this);
                Render();
                if (ticks >= 4)
                {
                    sinkTimer.Stop();
                    sinkTimer.Dispose();
                }
            };
            sinkTimer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { _watcher.EnableRaisingEvents = false; _watcher.Dispose(); } catch { }
            try { if (_winEventHook != IntPtr.Zero) Native.UnhookWinEvent(_winEventHook); } catch { }
            SaveState();
            base.OnFormClosing(e);
        }
    }

    // =====================================================================
    //  Simple input dialog
    // =====================================================================
    internal static class Ui
    {
        public static float Scale()
        {
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f;
            }
            catch { return 1f; }
        }

        public static Button MakeButton(string text, DialogResult result, Rectangle bounds)
        {
            var b = new Button();
            b.Text = text;
            b.DialogResult = result;
            b.Bounds = bounds;
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.AutoSize = false;
            b.UseVisualStyleBackColor = true;
            return b;
        }
    }

    internal class InputDialog : Form
    {
        private readonly TextBox _box;

        public string Value { get { return _box.Text.Trim(); } }

        public InputDialog(string title, string label, string value)
        {
            float s = Ui.Scale();
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size((int)(390 * s), (int)(158 * s));
            Font = new Font("Microsoft YaHei UI", 9f);

            int pad = (int)(18 * s);
            int bw = (int)(88 * s), bh = (int)(34 * s), gap = (int)(10 * s);
            int by = ClientSize.Height - pad - bh;
            int bx = ClientSize.Width - pad - bw * 2 - gap;

            var lbl = new Label { Text = label, AutoSize = true, Location = new Point(pad, (int)(18 * s)) };
            _box = new TextBox
            {
                Text = value,
                Location = new Point(pad, (int)(48 * s)),
                Width = ClientSize.Width - 2 * pad,
                Font = new Font("Microsoft YaHei UI", 10.5f)
            };
            var ok = Ui.MakeButton("确定", DialogResult.OK, new Rectangle(bx, by, bw, bh));
            var cancel = Ui.MakeButton("取消", DialogResult.Cancel, new Rectangle(bx + bw + gap, by, bw, bh));
            Controls.AddRange(new Control[] { lbl, _box, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
            _box.SelectAll();
        }
    }

    internal class SettingsForm : Form
    {
        private readonly CheckBox _auto;

        public SettingsForm(bool autoStart)
        {
            float s = Ui.Scale();
            Text = "DeskFence 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size((int)(470 * s), (int)(250 * s));
            Font = new Font("Microsoft YaHei UI", 9f);

            int pad = (int)(18 * s);
            int bw = (int)(88 * s), bh = (int)(34 * s), gap = (int)(10 * s);
            int by = ClientSize.Height - pad - bh;
            int bx = ClientSize.Width - pad - bw * 2 - gap;

            _auto = new CheckBox
            {
                Text = "开机自动启动",
                Checked = autoStart,
                AutoSize = true,
                Location = new Point(pad, (int)(18 * s))
            };
            var info = new Label
            {
                AutoSize = false,
                Location = new Point(pad, (int)(52 * s)),
                Size = new Size(ClientSize.Width - 2 * pad, ClientSize.Height - (int)(52 * s) - pad - bh - (int)(12 * s)),
                Text = "数据目录：" + Store.RootDir + "\r\n\r\n" +
                       "每个收纳盒对应一个真实文件夹，内容随时可以在资源管理器中查看和修改。" +
                       "卸载本程序不会影响这些文件。"
            };
            var ok = Ui.MakeButton("确定", DialogResult.OK, new Rectangle(bx, by, bw, bh));
            var cancel = Ui.MakeButton("取消", DialogResult.Cancel, new Rectangle(bx + bw + gap, by, bw, bh));
            Controls.AddRange(new Control[] { _auto, info, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
        }

        public bool AutoStart { get { return _auto.Checked; } }
    }

    // =====================================================================
    //  Application
    // =====================================================================
    internal class FenceApp : ApplicationContext
    {
        private readonly List<FenceForm> _forms = new List<FenceForm>();
        private readonly NotifyIcon _tray;
        private readonly ToolStripMenuItem _autoItem;

        public FenceApp()
        {
            Store.Load();
            if (Store.Config.Boxes.Count == 0)
            {
                Store.Config.Boxes.Add(new BoxData
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                    Name = "收纳盒",
                    X = 80, Y = 80, W = 460, H = 360
                });
                Store.SaveNow();
            }
            foreach (var b in Store.Config.Boxes) ShowBox(b);

            var menu = new ContextMenuStrip();
            menu.Items.Add(Mi("新建收纳盒", delegate { NewBox(); }));
            menu.Items.Add(Mi("显示全部收纳盒", delegate { ShowAll(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Mi("打开数据目录", delegate
            {
                try { Process.Start("explorer.exe", "\"" + Store.RootDir + "\""); } catch { }
            }));
            _autoItem = Mi("开机自动启动", delegate
            {
                bool on = !Store.IsAutoStart();
                Store.SetAutoStart(on);
                Store.Config.AutoStart = on;
                Store.SaveSoon();
                UpdateAutoItem();
            });
            menu.Items.Add(_autoItem);
            menu.Items.Add(Mi("设置…", delegate { ShowSettings(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Mi("退出", delegate { ExitApp(); }));

            _tray = new NotifyIcon
            {
                Icon = MakeIcon(),
                Text = "DeskFence 收纳盒",
                Visible = true,
                ContextMenuStrip = menu
            };
            _tray.DoubleClick += delegate { ShowAll(); };
            UpdateAutoItem();
        }

        private ToolStripMenuItem Mi(string text, EventHandler h)
        {
            var it = new ToolStripMenuItem(text);
            if (h != null) it.Click += h;
            return it;
        }

        private void UpdateAutoItem()
        {
            _autoItem.Checked = Store.IsAutoStart();
        }

        private static Icon MakeIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(Color.FromArgb(255, 74, 132, 240)))
                {
                    g.FillRectangle(b, 3, 9, 26, 20);
                }
                using (var b = new SolidBrush(Color.FromArgb(255, 120, 170, 255)))
                {
                    g.FillRectangle(b, 3, 4, 26, 7);
                }
                IntPtr h = bmp.GetHicon();
                using (var tmp = Icon.FromHandle(h))
                {
                    Icon copy = (Icon)tmp.Clone();
                    Native.DestroyIcon(h);
                    return copy;
                }
            }
        }

        public void ShowBox(BoxData b)
        {
            var f = new FenceForm(this, b);
            f.FormClosed += delegate { _forms.Remove(f); };
            _forms.Add(f);
            f.Show();
            f.SaveState();
        }

        public void NewBox()
        {
            var b = new BoxData
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = "收纳盒 " + (Store.Config.Boxes.Count + 1),
                X = 120 + 30 * Store.Config.Boxes.Count,
                Y = 120 + 30 * Store.Config.Boxes.Count,
                W = 460, H = 360
            };
            Store.Config.Boxes.Add(b);
            Store.SaveNow();
            ShowBox(b);
        }

        public List<Rectangle> OtherBounds(FenceForm except)
        {
            var list = new List<Rectangle>();
            foreach (var f in _forms)
            {
                if (f == except || f.IsDisposed) continue;
                if (f.Data.Collapsed) list.Add(new Rectangle(f.Left, f.Top, f.Width, f.Height));
                else list.Add(f.Bounds);
            }
            return list;
        }

        public void SetTheme(string theme)
        {
            Store.Config.Theme = theme;
            Store.SaveNow();
            foreach (var f in _forms)
            {
                try { f.Invalidate(); } catch { }
            }
        }

        public void SetOpacity(int percent)
        {
            Store.Config.Opacity = percent;
            Store.SaveNow();
            foreach (var f in _forms)
            {
                try { f.Invalidate(); } catch { }
            }
        }

        public void SetIconSize(int px)
        {
            Store.Config.IconSize = px;
            Store.SaveNow();
            foreach (var f in _forms)
            {
                try
                {
                    f.ResetScroll();
                    f.Invalidate();
                }
                catch { }
            }
        }

        public void RemoveBox(FenceForm f)
        {
            try
            {
                foreach (var p in Directory.GetFileSystemEntries(f.Folder))
                {
                    string dest = Path.Combine(Store.DesktopDir, Path.GetFileName(p));
                    dest = Unique(dest);
                    if (Directory.Exists(p)) Directory.Move(p, dest);
                    else File.Move(p, dest);
                }
            }
            catch { }
            Store.Config.Boxes.Remove(f.Data);
            Store.SaveNow();
            f.Close();
        }

        private static string Unique(string dest)
        {
            if (!File.Exists(dest) && !Directory.Exists(dest)) return dest;
            string dir = Path.GetDirectoryName(dest);
            string name = Path.GetFileNameWithoutExtension(dest);
            string ext = Path.GetExtension(dest);
            for (int i = 2; i < 1000; i++)
            {
                string c = Path.Combine(dir, name + " (" + i + ")" + ext);
                if (!File.Exists(c) && !Directory.Exists(c)) return c;
            }
            return dest;
        }

        public void RenameBox(FenceForm f)
        {
            using (var dlg = new InputDialog("重命名收纳盒", "名称：", f.Data.Name))
            {
                if (dlg.ShowDialog() == DialogResult.OK && dlg.Value.Length > 0)
                {
                    f.Data.Name = dlg.Value;
                    f.Invalidate();
                    f.Text = dlg.Value;
                    Store.SaveSoon();
                }
            }
        }

        public void RenameItem(FenceForm f, string path)
        {
            string name = Path.GetFileName(path);
            using (var dlg = new InputDialog("重命名", "名称：", name))
            {
                if (dlg.ShowDialog() == DialogResult.OK && dlg.Value.Length > 0)
                {
                    try
                    {
                        string dest = Path.Combine(Path.GetDirectoryName(path), dlg.Value);
                        if (Directory.Exists(path)) Directory.Move(path, dest);
                        else File.Move(path, dest);
                        f.Reload();
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "DeskFence"); }
                }
            }
        }

        public void ShowSettings()
        {
            using (var dlg = new SettingsForm(Store.IsAutoStart()))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    Store.SetAutoStart(dlg.AutoStart);
                    Store.Config.AutoStart = dlg.AutoStart;
                    Store.SaveNow();
                    UpdateAutoItem();
                }
            }
        }

        public void SortAll()
        {
            foreach (var f in _forms)
            {
                f.Reload();
            }
            Store.SaveNow();
        }

        public void ShowAll()
        {
            foreach (var f in _forms) { f.Show(); f.BringToFront(); }
        }

        public void ExitApp()
        {
            foreach (var f in _forms.ToArray())
            {
                try { f.SaveState(); } catch { }
            }
            Store.SaveNow();
            _tray.Visible = false;
            _tray.Dispose();
            ExitThread();
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                bool created;
                using (var mutex = new Mutex(true, "DeskFence.SingleInstance.v1", out created))
                {
                    if (!created) return;
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new FenceApp());
                    GC.KeepAlive(mutex);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    string p = Path.Combine(Path.GetTempPath(), "DeskFence-crash.log");
                    File.AppendAllText(p, DateTime.Now + " " + ex.GetType().FullName + ": " + ex.Message + "\r\n" + ex.StackTrace + "\r\n");
                }
                catch { }
                throw;
            }
        }
    }
}
