using System.Runtime.InteropServices;

namespace WebToMarkdown;

internal static class NativeMethods
{
    private const int SwHide = 0;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static void HideConsoleWindow()
    {
        try
        {
            var handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
                ShowWindow(handle, SwHide);
        }
        catch
        {
            // The executable is built as WinExe, so there normally is no console window anyway.
        }
    }
}
