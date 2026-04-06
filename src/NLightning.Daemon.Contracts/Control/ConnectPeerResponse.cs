namespace NLightning.Daemon.Contracts.Control;

/// <summary>
/// Transport-agnostic response for ConnectPeer command.
/// </summary>
public sealed class ConnectPeerResponse
{
    public string Id { get; set; } = string.Empty;
    public string Features { get; set; } = string.Empty;
    public bool IsInitiator { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public uint Port { get; set; }
}