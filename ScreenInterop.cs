using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ModelContextProtocol;

namespace Laz.Mcp;

/// <summary>
/// Screen-size and monitor-enumeration support for <see cref="ScreenTools"/>. Laz itself exposes
/// no such API (only pixel capture/read), so this goes directly to the OS: Win32 on Windows,
/// CoreGraphics on macOS, and an `xrandr` shell-out on Linux (X11/XWayland only).
/// </summary>
internal static class ScreenInterop
{
    public static ScreenSize GetVirtualScreenBounds()
    {
        if (OperatingSystem.IsWindows())
        {
            return GetVirtualScreenBoundsWindows();
        }

        if (OperatingSystem.IsMacOS())
        {
            return GetVirtualScreenBoundsMac();
        }

        if (OperatingSystem.IsLinux())
        {
            return GetVirtualScreenBoundsLinux();
        }

        throw new PlatformNotSupportedException("screen_get_size is not supported on this platform.");
    }

    public static IReadOnlyList<DisplayInfo> ListDisplays()
    {
        if (OperatingSystem.IsWindows())
        {
            return ListDisplaysWindows();
        }

        if (OperatingSystem.IsMacOS())
        {
            return ListDisplaysMac();
        }

        if (OperatingSystem.IsLinux())
        {
            return ListDisplaysLinux();
        }

        throw new PlatformNotSupportedException("screen_list_displays is not supported on this platform.");
    }

    #region Windows

    private const int SmXvirtualscreen = 76;
    private const int SmYvirtualscreen = 77;
    private const int SmCxvirtualscreen = 78;
    private const int SmCyvirtualscreen = 79;
    private const uint MonitorinfofPrimary = 0x1;
    private const int MdtEffectiveDpi = 0;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    // Shcore.dll, not user32.dll — GetDpiForMonitor was introduced in Windows 8.1's Shcore API.
    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int CbSize;
        public Rect RcMonitor;
        public Rect RcWork;
        public uint DwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string SzDevice;
    }

    private static ScreenSize GetVirtualScreenBoundsWindows()
    {
        var x = GetSystemMetrics(SmXvirtualscreen);
        var y = GetSystemMetrics(SmYvirtualscreen);
        var width = GetSystemMetrics(SmCxvirtualscreen);
        var height = GetSystemMetrics(SmCyvirtualscreen);
        return new ScreenSize(x, y, width, height);
    }

    private static IReadOnlyList<DisplayInfo> ListDisplaysWindows()
    {
        var handles = new List<IntPtr>();

        bool Callback(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData)
        {
            handles.Add(hMonitor);
            return true;
        }

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);

        var displays = new List<DisplayInfo>(handles.Count);
        for (var i = 0; i < handles.Count; i++)
        {
            var info = new MonitorInfoEx { CbSize = Marshal.SizeOf<MonitorInfoEx>() };
            if (!GetMonitorInfo(handles[i], ref info))
            {
                continue;
            }

            var scale = 1.0;
            if (GetDpiForMonitor(handles[i], MdtEffectiveDpi, out var dpiX, out _) == 0)
            {
                scale = dpiX / 96.0;
            }

            displays.Add(new DisplayInfo(
                i,
                info.RcMonitor.Left,
                info.RcMonitor.Top,
                info.RcMonitor.Right - info.RcMonitor.Left,
                info.RcMonitor.Bottom - info.RcMonitor.Top,
                (info.DwFlags & MonitorinfofPrimary) != 0,
                scale));
        }

        return displays;
    }

    #endregion

    #region macOS

    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    [DllImport(CoreGraphicsLib)]
    private static extern int CGGetActiveDisplayList(uint maxDisplays, uint[]? activeDisplays, out uint displayCount);

    [DllImport(CoreGraphicsLib)]
    private static extern uint CGMainDisplayID();

    [DllImport(CoreGraphicsLib)]
    private static extern CgRect CGDisplayBounds(uint display);

    [DllImport(CoreGraphicsLib)]
    private static extern IntPtr CGDisplayCopyDisplayMode(uint display);

    [DllImport(CoreGraphicsLib)]
    private static extern void CGDisplayModeRelease(IntPtr mode);

    [DllImport(CoreGraphicsLib)]
    private static extern nuint CGDisplayModeGetWidth(IntPtr mode);

    [DllImport(CoreGraphicsLib)]
    private static extern nuint CGDisplayModeGetPixelWidth(IntPtr mode);

    [StructLayout(LayoutKind.Sequential)]
    private struct CgRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }

    private static uint[] GetActiveDisplayIds()
    {
        CGGetActiveDisplayList(0, null, out var count);
        if (count == 0)
        {
            return [];
        }

        var ids = new uint[count];
        CGGetActiveDisplayList(count, ids, out count);
        return ids;
    }

    private static double GetScaleFactorMac(uint displayId)
    {
        var mode = CGDisplayCopyDisplayMode(displayId);
        if (mode == IntPtr.Zero)
        {
            return 1.0;
        }

        try
        {
            var pointWidth = (double)CGDisplayModeGetWidth(mode);
            var pixelWidth = (double)CGDisplayModeGetPixelWidth(mode);
            return pointWidth > 0 ? pixelWidth / pointWidth : 1.0;
        }
        finally
        {
            CGDisplayModeRelease(mode);
        }
    }

    private static ScreenSize GetVirtualScreenBoundsMac()
    {
        var ids = GetActiveDisplayIds();
        if (ids.Length == 0)
        {
            return new ScreenSize(0, 0, 0, 0);
        }

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var id in ids)
        {
            var bounds = CGDisplayBounds(id);
            minX = Math.Min(minX, bounds.X);
            minY = Math.Min(minY, bounds.Y);
            maxX = Math.Max(maxX, bounds.X + bounds.Width);
            maxY = Math.Max(maxY, bounds.Y + bounds.Height);
        }

        return new ScreenSize((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
    }

    private static IReadOnlyList<DisplayInfo> ListDisplaysMac()
    {
        var ids = GetActiveDisplayIds();
        var mainId = CGMainDisplayID();

        var displays = new List<DisplayInfo>(ids.Length);
        for (var i = 0; i < ids.Length; i++)
        {
            var bounds = CGDisplayBounds(ids[i]);
            displays.Add(new DisplayInfo(
                i,
                (int)bounds.X,
                (int)bounds.Y,
                (int)bounds.Width,
                (int)bounds.Height,
                ids[i] == mainId,
                GetScaleFactorMac(ids[i])));
        }

        return displays;
    }

    #endregion

    #region Linux

    // e.g. "Screen 0: minimum 320 x 200, current 3840 x 1080, maximum 16384 x 16384"
    private static readonly Regex ScreenSizeRegex = new(@"current\s+(\d+)\s*x\s*(\d+)", RegexOptions.Compiled);

    // e.g. "HDMI-1 connected primary 1920x1080+0+0 (normal left inverted...) 527mm x 296mm"
    private static readonly Regex DisplayRegex = new(
        @"^(?<name>\S+)\s+connected\s+(?<primary>primary\s+)?(?<width>\d+)x(?<height>\d+)\+(?<x>-?\d+)\+(?<y>-?\d+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static string RunXrandr()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "xrandr",
                    Arguments = "--query",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new McpException(
                    "xrandr exited with an error. screen_get_size/screen_list_displays require an "
                    + "active X11/XWayland session with xrandr installed.");
            }

            return output;
        }
        catch (Exception ex) when (ex is not McpException)
        {
            throw new McpException(
                "Could not run 'xrandr'. screen_get_size/screen_list_displays require an "
                + "X11/XWayland session with xrandr installed (Wayland-native sessions aren't supported).",
                ex);
        }
    }

    private static ScreenSize GetVirtualScreenBoundsLinux()
    {
        var output = RunXrandr();
        var match = ScreenSizeRegex.Match(output);
        if (!match.Success)
        {
            throw new McpException("Could not parse xrandr output for the virtual screen size.");
        }

        var width = int.Parse(match.Groups[1].Value);
        var height = int.Parse(match.Groups[2].Value);
        return new ScreenSize(0, 0, width, height);
    }

    private static IReadOnlyList<DisplayInfo> ListDisplaysLinux()
    {
        var output = RunXrandr();
        var displays = new List<DisplayInfo>();
        var index = 0;
        foreach (Match match in DisplayRegex.Matches(output))
        {
            displays.Add(new DisplayInfo(
                index++,
                int.Parse(match.Groups["x"].Value),
                int.Parse(match.Groups["y"].Value),
                int.Parse(match.Groups["width"].Value),
                int.Parse(match.Groups["height"].Value),
                match.Groups["primary"].Success,
                1.0));
        }

        return displays;
    }

    #endregion
}
