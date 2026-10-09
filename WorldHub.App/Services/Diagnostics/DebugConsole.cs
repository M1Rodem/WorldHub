using System.IO;
using System.Runtime.InteropServices;

namespace WorldHub.App.Services.Diagnostics;

public static class DebugConsole
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_CLOSE = 0x0010;

    public static void Initialize()
    {
        if (!AllocConsole())
        {
            return;
        }

        // Перенаправляем потоки Console на выделенную консоль
        var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
        Console.SetOut(stdout);
        Console.SetError(stderr);

        Console.Title = "WorldHub Debug Console";

        Console.WriteLine("========================================");
        Console.WriteLine("          WORLDHUB DEBUG CONSOLE");
        Console.WriteLine("========================================");
        Console.WriteLine();

        Console.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Process ID: {Environment.ProcessId}");
        Console.WriteLine($"OS: {Environment.OSVersion}");
        Console.WriteLine($".NET: {Environment.Version}");
        Console.WriteLine();
    }

    public static void Log(string message)
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    public static void Error(string message)
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] [ERROR] {message}");
    }

    public static void Close()
    {
        try
        {
            var hWnd = GetConsoleWindow();
            if (hWnd != IntPtr.Zero)
            {
                PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }
        catch
        {
        }

        try
        {
            FreeConsole();
        }
        catch
        {
        }
    }
}