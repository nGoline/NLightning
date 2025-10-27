using MessagePack;

namespace NLightning.Transport.Ipc.Requests;

using Domain.Bitcoin.Enums;

/// <summary>
/// Request for Get Address command
/// </summary>
[MessagePackObject]
public class GetAddressIpcRequest
{
    [Key(0)] public AddressType AddressType { get; set; } = AddressType.P2Wpkh;
}