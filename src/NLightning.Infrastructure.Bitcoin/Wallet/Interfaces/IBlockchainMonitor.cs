namespace NLightning.Infrastructure.Bitcoin.Wallet.Interfaces;

using Domain.Bitcoin.Events;
using Domain.Bitcoin.ValueObjects;
using Domain.Bitcoin.Wallet.Models;
using Domain.Channels.ValueObjects;

public interface IBlockchainMonitor
{
    uint LastProcessedBlockHeight { get; }
    event EventHandler<NewBlockEventArgs> OnNewBlockDetected;
    event EventHandler<TransactionConfirmedEventArgs> OnTransactionConfirmed;

    Task PublishAndWatchTransactionAsync(ChannelId channelId, SignedTransaction signedTransaction, uint requiredDepth);
    Task WatchTransactionAsync(ChannelId channelId, TxId txId, uint requiredDepth);
    void WatchBitcoinAddress(WalletAddressModel walletAddress);

    /// <summary>
    /// Starts a background task to periodically refresh the fee rate
    /// </summary>
    /// <param name="heightOfBirth">Wallet's height of birth to avoid processing old blocks</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task StartAsync(uint heightOfBirth, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the background task and cancels any ongoing operations within the service.
    /// </summary>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    Task StopAsync();
}