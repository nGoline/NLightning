using MessagePack;
using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Ipc.Handlers;

using Domain.Client.Constants;
using Domain.Client.Enums;
using Domain.Exceptions;
using Domain.Node.Interfaces;
using Interfaces;
using Services.Ipc.Factories;
using Transport.Ipc;
using Transport.Ipc.Requests;
using Transport.Ipc.Responses;

internal sealed class ConnectPeerIpcHandler : IIpcCommandHandler
{
    private readonly ILogger<ConnectPeerIpcHandler> _logger;
    private readonly IPeerManager _peerManager;

    public ClientCommand Command => ClientCommand.ConnectPeer;

    public ConnectPeerIpcHandler(ILogger<ConnectPeerIpcHandler> logger, IPeerManager peerManager)
    {
        _peerManager = peerManager;
        _logger = logger;
    }

    public async Task<IpcEnvelope> HandleAsync(IpcEnvelope envelope, CancellationToken ct)
    {
        try
        {
            // Deserialize the request
            var request =
                MessagePackSerializer.Deserialize<ConnectPeerIpcRequest>(envelope.Payload, cancellationToken: ct);

            // Validate the address
            if (string.IsNullOrWhiteSpace(request.Address.Address))
                return IpcErrorFactory.CreateErrorEnvelope(envelope, ErrorCodes.InvalidAddress,
                                                           "Invalid address: address cannot be empty");

            // Parse and connect to the peer
            var peer = await _peerManager.ConnectToPeerAsync(request.Address);

            _logger.LogInformation("Successfully connected to peer at {Address}", request.Address);

            // Create a success response
            var response = new ConnectPeerIpcResponse
            {
                Id = peer.NodeId,
                Features = peer.Features,
                IsInitiator = true,
                Address = peer.Host,
                Type = peer.Type,
                Port = peer.Port
            };

            var payload = MessagePackSerializer.Serialize(response, cancellationToken: ct);
            return new IpcEnvelope
            {
                Version = envelope.Version,
                Command = envelope.Command,
                CorrelationId = envelope.CorrelationId,
                Kind = IpcEnvelopeKind.Response,
                Payload = payload
            };
        }
        catch (FormatException fe)
        {
            _logger.LogWarning(fe, "Invalid peer address format");
            return IpcErrorFactory.CreateErrorEnvelope(envelope, ErrorCodes.InvalidAddress,
                                                       $"Invalid address format: {fe.Message}");
        }
        catch (InvalidOperationException oe)
        {
            _logger.LogInformation(oe, "The operation could not be completed");
            return IpcErrorFactory.CreateErrorEnvelope(envelope, ErrorCodes.InvalidOperation,
                                                       $"The operation could not be completed: {oe.Message}");
        }
        catch (ConnectionException ce)
        {
            _logger.LogError(ce, "Failed to connect to peer");
            return IpcErrorFactory.CreateErrorEnvelope(envelope, ErrorCodes.ConnectionError,
                                                       $"Connection failed: {ce.Message}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error connecting to peer");
            return IpcErrorFactory.CreateErrorEnvelope(envelope, ErrorCodes.ServerError,
                                                       $"Error connecting to peer: {e.Message}");
        }
    }
}