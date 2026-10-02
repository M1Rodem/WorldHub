namespace WorldHub.Core.Diagnostics;

public static class DebugConsole
{
    public static void Log(string message)
        => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");

    public static void Error(string message)
        => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] {message}");
}