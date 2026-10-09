using global::Google.Apis.Drive.v3;
using global::Google.Apis.Services;
using Google;
using WorldHub.Core.Enums;
using WorldHub.Logging;

namespace WorldHub.Infrastructure.Google;

public enum GoogleDriveCheckResult
{
    /// <summary>Проверка ещё не завершилась, используется кэшем.</summary>
    CheckPending,

    /// <summary>About.Get прошёл, Drive API доступен.</summary>
    DriveAvailable,

    /// <summary>Токен истёк или отозван.</summary>
    TokenExpired,

    /// <summary>Авторизация не сохранена.</summary>
    NotAuthorized,

    /// <summary>credentials.json не найден.</summary>
    NoCredentials,

    /// <summary>403 от Google API — не хватает прав/scope.</summary>
    InsufficientPermissions,

    /// <summary>Сеть недоступна, Google API не ответил.</summary>
    NetworkError,

    /// <summary>Проверка не завершилась за отведённое время.</summary>
    Timeout,

    /// <summary>Не удалось классифицировать ошибку.</summary>
    UnknownError
}

public sealed record FolderShareResult(
    bool Success,
    string? PermissionId,
    string? Message);

public sealed class GoogleDriveClient
{
    private const string ApplicationName = "WorldHub";

    private static readonly TimeSpan DefaultTimeout =
        TimeSpan.FromSeconds(10);

    private readonly GoogleAuthService _authService;

    public GoogleAuthService AuthService => _authService;

    public GoogleDriveClient(GoogleAuthService authService)
    {
        ArgumentNullException.ThrowIfNull(authService);

        _authService = authService;
    }

    public async Task<GoogleDriveCheckResult> CheckDriveAccessAsync(
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutCts.CancelAfter(DefaultTimeout);

        var token = timeoutCts.Token;

        try
        {
            var authResult =
                await _authService.CheckAuthorizationAsync(token);

            switch (authResult)
            {
                case GoogleAuthCheckResult.Authorized:
                    break;

                case GoogleAuthCheckResult.NotAuthorized:
                    return GoogleDriveCheckResult.NotAuthorized;

                case GoogleAuthCheckResult.NoCredentials:
                    return GoogleDriveCheckResult.NoCredentials;

                case GoogleAuthCheckResult.TokenExpired:
                    return GoogleDriveCheckResult.TokenExpired;

                case GoogleAuthCheckResult.NetworkError:
                    return GoogleDriveCheckResult.NetworkError;

                default:
                    return GoogleDriveCheckResult.UnknownError;
            }

            var credential =
                await _authService.AuthorizeAsync(token);

            var service =
                new DriveService(
                    new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = ApplicationName
                    });

            var aboutRequest = service.About.Get();

            aboutRequest.Fields =
                "user(emailAddress,displayName,photoLink)";

            await aboutRequest.ExecuteAsync(token);

            return GoogleDriveCheckResult.DriveAvailable;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return GoogleDriveCheckResult.Timeout;
        }
        catch (global::Google.Apis.Auth.OAuth2.Responses.TokenResponseException)
        {
            return GoogleDriveCheckResult.TokenExpired;
        }
        catch (GoogleApiException exception)
            when ((int)exception.HttpStatusCode == 403)
        {
            return GoogleDriveCheckResult.InsufficientPermissions;
        }
        catch (GoogleApiException exception)
            when ((int)exception.HttpStatusCode == 401)
        {
            return GoogleDriveCheckResult.TokenExpired;
        }
        catch (HttpRequestException)
        {
            return GoogleDriveCheckResult.NetworkError;
        }
        catch
        {
            return GoogleDriveCheckResult.UnknownError;
        }
    }

    public async Task<string> GetAccountEmailAsync(
        CancellationToken cancellationToken = default)
    {
        var credential =
            await _authService.AuthorizeAsync(
                cancellationToken);

        var service =
            new DriveService(
                new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName
                });

        var aboutRequest = service.About.Get();

        aboutRequest.Fields =
            "user(emailAddress,displayName,photoLink)";

        var about =
            await aboutRequest.ExecuteAsync(
                cancellationToken);

        return about.User?.EmailAddress
            ?? throw new InvalidOperationException(
                "Google не вернул адрес аккаунта.");
    }

    /// <summary>
    /// Создаёт папку в Google Drive текущего аккаунта.
    /// Возвращает ID созданной папки или null при ошибке.
    /// Не вызывать повторно без явного действия пользователя —
    /// каждый вызов создаёт НОВУЮ папку.
    /// </summary>
    public async Task<string?> CreateFolderAsync(
        string folderName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutCts.CancelAfter(DefaultTimeout);

        var token = timeoutCts.Token;

        var credential =
            await _authService.AuthorizeAsync(token);

        var service =
            new DriveService(
                new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName
                });

        var metadata = new global::Google.Apis.Drive.v3.Data.File
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder"
        };

        var request = service.Files.Create(metadata);
        request.Fields = "id,name";

        var folder = await request.ExecuteAsync(token);

        AppLog.Log(
            $"[DRIVE] Folder created: {folder.Name} ({folder.Id})");

        return folder.Id;
    }

    /// <summary>
    /// Выдаёт разрешение на папку указанному email.
    /// По умолчанию Google-уведомление не отправляется — доступ выдаётся
    /// сразу, письмо не нужно. Участник увидит папку в разделе
    /// «Доступные мне» без дополнительных действий.
    /// </summary>
    public async Task<FolderShareResult> ShareFolderAsync(
        string folderId,
        string email,
        string role = "writer",
        bool sendNotificationEmail = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (role is not ("reader" or "commenter" or "writer"))
        {
            throw new ArgumentException(
                $"Unsupported role '{role}'. Expected reader, commenter or writer.",
                nameof(role));
        }

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutCts.CancelAfter(DefaultTimeout);

        var token = timeoutCts.Token;

        try
        {
            var credential =
                await _authService.AuthorizeAsync(token);

            var service =
                new DriveService(
                    new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = ApplicationName
                    });

            var permission = new global::Google.Apis.Drive.v3.Data.Permission
            {
                Type = "user",
                Role = role,
                EmailAddress = email
            };

            var request = service.Permissions.Create(
                permission,
                folderId);

            request.Fields = "id,emailAddress,role";
            request.SendNotificationEmail = sendNotificationEmail;

            var result = await request.ExecuteAsync(token);

            AppLog.Success(
                $"[DRIVE] Share {folderId} → {result.EmailAddress} ({result.Role})");

            return new FolderShareResult(
                Success: true,
                PermissionId: result.Id,
                Message: $"Доступ выдан: {result.EmailAddress} ({result.Role})");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new FolderShareResult(
                Success: false,
                PermissionId: null,
                Message: "Превышено время операции.");
        }
        catch (GoogleApiException exception)
        {
            AppLog.Error(
                $"[DRIVE] Share {folderId} → {email} failed: " +
                $"{exception.HttpStatusCode} {exception.Message}",
                exception);

            return new FolderShareResult(
                Success: false,
                PermissionId: null,
                Message: $"GoogleApiException {(int)exception.HttpStatusCode}: {exception.Message}");
        }
        catch (Exception exception)
        {
            return new FolderShareResult(
                Success: false,
                PermissionId: null,
                Message: $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    /// <summary>
    /// Проверяет доступ текущего Google-аккаунта к указанной папке.
    /// Не различает «папки нет» и «папка чужая» — Google отдаёт 404
    /// в обоих случаях, чтобы не подтверждать существование чужих файлов.
    /// </summary>
    public async Task<FolderAccessStatus> CheckFolderAccessAsync(
        string folderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutCts.CancelAfter(DefaultTimeout);

        var token = timeoutCts.Token;

        try
        {
            var credential =
                await _authService.AuthorizeAsync(token);

            var service =
                new DriveService(
                    new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = ApplicationName
                    });

            var request = service.Files.Get(folderId);
            request.Fields = "id,name,mimeType";
            request.SupportsAllDrives = true;

            var folder = await request.ExecuteAsync(token);

            if (!string.Equals(
                    folder.MimeType,
                    "application/vnd.google-apps.folder",
                    StringComparison.Ordinal))
            {
                AppLog.Warning(
                    $"[DRIVE] {folderId} is not a folder: {folder.MimeType}");
                return FolderAccessStatus.NoAccess;
            }

            AppLog.Success(
                $"[DRIVE] Folder access OK: {folder.Name} ({folderId})");

            return FolderAccessStatus.Accessible;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return FolderAccessStatus.Timeout;
        }
        catch (global::Google.Apis.Auth.OAuth2.Responses.TokenResponseException)
        {
            return FolderAccessStatus.NotAuthorized;
        }
        catch (GoogleApiException exception)
            when ((int)exception.HttpStatusCode == 401)
        {
            return FolderAccessStatus.NotAuthorized;
        }
        catch (GoogleApiException exception)
            when ((int)exception.HttpStatusCode == 404)
        {
            AppLog.Error(
                $"[DRIVE] Folder {folderId} not found (404).",
                exception);
            return FolderAccessStatus.FolderNotFound;
        }
        catch (GoogleApiException exception)
            when ((int)exception.HttpStatusCode == 403)
        {
            // Различаем «нет прав» и «недостаточно scope» по reason.
            var reasons = exception.Error?.Errors?
                .Select(e => e.Reason)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList();

            if (reasons is not null &&
                reasons.Any(r =>
                    r.Contains("insufficientPermissions", StringComparison.OrdinalIgnoreCase) ||
                    r.Contains("insufficientFilePermissions", StringComparison.OrdinalIgnoreCase) ||
                    r.Contains("accessNotConfigured", StringComparison.OrdinalIgnoreCase)))
            {
                return FolderAccessStatus.InsufficientScope;
            }

            return FolderAccessStatus.NoAccess;
        }
        catch (HttpRequestException)
        {
            return FolderAccessStatus.NetworkError;
        }
        catch
        {
            return FolderAccessStatus.UnknownError;
        }
    }
}