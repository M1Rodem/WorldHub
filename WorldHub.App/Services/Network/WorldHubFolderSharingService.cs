using WorldHub.Core.Entities;
using WorldHub.Infrastructure.Google;
using WorldHub.Logging;
using WorldHub.Sync.Services;

namespace WorldHub.App.Services.Network;

/// <summary>
/// Автоматически выдаёт доступ к общей папке Google Drive
/// всем участникам WorldHub-сервера, у которых известен Google-email.
/// </summary>
public sealed class WorldHubFolderSharingService
{
    private readonly GoogleDriveClient _googleDriveClient;
    private readonly WorldHubServerService _worldHubServerService;

    public WorldHubFolderSharingService(
        GoogleDriveClient googleDriveClient,
        WorldHubServerService worldHubServerService)
    {
        ArgumentNullException.ThrowIfNull(googleDriveClient);
        ArgumentNullException.ThrowIfNull(worldHubServerService);

        _googleDriveClient = googleDriveClient;
        _worldHubServerService = worldHubServerService;
    }

    /// <summary>
    /// Проходит по всем участникам сервера, у которых известен Google-email
    /// (то есть они уже были успешно проверены), и выдаёт им доступ к папке.
    /// Повторные вызовы безопасны: если доступ уже выдан,
    /// Google вернёт ошибку alreadyExists — она игнорируется.
    /// </summary>
    public async Task ShareWithAllParticipantsAsync(
        WorldHubServer server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (string.IsNullOrWhiteSpace(server.GoogleDriveFolderId))
        {
            return;
        }

        var withEmail = server.Participants
            .Where(p => !string.IsNullOrWhiteSpace(p.GoogleEmail))
            .ToList();

        AppLog.Log(
            $"[SHARE] Folder {server.GoogleDriveFolderId}: " +
            $"{withEmail.Count} участников с GoogleEmail из {server.Participants.Count}.");

        foreach (var participant in server.Participants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(participant.GoogleEmail))
            {
                AppLog.Log(
                    $"[SHARE] Skip {participant.IpAddress}: GoogleEmail unknown.");
                continue;
            }

            try
            {
                AppLog.Log(
                    $"[SHARE] Sharing with {participant.GoogleEmail} " +
                    $"({participant.IpAddress}) ...");

                var result = await _googleDriveClient.ShareFolderAsync(
                    server.GoogleDriveFolderId,
                    participant.GoogleEmail!,
                    role: "writer",
                    sendNotificationEmail: false,
                    cancellationToken);

                if (result.Success)
                {
                    AppLog.Success(
                        $"[SHARE] Access granted to {participant.GoogleEmail}.");
                }
                else if (IsAlreadyShared(result.Message))
                {
                    AppLog.Log(
                        $"[SHARE] Already shared with {participant.GoogleEmail}.");
                }
                else
                {
                    AppLog.Error(
                        $"[SHARE] {participant.GoogleEmail}: {result.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                AppLog.Error(
                    $"[SHARE] {participant.GoogleEmail}: {exception.Message}");
            }
        }
    }

    private static bool IsAlreadyShared(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains(
            "alreadyExists",
            StringComparison.OrdinalIgnoreCase);
    }
}