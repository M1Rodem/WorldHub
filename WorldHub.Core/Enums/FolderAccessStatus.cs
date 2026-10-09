namespace WorldHub.Core.Enums;

public enum FolderAccessStatus
{
    /// <summary>Проверка не выполнялась.</summary>
    Unknown = 0,

    /// <summary>Папка доступна текущему аккаунту.</summary>
    Accessible = 1,

    /// <summary>Папка есть, но у аккаунта нет прав (403 без признака scope).</summary>
    NoAccess = 2,

    /// <summary>Папка не найдена (404).</summary>
    FolderNotFound = 3,

    /// <summary>Нет авторизации или токен недействителен.</summary>
    NotAuthorized = 4,

    /// <summary>Недостаточно scope (403 с reason=insufficientPermissions или insufficientFilePermissions).</summary>
    InsufficientScope = 5,

    /// <summary>Сеть недоступна.</summary>
    NetworkError = 6,

    /// <summary>Проверка не завершилась за отведённое время.</summary>
    Timeout = 7,

    /// <summary>Прочее.</summary>
    UnknownError = 8
}