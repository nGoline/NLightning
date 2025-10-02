using MessagePack;

namespace NLightning.Daemon.Handlers;

using Interfaces;
using Transport.Ipc;

public sealed class NodeInfoIpcHandler : IIpcCommandHandler
{
    private readonly INodeInfoQueryService _query;

    public NodeInfoIpcHandler(INodeInfoQueryService query)
    {
        _query = query;
    }

    public NodeIpcCommand Command => NodeIpcCommand.NodeInfo;

    public async Task<IpcEnvelope> HandleAsync(IpcEnvelope envelope, CancellationToken ct)
    {
        var resp = await _query.QueryAsync(ct);
        var payload = MessagePackSerializer.Serialize(resp, cancellationToken: ct);
        return new IpcEnvelope
        {
            Version = envelope.Version,
            Command = envelope.Command,
            CorrelationId = envelope.CorrelationId,
            Kind = 1,
            Payload = payload
        };
    }
}