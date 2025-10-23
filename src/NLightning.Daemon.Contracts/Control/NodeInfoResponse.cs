namespace NLightning.Daemon.Contracts.Control;

/// <summary>
/// Transport-agnostic response for NodeInfo command.
/// </summary>
public sealed class NodeInfoResponse
{
    public string Network { get; init; } = string.Empty;
    public string BestBlockHash { get; init; } = string.Empty;
    public long BestBlockHeight { get; init; }
    public DateTimeOffset? BestBlockTime { get; init; }
    public string? Implementation { get; init; } = "NLightning";
    public string? Version { get; init; }
}