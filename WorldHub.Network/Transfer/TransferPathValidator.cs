using System.IO;

namespace WorldHub.Network.Transfer;

/// <summary>
/// Валидация и нормализация путей, приходящих по сети.
/// </summary>
public static class TransferPathValidator
{
    public static string ValidateRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidDataException("Received file path is empty.");
        }

        var normalized = relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException("Received file path must be relative.");
        }

        var segments = normalized.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException(
                "Received file path contains invalid traversal segments.");
        }

        return normalized;
    }

    public static bool IsUnderRoot(string path, string rootPath)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetFullPath(rootPath);

        return fullPath.StartsWith(
            root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }
}