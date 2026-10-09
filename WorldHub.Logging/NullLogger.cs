namespace WorldHub.Logging;

/// <summary>
/// Безопасная no-op реализация <see cref="IAppLogger"/>, которая игнорирует любые сообщения.
/// Используется по умолчанию до установки активного логгера, исключая <see cref="NullReferenceException"/>
/// при ранних вызовах из статических конструкторов или фоновых потоков.
/// </summary>
public sealed class NullLogger : IAppLogger
{
    /// <summary>
    /// Единственный экземпляр NullLogger.
    /// </summary>
    public static readonly NullLogger Instance = new();

    private NullLogger()
    {
    }

    /// <inheritdoc />
    public void Log(string message)
    {
    }

    /// <inheritdoc />
    public void Success(string message)
    {
    }

    /// <inheritdoc />
    public void Warning(string message, Exception? exception = null)
    {
    }

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null)
    {
    }

    /// <inheritdoc />
    public void Separator(string title = "")
    {
    }
}
