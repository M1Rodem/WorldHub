using WorldHub.Network.Protocol;

namespace WorldHub.App.Services.Network;

public enum PeerCheckOutcome
{
    TcpUnavailable,
    HandshakeFailed,
    ProtocolFailed,
    ProfileReceived
}

public sealed record PeerCheckResult(
    PeerCheckOutcome Outcome,
    PeerInfo? Profile)
{
    public bool IsTcpAvailable =>
        Outcome != PeerCheckOutcome.TcpUnavailable;

    public bool IsWorldHubResponding =>
        Outcome == PeerCheckOutcome.ProfileReceived;

    public static PeerCheckResult TcpUnavailable() =>
        new(PeerCheckOutcome.TcpUnavailable, null);

    public static PeerCheckResult HandshakeFailed() =>
        new(PeerCheckOutcome.HandshakeFailed, null);

    public static PeerCheckResult ProtocolFailed() =>
        new(PeerCheckOutcome.ProtocolFailed, null);

    public static PeerCheckResult ProfileReceived(PeerInfo profile) =>
        new(PeerCheckOutcome.ProfileReceived, profile);
}