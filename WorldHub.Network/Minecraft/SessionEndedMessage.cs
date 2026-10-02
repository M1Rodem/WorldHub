namespace WorldHub.Network.Minecraft;

public sealed record SessionEndedMessage(
    string SessionId,
    string WorldName,
    string WorldPath)
{
    private const string Prefix = "SESSION_ENDED|";

    public static bool TryParse(
        string? message,
        out SessionEndedMessage? result)
    {
        result = null;

        if (string.IsNullOrEmpty(message) ||
            !message.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = message.Split('|', 4);

        if (parts.Length != 4)
        {
            return false;
        }

        result = new SessionEndedMessage(
            parts[1],
            parts[2],
            parts[3]);

        return true;
    }
}