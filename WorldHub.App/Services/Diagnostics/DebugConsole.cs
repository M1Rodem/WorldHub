using System.Runtime.InteropServices;

namespace WorldHub.App.Services.Diagnostics;

public static class DebugConsole
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    public static void Initialize()
    {
        if (!AllocConsole())
        {
            return;
        }

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
        FreeConsole();
    }
}