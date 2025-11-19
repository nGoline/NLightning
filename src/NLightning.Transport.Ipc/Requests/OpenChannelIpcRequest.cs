using MessagePack;

namespace NLightning.Transport.Ipc.Requests;

using Domain.Client.Requests;
using Domain.Money;

/// <summary>
/// Empty request for OpenChannel.
/// </summary>
[MessagePackObject]
public sealed class OpenChannelIpcRequest
{
    [Key(0)] public required string NodeInfo { get; init; }
    [Key(2)] public required LightningMoney Amount { get; init; }

    public OpenChannelClientRequest ToClientRequest()
    {
        return new OpenChannelClientRequest(NodeInfo, Amount);
    }
}