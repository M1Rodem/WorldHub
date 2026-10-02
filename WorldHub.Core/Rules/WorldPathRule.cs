namespace WorldHub.Core.Rules;

public static class WorldPathRule
{
    public static bool IsSame(string left, string right)
    {
        try
        {
            var normalizedLeft = Path.GetFullPath(left)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            var normalizedRight = Path.GetFullPath(right)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            return string.Equals(
                normalizedLeft,
                normalizedRight,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}