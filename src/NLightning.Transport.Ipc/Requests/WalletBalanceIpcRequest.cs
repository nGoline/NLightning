using MessagePack;

namespace NLightning.Transport.Ipc.Requests;

/// <summary>
/// Empty request for WalletBalance.
/// </summary>
[MessagePackObject]
public readonly struct WalletBalanceIpcRequest;