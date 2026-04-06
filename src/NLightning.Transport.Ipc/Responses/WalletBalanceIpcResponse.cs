using MessagePack;

namespace NLightning.Transport.Ipc.Responses;

using Domain.Money;

/// <summary>
/// Response for Wallet Balance command
/// </summary>
[MessagePackObject]
public sealed class WalletBalanceIpcResponse
{
    [Key(0)] public required LightningMoney ConfirmedBalance { get; init; }
    [Key(1)] public required LightningMoney UnconfirmedBalance { get; init; }
}