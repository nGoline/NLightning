using NLightning.Daemon.Contracts.Control;

namespace NLightning.Daemon.Interfaces;
public interface INodeInfoQueryService
{
    Task<NodeInfoResponse> QueryAsync(CancellationToken ct);
}