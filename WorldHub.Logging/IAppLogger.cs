namespace WorldHub.Logging;

/// <summary>
/// Общий интерфейс системы логирования WorldHub.
/// Предоставляет методы для вывода информационных сообщений, статусов успеха,
/// предупреждений, ошибок и разделителей.
/// </summary>
public interface IAppLogger
{
    /// <summary>
    /// Обычное информационное сообщение.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    void Log(string message);

    /// <summary>
    /// Успешное выполнение операции (выделяется успехом / зелёным цветом).
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    void Success(string message);

    /// <summary>
    /// Предупреждение (помечается вниманием / жёлтым цветом).
    /// </summary>
    /// <param name="message">Текст предупреждения.</param>
    /// <param name="exception">Необязательное исключение.</param>
    void Warning(string message, Exception? exception = null);

    /// <summary>
    /// Ошибка с поддержкой объекта исключения.
    /// </summary>
    /// <param name="message">Текст ошибки.</param>
    /// <param name="exception">Необязательное исключение с диагностической информацией.</param>
    void Error(string message, Exception? exception = null);

    /// <summary>
    /// Визуальный разделитель между логическими блоками.
    /// </summary>
    /// <param name="title">Необязательный заголовок секции.</param>
    void Separator(string title = "");
}
