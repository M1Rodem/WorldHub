using global::Google.Apis.Auth.OAuth2;
using global::Google.Apis.Auth.OAuth2.Flows;
using global::Google.Apis.Util.Store;
using WorldHub.Logging;

namespace WorldHub.Infrastructure.Google;

public enum GoogleAuthCheckResult
{
    /// <summary>Токен есть, обновление прошло, UserCredential получен.</summary>
    Authorized,

    /// <summary>Файла токена нет — нужна авторизация.</summary>
    NotAuthorized,

    /// <summary>credentials.json не найден.</summary>
    NoCredentials,

    /// <summary>
    /// Токен не удалось обновить: истёк, отозван, refresh-token невалиден.
    /// В будущем можно разделить на отдельные состояния.
    /// </summary>
    TokenExpired,

    /// <summary>Сеть недоступна, Google API не ответил.</summary>
    NetworkError,

    /// <summary>Не удалось классифицировать ошибку.</summary>
    UnknownError
}

public sealed class GoogleAuthService
{
    private const string ApplicationName = "WorldHub";
    private const string TokenDirectoryName = "google";

    private static readonly string[] Scopes =
    [
        "https://www.googleapis.com/auth/drive"
    ];

    private readonly string _credentialsPath;
    private readonly string _tokenDirectory;

    public GoogleAuthService(string dataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataPath);

        _credentialsPath = Path.Combine(
            AppContext.BaseDirectory,
            "Google",
            "credentials.json");

        _tokenDirectory = Path.Combine(
            dataPath,
            TokenDirectoryName);

        Directory.CreateDirectory(_tokenDirectory);
    }

    public bool HasCredentialsFile =>
        File.Exists(_credentialsPath);

    public async Task<GoogleAuthCheckResult> CheckAuthorizationAsync(
        CancellationToken cancellationToken = default)
    {
        if (!HasCredentialsFile)
        {
            return GoogleAuthCheckResult.NoCredentials;
        }

        if (!HasSavedToken())
        {
            return GoogleAuthCheckResult.NotAuthorized;
        }

        try
        {
            // AuthorizeAsync с живым refresh-token НЕ открывает браузер.
            // Браузер вызывается только если DataStore пуст — мы сюда
            // не доходим из-за HasSavedToken().
            await AuthorizeAsync(cancellationToken);

            return GoogleAuthCheckResult.Authorized;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (global::Google.Apis.Auth.OAuth2.Responses.TokenResponseException exception)
        {
            AppLog.Warning(
                $"[AUTH] Google token expired or invalid: {exception.Message}",
                exception);
            return GoogleAuthCheckResult.TokenExpired;
        }
        catch (HttpRequestException exception)
        {
            AppLog.Warning(
                $"[AUTH] Google auth network error: {exception.Message}",
                exception);
            return GoogleAuthCheckResult.NetworkError;
        }
        catch (TaskCanceledException)
        {
            return GoogleAuthCheckResult.NetworkError;
        }
        catch (Exception exception)
        {
            AppLog.Error(
                $"[AUTH] Unexpected Google auth check error: {exception.Message}",
                exception);
            return GoogleAuthCheckResult.UnknownError;
        }
    }

    public async Task<UserCredential> AuthorizeAsync(
        CancellationToken cancellationToken = default)
    {
        if (!HasCredentialsFile)
        {
            throw new FileNotFoundException(
                "Файл Google OAuth credentials.json не найден.",
                _credentialsPath);
        }

        await using var stream = new FileStream(
            _credentialsPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var clientSecrets =
            await GoogleClientSecrets.FromStreamAsync(
                stream,
                cancellationToken);

        var dataStore =
            new FileDataStore(_tokenDirectory);

        var flow =
            new GoogleAuthorizationCodeFlow(
                new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = clientSecrets.Secrets,
                    Scopes = Scopes,
                    DataStore = dataStore
                });

        var receiver =
            new LocalServerCodeReceiver();

        return await new AuthorizationCodeInstalledApp(
                flow,
                receiver)
            .AuthorizeAsync(
                "default",
                cancellationToken);
    }

    public bool HasSavedToken()
    {
        if (!Directory.Exists(_tokenDirectory))
        {
            return false;
        }

        return Directory.EnumerateFiles(_tokenDirectory).Any();
    }

    public async Task SignOutAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_tokenDirectory))
        {
            return;
        }

        await Task.Run(
            () =>
            {
                foreach (var file in Directory.EnumerateFiles(
                             _tokenDirectory))
                {
                    File.Delete(file);
                }
            },
            cancellationToken);
    }

    public string GetTokenDirectory() =>
        _tokenDirectory;
}