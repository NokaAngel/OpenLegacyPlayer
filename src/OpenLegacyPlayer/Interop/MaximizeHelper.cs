using System.Runtime.InteropServices;

namespace OpenLegacyPlayer.Interop;

/// <summary>
/// A borderless (<c>WindowStyle=None</c>) window maximizes to the full monitor
/// bounds by default, which hides the taskbar. Handling <c>WM_GETMINMAXINFO</c>
/// lets us clamp the maximized size and position to the monitor's *work area*
/// so the taskbar stays visible, just like a normal window.
/// </summary>
public static class MaximizeHelper
{
    public const int WM_GETMINMAXINFO = 0x0024;

    public static void HandleGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                RECT work = info.rcWork;
                RECT full = info.rcMonitor;

                // Position/size relative to the monitor (work area excludes the taskbar).
                mmi.ptMaxPosition.X = work.Left - full.Left;
                mmi.ptMaxPosition.Y = work.Top - full.Top;
                mmi.ptMaxSize.X = work.Right - work.Left;
                mmi.ptMaxSize.Y = work.Bottom - work.Top;

                // Keep the window usable when maximized on smaller displays.
                mmi.ptMinTrackSize.X = 820;
                mmi.ptMinTrackSize.Y = 520;
            }
        }

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }
}
