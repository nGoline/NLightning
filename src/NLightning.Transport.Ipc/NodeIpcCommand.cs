namespace NLightning.Transport.Ipc;

/// <summary>
/// Commands supported by the IPC protocol.
/// </summary>
public enum NodeIpcCommand
{
    // Reserve 0 for unknown
    Unknown = 0,
    NodeInfo = 1,
    ConnectPeer = 2,
    ListPeers = 3,
    GetAddress = 4
}