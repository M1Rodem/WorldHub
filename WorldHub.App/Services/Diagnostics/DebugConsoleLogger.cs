using System.IO;
using WorldHub.Logging;

namespace WorldHub.App.Services.Diagnostics;

/// <summary>
/// Реализация <see cref="IAppLogger"/>, выводящая сообщения в отладочную консоль Windows.
/// Потокобезопасна и форматирует вывод с цветовой индикацией уровней логирования.
/// </summary>
public sealed class DebugConsoleLogger : IAppLogger
{
    private static readonly object ConsoleLock = new();

    /// <inheritdoc />
    public void Log(string message)
    {
        lock (ConsoleLock)
        {
            Console.WriteLine($"[{Time()}] {message}");
        }
    }

    /// <inheritdoc />
    public void Success(string message)
    {
        lock (ConsoleLock)
        {
            var previous = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{Time()}] ✓ {message}");
            }
            finally
            {
                Console.ForegroundColor = previous;
            }
        }
    }

    /// <inheritdoc />
    public void Warning(string message, Exception? exception = null)
    {
        lock (ConsoleLock)
        {
            var previous = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[{Time()}] ! {message}");

                if (exception is not null)
                {
                    Console.WriteLine($"[{Time()}]   Детали: {exception.GetType().Name}: {exception.Message}");
                }
            }
            finally
            {
                Console.ForegroundColor = previous;
            }
        }
    }

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null)
    {
        lock (ConsoleLock)
        {
            var previous = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{Time()}] ✗ {message}");

                if (exception is not null)
                {
                    Console.WriteLine($"[{Time()}]   Исключение: {exception.GetType().FullName}: {exception.Message}");
                    if (!string.IsNullOrWhiteSpace(exception.StackTrace))
                    {
                        Console.WriteLine($"[{Time()}]   Стек вызовов:\n{exception.StackTrace}");
                    }
                }
            }
            finally
            {
                Console.ForegroundColor = previous;
            }
        }
    }

    /// <inheritdoc />
    public void Separator(string title = "")
    {
        lock (ConsoleLock)
        {
            Console.WriteLine($"[{Time()}] ───────── {title} ─────────");
        }
    }

    private static string Time() =>
        DateTime.Now.ToString("HH:mm:ss");
}
