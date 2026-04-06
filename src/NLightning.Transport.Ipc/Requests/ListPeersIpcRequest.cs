using MessagePack;

namespace NLightning.Transport.Ipc.Requests;

/// <summary>
/// Empty request for ListPeers.
/// </summary>
[MessagePackObject]
public readonly struct ListPeersIpcRequest;