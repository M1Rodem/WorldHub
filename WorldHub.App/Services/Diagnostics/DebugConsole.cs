using System.IO;
using System.Runtime.InteropServices;

namespace WorldHub.App.Services.Diagnostics;

public static class DebugConsole
{
    private static IntPtr _consoleHwnd = IntPtr.Zero;
    private static StreamWriter? _stdout;
    private static StreamWriter? _stderr;

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const uint WM_CLOSE = 0x0010;
    private const int SW_HIDE = 0;

    public static void Initialize()
    {
        if (!AllocConsole())
        {
            return;
        }

        _consoleHwnd = GetConsoleWindow();

        _stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        _stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
        Console.SetOut(_stdout);
        Console.SetError(_stderr);

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

    /// <summary>Обычное информационное сообщение (делегируется в AppLog).</summary>
    public static void Log(string message) =>
        WorldHub.Logging.AppLog.Log(message);

    /// <summary>Успех — помечается ✓, зелёным (делегируется в AppLog).</summary>
    public static void Success(string message) =>
        WorldHub.Logging.AppLog.Success(message);

    /// <summary>Предупреждение — помечается !, жёлтым (делегируется в AppLog).</summary>
    public static void Warning(string message) =>
        WorldHub.Logging.AppLog.Warning(message);

    /// <summary>Ошибка — помечается ✗, красным (делегируется в AppLog).</summary>
    public static void Error(string message) =>
        WorldHub.Logging.AppLog.Error(message);

    /// <summary>Разделитель между логическими блоками (делегируется в AppLog).</summary>
    public static void Separator(string title = "") =>
        WorldHub.Logging.AppLog.Separator(title);

    public static void Close()
    {
        var hWnd = _consoleHwnd != IntPtr.Zero ? _consoleHwnd : GetConsoleWindow();

        if (hWnd != IntPtr.Zero)
        {
            try
            {
                // Немедленно скрываем окно с экрана
                ShowWindow(hWnd, SW_HIDE);
            }
            catch
            {
            }

            try
            {
                SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
            }

            try
            {
                PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
            }
        }

        try
        {
            _stdout?.Flush();
            _stderr?.Flush();
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            _stdout?.Dispose();
            _stderr?.Dispose();
            _stdout = null;
            _stderr = null;
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

        _consoleHwnd = IntPtr.Zero;
    }

    private static string Time() =>
        DateTime.Now.ToString("HH:mm:ss");
}