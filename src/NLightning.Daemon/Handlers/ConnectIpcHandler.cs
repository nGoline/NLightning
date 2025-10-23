using MessagePack;
using Microsoft.Extensions.Logging;
using NLightning.Transport.Ipc.Responses;

namespace NLightning.Daemon.Handlers;

using Domain.Node.Interfaces;
using Interfaces;
using Transport.Ipc;
using Transport.Ipc.Requests;

public sealed class ConnectIpcHandler : IIpcCommandHandler
{
    private readonly IPeerManager _peerManager;
    private readonly ILogger<ConnectIpcHandler> _logger;

    public ConnectIpcHandler(IPeerManager peerManager, ILogger<ConnectIpcHandler> logger)
    {
        _peerManager = peerManager;
        _logger = logger;
    }

    public NodeIpcCommand Command => NodeIpcCommand.ConnectPeer;

    public async Task<IpcEnvelope> HandleAsync(IpcEnvelope envelope, CancellationToken ct)
    {
        try
        {
            // Deserialize the request
            var request =
                MessagePackSerializer.Deserialize<ConnectPeerIpcRequest>(envelope.Payload, cancellationToken: ct);

            // Validate the address
            if (string.IsNullOrWhiteSpace(request.Address.Address))
                return CreateErrorResponse(envelope, "Invalid address: address cannot be empty");

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
                Kind = 1,
                Payload = payload
            };
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Invalid peer address format");
            return CreateErrorResponse(envelope, $"Invalid address format: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to peer");
            return CreateErrorResponse(envelope, $"Connection failed: {ex.Message}");
        }
    }

    private static IpcEnvelope CreateErrorResponse(IpcEnvelope envelope, string errorMessage)
    {
        var response = new IpcError
        {
            Code = "1",
            Message = errorMessage
        };

        var payload = MessagePackSerializer.Serialize(response);
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