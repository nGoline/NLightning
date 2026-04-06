using NLightning.Daemon.Contracts.Control;

namespace NLightning.Daemon.Contracts;

public interface IControlClient
{
    Task<NodeInfoResponse> GetNodeInfoAsync(CancellationToken ct = default);
}