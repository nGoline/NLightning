using MessagePack;

namespace NLightning.Transport.Ipc.Responses;

/// <summary>
/// Response for List Peers command
/// </summary>
[MessagePackObject]
public sealed class ListPeersIpcResponse
{
    [Key(0)] public List<PeerInfoIpcResponse>? Peers { get; set; }
}