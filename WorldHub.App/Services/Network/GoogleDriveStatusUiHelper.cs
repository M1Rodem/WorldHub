using System.Windows.Media;
using WorldHub.Infrastructure.Google;

namespace WorldHub.App.Services.Network;

public static class GoogleDriveStatusUiHelper
{
    private static readonly SolidColorBrush SuccessBrush =
        new(Color.FromRgb(0x42, 0xC7, 0x83));

    private static readonly SolidColorBrush MutedBrush =
        new(Color.FromRgb(0x8C, 0x95, 0xA3));

    private static readonly SolidColorBrush WarningBrush =
        new(Color.FromRgb(0xFF, 0xC1, 0x5C));

    private static readonly SolidColorBrush ErrorBrush =
        new(Color.FromRgb(0xE5, 0x6B, 0x6F));

    static GoogleDriveStatusUiHelper()
    {
        SuccessBrush.Freeze();
        MutedBrush.Freeze();
        WarningBrush.Freeze();
        ErrorBrush.Freeze();
    }

    public static string GetStatusMessage(GoogleDriveCheckResult result)
    {
        return result switch
        {
            GoogleDriveCheckResult.DriveAvailable =>
                "Google Drive доступен",

            GoogleDriveCheckResult.NotAuthorized =>
                "Авторизация не сохранена. Подключите Google аккаунт.",

            GoogleDriveCheckResult.NoCredentials =>
                "Файл credentials.json не найден.",

            GoogleDriveCheckResult.TokenExpired =>
                "Токен недействителен. Отключите и подключите Google аккаунт заново.",

            GoogleDriveCheckResult.InsufficientPermissions =>
                "Недостаточно прав. Проверьте настройки приложения в Google Cloud.",

            GoogleDriveCheckResult.NetworkError =>
                "Нет соединения с Google. Проверьте интернет.",

            GoogleDriveCheckResult.Timeout =>
                "Превышено время проверки. Попробуйте позже.",

            _ =>
                "Не удалось проверить доступ к Google Drive."
        };
    }

    public static Brush GetStatusBrush(GoogleDriveCheckResult result)
    {
        return result switch
        {
            GoogleDriveCheckResult.DriveAvailable =>
                SuccessBrush,

            GoogleDriveCheckResult.NotAuthorized
                or GoogleDriveCheckResult.NoCredentials =>
                MutedBrush,

            GoogleDriveCheckResult.NetworkError
                or GoogleDriveCheckResult.Timeout =>
                WarningBrush,

            _ =>
                ErrorBrush
        };
    }

    public static string GetFolderStatusText(WorldHub.Core.Enums.FolderAccessStatus status)
    {
        return status switch
        {
            WorldHub.Core.Enums.FolderAccessStatus.Accessible =>
                "Google Drive: папка доступна",
            WorldHub.Core.Enums.FolderAccessStatus.NoAccess =>
                "Google Drive: нет доступа к папке",
            WorldHub.Core.Enums.FolderAccessStatus.FolderNotFound =>
                "Google Drive: папка не найдена или доступ ещё применяется (подождите пару секунд и нажмите Проверить)",
            WorldHub.Core.Enums.FolderAccessStatus.NotAuthorized =>
                "Google Drive: нет авторизации",
            WorldHub.Core.Enums.FolderAccessStatus.InsufficientScope =>
                "Google Drive: недостаточно прав приложения",
            WorldHub.Core.Enums.FolderAccessStatus.NetworkError =>
                "Google Drive: нет соединения",
            WorldHub.Core.Enums.FolderAccessStatus.Timeout =>
                "Google Drive: превышено время проверки",
            WorldHub.Core.Enums.FolderAccessStatus.UnknownError =>
                "Google Drive: неизвестная ошибка",
            _ =>
                "Доступ не проверен"
        };
    }

    public static Brush GetFolderStatusBrush(WorldHub.Core.Enums.FolderAccessStatus status)
    {
        return status switch
        {
            WorldHub.Core.Enums.FolderAccessStatus.Accessible =>
                SuccessBrush,
            WorldHub.Core.Enums.FolderAccessStatus.NoAccess
                or WorldHub.Core.Enums.FolderAccessStatus.FolderNotFound
                or WorldHub.Core.Enums.FolderAccessStatus.InsufficientScope =>
                ErrorBrush,
            WorldHub.Core.Enums.FolderAccessStatus.NetworkError
                or WorldHub.Core.Enums.FolderAccessStatus.Timeout =>
                WarningBrush,
            _ =>
                MutedBrush
        };
    }
}
