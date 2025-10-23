using MessagePack;

namespace NLightning.Transport.Ipc.Requests;

/// <summary>
/// Empty request for NodeInfo.
/// </summary>
[MessagePackObject]
public readonly struct NodeInfoIpcRequest;