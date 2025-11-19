using MessagePack;
using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Ipc.Handlers;

using Daemon.Interfaces;
using Domain.Client.Constants;
using Domain.Client.Enums;
using Domain.Crypto.ValueObjects;
using Interfaces;
using Services.Ipc.Factories;
using Transport.Ipc;
using Transport.Ipc.Responses;

internal sealed class NodeInfoIpcHandler : IIpcCommandHandler
{
    private readonly INodeInfoQueryService _query;
    private readonly ILogger<NodeInfoIpcHandler> _logger;

    public ClientCommand Command => ClientCommand.NodeInfo;

    public NodeInfoIpcHandler(INodeInfoQueryService query, ILogger<NodeInfoIpcHandler> logger)
    {
        _query = query;
        _logger = logger;
    }

    public async Task<IpcEnvelope> HandleAsync(IpcEnvelope envelope, CancellationToken ct)
    {
        try
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
                Kind = IpcEnvelopeKind.Response,
                Payload = payload
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error handling node info");
            return IpcErrorFactory.CreateErrorEnvelope(envelope, ErrorCodes.ServerError, e.Message);
        }
    }
}