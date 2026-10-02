namespace WorldHub.Core.Rules;

public static class TransferSnapshotCountRule
{
    public const int Min = 1;
    public const int Max = 3;

    public static bool IsValid(int snapshotCount)
    {
        return snapshotCount is >= Min and <= Max;
    }
}