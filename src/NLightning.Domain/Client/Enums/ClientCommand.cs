namespace NLightning.Domain.Client.Enums;

/// <summary>
/// Commands sent by a client.
/// </summary>
public enum ClientCommand
{
    // Reserve 0 for unknown
    Unknown = 0,
    NodeInfo = 1,
    ConnectPeer = 2,
    ListPeers = 3,
    GetAddress = 4,
    WalletBalance = 5,
    OpenChannel = 6
}