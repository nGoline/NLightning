using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Ipc.Handlers;

using Domain.Bitcoin.Enums;
using Domain.Client.Constants;
using Domain.Client.Enums;
using Infrastructure.Bitcoin.Wallet.Interfaces;
using Interfaces;
using Services.Ipc.Factories;
using Transport.Ipc;
using Transport.Ipc.Requests;
using Transport.Ipc.Responses;

internal class GetAddressIpcHandler : IIpcCommandHandler
{
    private readonly ILogger<GetAddressIpcHandler> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ClientCommand Command => ClientCommand.GetAddress;

    public GetAddressIpcHandler(ILogger<GetAddressIpcHandler> logger, IServiceProvider serviceProvider)
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
                MessagePackSerializer.Deserialize<GetAddressIpcRequest>(envelope.Payload, cancellationToken: ct);

            string? p2Tr = null;
            string? p2Wpkh = null;

            // Create a scope for this call
            using var scope = _serviceProvider.CreateScope();
            var walletAddressService = scope.ServiceProvider.GetService<IBitcoinWalletService>() ??
                                       throw new NullReferenceException(
                                           $"Error activating service {nameof(IBitcoinWalletService)}");

            // Get unused addresses by type
            if (request.AddressType.HasFlag(AddressType.P2Tr))
                p2Tr = (await walletAddressService.GetUnusedAddressAsync(AddressType.P2Tr, false)).Address;

            if (request.AddressType.HasFlag(AddressType.P2Wpkh))
                p2Wpkh = (await walletAddressService.GetUnusedAddressAsync(AddressType.P2Wpkh, false)).Address;

            // Create a success response
            var response = new GetAddressIpcResponse
            {
                AddressP2Tr = p2Tr,
                AddressP2Wsh = p2Wpkh
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
        catch (Exception e)
        {
            _logger.LogError(e, "Error getting a unused address");
            return IpcErrorFactory.CreateErrorEnvelope(envelope, ErrorCodes.ServerError,
                                                       $"Error getting a unused address: {e.Message}");
        }
    }
}