using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Ipc.Handlers;

using Daemon.Handlers;
using Daemon.Interfaces;
using Domain.Client.Constants;
using Domain.Client.Enums;
using Domain.Client.Exceptions;
using Domain.Client.Requests;
using Domain.Client.Responses;
using Domain.Exceptions;
using Interfaces;
using Services.Ipc.Factories;
using Transport.Ipc;
using Transport.Ipc.Requests;
using Transport.Ipc.Responses;

public class OpenChannelSubscriptionIpcHandler : IIpcCommandHandler
{
    private readonly ILogger<OpenChannelSubscriptionIpcHandler> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ClientCommand Command => ClientCommand.OpenChannelSubscription;

    public OpenChannelSubscriptionIpcHandler(ILogger<OpenChannelSubscriptionIpcHandler> logger,
                                             IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<IpcEnvelope> HandleAsync(IpcEnvelope envelope, CancellationToken ct)
    {
        try
        {
            // Deserialize the request
            var request =
                MessagePackSerializer.Deserialize<OpenChannelSubscriptionIpcRequest>(
                    envelope.Payload, cancellationToken: ct);

            // Get the client handler
            using var scope = _serviceProvider.CreateScope();
            var openChannelClientSubscriptionHandler =
                scope.ServiceProvider.GetService(
                        typeof(IClientCommandHandler<OpenChannelClientSubscriptionRequest,
                            OpenChannelClientSubscriptionResponse>)) as
                    OpenChannelClientSubscriptionHandler ??
                throw new InvalidOperationException(
                    $"Unable to get service {nameof(OpenChannelClientSubscriptionHandler)}");

            var clientResponse = await openChannelClientSubscriptionHandler.HandleAsync(request.ToClientRequest(), ct);

            var payload = MessagePackSerializer.Serialize(
                OpenChannelSubscriptionIpcResponse.FromClientResponse(clientResponse),
                cancellationToken: ct);
            return new IpcEnvelope
            {
                Version = envelope.Version,
                Command = envelope.Command,
                CorrelationId = envelope.CorrelationId,
                Kind = IpcEnvelopeKind.Response,
                Payload = payload
            };
        }
        catch (ClientException ce)
        {
            _logger.LogError(ce, "Error while handling OpenChannelSubscription");
            return IpcErrorFactory.CreateErrorEnvelope(envelope, ce.Message, ce.Message);
        }
        catch (InvalidOperationException oe)
        {
            _logger.LogError(oe, "The operation could not be completed");
            return IpcErrorFactory.CreateErrorEnvelope(envelope, ErrorCodes.InvalidOperation,
                                                       $"The operation could not be completed: {oe.Message}");
        }
        catch (ConnectionException ce)
        {
            _logger.LogError(ce, "Failed to connect to peer");
            return IpcErrorFactory.CreateErrorEnvelope(envelope, ErrorCodes.ConnectionError,
                                                       $"Connection failed: {ce.Message}");
        }
        catch (ChannelErrorException cee)
        {
            _logger.LogError(cee, "Error opening Channel");
            return IpcErrorFactory.CreateErrorEnvelope(envelope, ErrorCodes.ConnectionError,
                                                       $"Channel Error: {cee.Message}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error opening channel");
            return IpcErrorFactory.CreateErrorEnvelope(envelope, ErrorCodes.ServerError,
                                                       $"Error opening channel: {e.Message}");
        }
    }
}