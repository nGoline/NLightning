using MessagePack;
using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Handlers;

using Domain.Bitcoin.Interfaces;
using Infrastructure.Bitcoin.Wallet.Interfaces;
using Interfaces;
using Services.Ipc.Factories;
using Transport.Ipc;
using Transport.Ipc.Constants;
using Transport.Ipc.Responses;

public class GetWalletBalanceIpcHandler : IIpcCommandHandler
{
    private readonly IBlockchainMonitor _blockchainMonitor;
    private readonly ILogger<GetWalletBalanceIpcHandler> _logger;
    private readonly IUtxoMemoryRepository _utxoMemoryRepository;
    public NodeIpcCommand Command => NodeIpcCommand.WalletBalance;

    public GetWalletBalanceIpcHandler(IBlockchainMonitor blockchainMonitor, ILogger<GetWalletBalanceIpcHandler> logger,
                                      IUtxoMemoryRepository utxoMemoryRepository)
    {
        _blockchainMonitor = blockchainMonitor;
        _logger = logger;
        _utxoMemoryRepository = utxoMemoryRepository;
    }

    public Task<IpcEnvelope> HandleAsync(IpcEnvelope envelope, CancellationToken ct)
    {
        try
        {
            var currentBlockHeight = _blockchainMonitor.LastProcessedBlockHeight;
            var confirmedBalance = _utxoMemoryRepository.GetConfirmedBalance(currentBlockHeight);
            var unconfirmedBalance = _utxoMemoryRepository.GetUnconfirmedBalance(currentBlockHeight);

            // Create a success response
            var response = new WalletBalanceIpcResponse
            {
                ConfirmedBalance = confirmedBalance,
                UnconfirmedBalance = unconfirmedBalance
            };

            var payload = MessagePackSerializer.Serialize(response, cancellationToken: ct);
            var respEnvelope = new IpcEnvelope
            {
                Version = envelope.Version,
                Command = envelope.Command,
                CorrelationId = envelope.CorrelationId,
                Kind = IpcEnvelopeKind.Response,
                Payload = payload
            };

            return Task.FromResult(respEnvelope);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error getting a unused address");
            return Task.FromResult(IpcErrorFactory.CreateErrorEnvelope(envelope, ErrorCodes.ServerError,
                                                                       $"Error getting a unused address: {e.Message}"));
        }
    }
}