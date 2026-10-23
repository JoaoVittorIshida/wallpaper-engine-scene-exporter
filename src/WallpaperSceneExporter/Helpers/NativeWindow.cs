using System.Runtime.InteropServices;
using System.Text;

namespace WallpaperSceneExporter.Helpers;

public static class NativeWindow
{
    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hwnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint hwnd, out Rect rect);

    public static nint FindTopLevelWindow(string exactTitle)
    {
        nint found = 0;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            var title = new StringBuilder(512);
            GetWindowText(hwnd, title, title.Capacity);
            if (!string.Equals(title.ToString(), exactTitle, StringComparison.Ordinal)) return true;
            found = hwnd;
            return false;
        }, 0);
        return found;
    }

    public static bool Exists(nint hwnd) => hwnd != 0 && IsWindow(hwnd);

    public static (int Width, int Height) GetClientSize(nint hwnd)
    {
        if (!GetClientRect(hwnd, out Rect rect))
            throw new InvalidOperationException("Não foi possível obter a dimensão da janela de renderização.");
        return (rect.Right - rect.Left, rect.Bottom - rect.Top);
    }
}
