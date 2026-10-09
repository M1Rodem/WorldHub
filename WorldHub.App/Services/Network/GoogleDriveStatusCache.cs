using WorldHub.Infrastructure.Google;

namespace WorldHub.App.Services.Network;

/// <summary>
/// Хранит последний известный результат проверки Google Drive.
/// Заполняется быстро при старте (PreInitialize) и уточняется
/// асинхронно (Update). Читается синхронно при формировании PeerInfo.
/// </summary>
public sealed class GoogleDriveStatusCache
{
    private readonly object _lock = new();

    private GoogleDriveCheckResult _lastResult =
        GoogleDriveCheckResult.CheckPending;

    private DateTimeOffset? _lastCheckedAtUtc;

    /// <summary>
    /// Срабатывает, когда кэш впервые получает любое значение
    /// (через PreInitialize или первый Update).
    /// </summary>
    public event EventHandler? Initialized;

    /// <summary>
    /// Срабатывает при каждом обновлении значения — и при первом,
    /// и при повторных. Используется, чтобы UI обновлял статус
    /// участников после каждой проверки.
    /// </summary>
    public event EventHandler? Updated;

    public bool IsInitialized
    {
        get
        {
            lock (_lock)
            {
                return _lastCheckedAtUtc is not null;
            }
        }
    }

    public DateTimeOffset? LastCheckedAtUtc
    {
        get
        {
            lock (_lock)
            {
                return _lastCheckedAtUtc;
            }
        }
    }

    /// <summary>
    /// Мгновенно заполняет кэш на основе быстрой проверки
    /// наличия файла токена. Вызывается один раз при старте
    /// до запуска сетевого listener и до первого PeerInfo.
    /// Если кэш уже был заполнен реальной проверкой — ничего не делает.
    /// </summary>
    public void PreInitialize(bool hasSavedToken)
    {
        bool wasUninitialized;

        lock (_lock)
        {
            if (_lastCheckedAtUtc is not null)
            {
                return;
            }

            wasUninitialized = true;

            _lastResult = hasSavedToken
                ? GoogleDriveCheckResult.CheckPending
                : GoogleDriveCheckResult.NotAuthorized;

            _lastCheckedAtUtc = DateTimeOffset.UtcNow;
        }

        if (wasUninitialized)
        {
            Initialized?.Invoke(this, EventArgs.Empty);
            Updated?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Update(GoogleDriveCheckResult result)
    {
        bool wasUninitialized;

        lock (_lock)
        {
            wasUninitialized = _lastCheckedAtUtc is null;

            _lastResult = result;
            _lastCheckedAtUtc = DateTimeOffset.UtcNow;
        }

        if (wasUninitialized)
        {
            Initialized?.Invoke(this, EventArgs.Empty);
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    public string GetStatusString()
    {
        lock (_lock)
        {
            if (_lastCheckedAtUtc is null)
            {
                return "Unknown";
            }

            return _lastResult switch
            {
                GoogleDriveCheckResult.CheckPending =>
                    "CheckPending",

                GoogleDriveCheckResult.DriveAvailable =>
                    "DriveAvailable",

                GoogleDriveCheckResult.TokenExpired =>
                    "TokenExpired",

                GoogleDriveCheckResult.NotAuthorized =>
                    "NotAuthorized",

                GoogleDriveCheckResult.NoCredentials =>
                    "NoCredentials",

                GoogleDriveCheckResult.InsufficientPermissions =>
                    "InsufficientPermissions",

                GoogleDriveCheckResult.NetworkError =>
                    "NetworkError",

                GoogleDriveCheckResult.Timeout =>
                    "Timeout",

                _ => "Unknown"
            };
        }
    }
}