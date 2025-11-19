using MessagePack;
using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Ipc.Handlers;

using Domain.Client.Constants;
using Domain.Client.Enums;
using Domain.Node.Interfaces;
using Interfaces;
using Services.Ipc.Factories;
using Transport.Ipc;
using Transport.Ipc.Responses;

internal class ListPeersIpcHandler : IIpcCommandHandler
{
    private readonly IPeerManager _peerManager;
    private readonly ILogger<ListPeersIpcHandler> _logger;

    public ClientCommand Command => ClientCommand.ListPeers;

    public ListPeersIpcHandler(IPeerManager peerManager, ILogger<ListPeersIpcHandler> logger)
    {
        _peerManager = peerManager;
        _logger = logger;
    }

    public Task<IpcEnvelope> HandleAsync(IpcEnvelope envelope, CancellationToken ct)
    {
        try
        {
            var resp = _peerManager.ListPeers();
            var ipcResp = new ListPeersIpcResponse();

            if (resp.Count > 0)
            {
                ipcResp.Peers = new List<PeerInfoIpcResponse>(resp.Count);
                foreach (var peer in resp)
                {
                    ipcResp.Peers.Add(new PeerInfoIpcResponse
                    {
                        Address = $"{peer.Host}:{peer.Port}",
                        Connected = true,
                        Features = peer.Features,
                        Id = peer.NodeId,
                        ChannelQty = (uint)(peer.Channels?.Count ?? 0)
                    });
                }
            }

            var payload = MessagePackSerializer.Serialize(ipcResp, cancellationToken: ct);
            var responseEnvelope = new IpcEnvelope
            {
                Version = envelope.Version,
                Command = envelope.Command,
                CorrelationId = envelope.CorrelationId,
                Kind = IpcEnvelopeKind.Response,
                Payload = payload
            };
            return Task.FromResult(responseEnvelope);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error listing peers");
            return Task.FromResult(IpcErrorFactory.CreateErrorEnvelope(envelope, ErrorCodes.ServerError, e.Message));
        }
    }
}