namespace WorldHub.Logging;

/// <summary>
/// Статический фасад для логирования во всех проектах решения WorldHub.
/// Потокобезопасен, устойчив к ранней инициализации и предотвращает
/// рекурсивные сбои при возникновении внутренних ошибок логирования.
/// </summary>
public static class AppLog
{
    private static volatile IAppLogger _logger = NullLogger.Instance;
    private static readonly object _syncLock = new();

    [ThreadStatic]
    private static bool _isLogging;

    /// <summary>
    /// Текущий активный логгер. Если логгер не был установлен, возвращает <see cref="NullLogger.Instance"/>.
    /// </summary>
    public static IAppLogger Current => _logger;

    /// <summary>
    /// Устанавливает активный логгер.
    /// Передача <c>null</c> безопасно переключает логирование на <see cref="NullLogger.Instance"/>.
    /// </summary>
    /// <param name="logger">Экземпляр логгера или null.</param>
    public static void SetLogger(IAppLogger? logger)
    {
        lock (_syncLock)
        {
            _logger = logger ?? NullLogger.Instance;
        }
    }

    /// <summary>
    /// Обычное информационное сообщение.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    public static void Log(string message)
    {
        if (string.IsNullOrEmpty(message) || _isLogging)
        {
            return;
        }

        try
        {
            _isLogging = true;
            _logger.Log(message);
        }
        catch
        {
            // Ошибки логгера никогда не должны маскировать или прерывать работу приложения.
        }
        finally
        {
            _isLogging = false;
        }
    }

    /// <summary>
    /// Успешное выполнение операции (зелёный цвет / маркер успеха).
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    public static void Success(string message)
    {
        if (string.IsNullOrEmpty(message) || _isLogging)
        {
            return;
        }

        try
        {
            _isLogging = true;
            _logger.Success(message);
        }
        catch
        {
        }
        finally
        {
            _isLogging = false;
        }
    }

    /// <summary>
    /// Предупреждение (жёлтый цвет / маркер внимания).
    /// </summary>
    /// <param name="message">Текст предупреждения.</param>
    /// <param name="exception">Необязательное исключение.</param>
    public static void Warning(string message, Exception? exception = null)
    {
        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        if (_isLogging)
        {
            return;
        }

        try
        {
            _isLogging = true;
            _logger.Warning(message, exception);
        }
        catch
        {
        }
        finally
        {
            _isLogging = false;
        }
    }

    /// <summary>
    /// Ошибка (красный цвет / маркер ошибки).
    /// </summary>
    /// <param name="message">Текст ошибки.</param>
    /// <param name="exception">Необязательное исключение с деталями.</param>
    public static void Error(string message, Exception? exception = null)
    {
        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        if (_isLogging)
        {
            return;
        }

        try
        {
            _isLogging = true;
            _logger.Error(message, exception);
        }
        catch
        {
        }
        finally
        {
            _isLogging = false;
        }
    }

    /// <summary>
    /// Ошибка по объекту исключения.
    /// </summary>
    /// <param name="exception">Исключение.</param>
    /// <param name="message">Необязательное поясняющее сообщение (по умолчанию сообщение из exception).</param>
    public static void Error(Exception exception, string? message = null)
    {
        if (exception is null)
        {
            return;
        }

        var text = string.IsNullOrWhiteSpace(message)
            ? exception.Message
            : message;

        Error(text, exception);
    }

    /// <summary>
    /// Разделитель между логическими блоками.
    /// </summary>
    /// <param name="title">Необязательный заголовок разделителя.</param>
    public static void Separator(string title = "")
    {
        if (_isLogging)
        {
            return;
        }

        try
        {
            _isLogging = true;
            _logger.Separator(title);
        }
        catch
        {
        }
        finally
        {
            _isLogging = false;
        }
    }
}
