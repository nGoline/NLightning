using MessagePack;

namespace NLightning.Transport.Ipc.Responses;

/// <summary>
/// Response for List Peers command
/// </summary>
[MessagePackObject]
public sealed class GetAddressIpcResponse
{
    [Key(0)] public string? AddressP2Tr { get; set; }
    [Key(1)] public string? AddressP2Wsh { get; set; }
}