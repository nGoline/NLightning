using MessagePack;
using NLightning.Domain.Crypto.ValueObjects;

namespace NLightning.Daemon.Handlers;

using Interfaces;
using Transport.Ipc;
using Transport.Ipc.Responses;

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
        var ipcResp = new NodeInfoIpcResponse
        {
            Network = resp.Network,
            BestBlockHash = new Hash(Convert.FromHexString(resp.BestBlockHash)),
            BestBlockHeight = resp.BestBlockHeight,
            BestBlockTime = resp.BestBlockTime,
            Implementation = resp.Implementation,
            Version = resp.Version
        };
        var payload = MessagePackSerializer.Serialize(ipcResp, cancellationToken: ct);
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