using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using NetMQ;
using NetMQ.Sockets;

namespace NLightning.Infrastructure.Bitcoin.Wallet;

using Domain.Bitcoin.Events;
using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.Transactions.Models;
using Domain.Bitcoin.ValueObjects;
using Domain.Bitcoin.Wallet.Models;
using Domain.Channels.ValueObjects;
using Domain.Crypto.ValueObjects;
using Domain.Money;
using Domain.Node.Options;
using Domain.Persistence.Interfaces;
using Interfaces;
using Options;

public class BlockchainMonitorService : IBlockchainMonitor
{
    private readonly BitcoinOptions _bitcoinOptions;
    private readonly IBitcoinChainService _bitcoinChainService;
    private readonly ILogger<BlockchainMonitorService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Network _network;
    private readonly SemaphoreSlim _newBlockSemaphore = new(1, 1);
    private readonly SemaphoreSlim _blockBacklogSemaphore = new(1, 1);
    private readonly ConcurrentDictionary<uint256, WatchedTransactionModel> _watchedTransactions = new();
    private readonly ConcurrentDictionary<string, WalletAddressModel> _watchedAddresses = new();
    private readonly OrderedDictionary<uint, Block> _blocksToProcess = new();

    private BlockchainState _blockchainState = new(0, Hash.Empty, DateTime.UtcNow);
    private CancellationTokenSource? _cts;
    private Task? _monitoringTask;
    private uint _lastProcessedBlockHeight;
    private SubscriberSocket? _blockSocket;
    // private SubscriberSocket? _transactionSocket;

    public event EventHandler<NewBlockEventArgs>? OnNewBlockDetected;
    public event EventHandler<TransactionConfirmedEventArgs>? OnTransactionConfirmed;
    public event EventHandler<WalletMovementEventArgs>? OnWalletMovementDetected;

    public uint LastProcessedBlockHeight => _lastProcessedBlockHeight;

    public BlockchainMonitorService(IOptions<BitcoinOptions> bitcoinOptions, IBitcoinChainService bitcoinChainService,
                                    ILogger<BlockchainMonitorService> logger, IOptions<NodeOptions> nodeOptions,
                                    IServiceProvider serviceProvider)
    {
        _bitcoinOptions = bitcoinOptions.Value;
        _bitcoinChainService = bitcoinChainService;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _network = Network.GetNetwork(nodeOptions.Value.BitcoinNetwork) ?? Network.Main;
    }

    public async Task StartAsync(uint heightOfBirth, CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        using var scope = _serviceProvider.CreateScope();
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Load pending transactions
        await LoadPendingWatchedTransactionsAsync(uow);

        // Load existing addresses
        LoadBitcoinAddresses(uow);

        // Load UtxoSet
        await LoadUtxoSetAsync(uow);

        // Get the current state or create a new one if it doesn't exist
        var currentBlockchainState = await uow.BlockchainStateDbRepository.GetStateAsync();
        if (currentBlockchainState is null)
        {
            _logger.LogInformation("No blockchain state found, starting from height {Height}", heightOfBirth);

            _lastProcessedBlockHeight = heightOfBirth;
            _blockchainState = new BlockchainState(_lastProcessedBlockHeight, Hash.Empty, DateTime.UtcNow);
            uow.BlockchainStateDbRepository.Add(_blockchainState);
        }
        else
        {
            _blockchainState = currentBlockchainState;
            _lastProcessedBlockHeight = _blockchainState.LastProcessedHeight;
            _logger.LogInformation("Starting blockchain monitoring at height {Height}, last block hash {LastBlockHash}",
                                   _lastProcessedBlockHeight, _blockchainState.LastProcessedBlockHash);
        }

        // Get the current block height from the wallet
        var currentBlockHeight = await _bitcoinChainService.GetCurrentBlockHeightAsync();

        if (currentBlockHeight > _lastProcessedBlockHeight)
        {
            // Add the current block to the processing queue
            var currentBlock = await _bitcoinChainService.GetBlockAsync(_lastProcessedBlockHeight);
            if (currentBlock is not null)
                _blocksToProcess[_lastProcessedBlockHeight] = currentBlock;

            // Add missing blocks to the processing queue and process any pending blocks
            await AddMissingBlocksToProcessAsync(currentBlockHeight);
            await ProcessPendingBlocksAsync(uow);
        }

        await uow.SaveChangesAsync();

        // Initialize ZMQ sockets
        InitializeZmqSockets();

        // Start monitoring task
        _monitoringTask = MonitorBlockchainAsync(_cts.Token);

        _logger.LogInformation("Blockchain monitor service started successfully");
    }

    public async Task StopAsync()
    {
        if (_cts is null)
            throw new InvalidOperationException("Service is not running");

        await _cts.CancelAsync();

        if (_monitoringTask is not null)
        {
            try
            {
                await _monitoringTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
        }

        CleanupZmqSockets();
    }

    public async Task PublishAndWatchTransactionAsync(ChannelId channelId, SignedTransaction signedTransaction,
                                                      uint requiredDepth)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(
                "Publishing transaction {TxId} for {RequiredDepth} confirmations for channel {channelId}",
                signedTransaction.TxId, requiredDepth, channelId);

        // Convert the tx
        var transaction = Transaction.Load(signedTransaction.RawTxBytes, _network);

        // Start watching the tx
        await WatchTransactionAsync(channelId, signedTransaction.TxId, requiredDepth);

        // Publish the tx
        await _bitcoinChainService.SendTransactionAsync(transaction);
    }

    public async Task WatchTransactionAsync(ChannelId channelId, TxId txId, uint requiredDepth)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(
                "Watching transaction {TxId} for {RequiredDepth} confirmations for channel {channelId}",
                txId, requiredDepth, channelId);

        using var scope = _serviceProvider.CreateScope();
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var nBitcoinTxId = new uint256(txId);
        var watchedTx = new WatchedTransactionModel(channelId, txId, requiredDepth);

        uow.WatchedTransactionDbRepository.Add(watchedTx);

        _watchedTransactions[nBitcoinTxId] = watchedTx;

        await uow.SaveChangesAsync();
    }

    public void WatchBitcoinAddress(WalletAddressModel walletAddress)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Watching bitcoin address {walletAddress} for deposits", walletAddress);

        _watchedAddresses[walletAddress.Address] = walletAddress;
    }

    // public Task WatchForRevocationAsync(TxId commitmentTxId, SignedTransaction penaltyTx)
    // {
    //     _logger.LogInformation("Watching for revocation of commitment transaction {CommitmentTxId}", commitmentTxId);
    //
    //     var nBitcoinTxId = new uint256(commitmentTxId);
    //     var revocationWatch = new RevocationWatch(nBitcoinTxId, Transaction.Load(penaltyTx.RawTxBytes, _network));
    //
    //     _revocationWatches.TryAdd(nBitcoinTxId, revocationWatch);
    //     return Task.CompletedTask;
    // }

    private async Task MonitorBlockchainAsync(CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Starting blockchain monitoring loop");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Check for new blocks
                    if (_blockSocket != null &&
                        _blockSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out var topic))
                    {
                        if (topic == "rawblock" && _blockSocket.TryReceiveFrameBytes(out var blockHashBytes))
                        {
                            try
                            {
                                // One at a time
                                await _newBlockSemaphore.WaitAsync(cancellationToken);
                                var block = Block.Load(blockHashBytes, _network);
                                var coinbaseHeight = block.GetCoinbaseHeight();
                                if (!coinbaseHeight.HasValue)
                                {
                                    // Get the current height from the wallet
                                    var currentHeight = await _bitcoinChainService.GetCurrentBlockHeightAsync();

                                    // Get the block from the wallet
                                    var blockAtHeight = await _bitcoinChainService.GetBlockAsync(currentHeight);
                                    if (blockAtHeight is null)
                                    {
                                        _logger.LogError("Failed to retrieve block at height {Height}", currentHeight);
                                        return;
                                    }

                                    coinbaseHeight = (int)currentHeight;
                                }

                                await ProcessNewBlock(block, (uint)coinbaseHeight);
                            }
                            finally
                            {
                                _newBlockSemaphore.Release();
                            }
                        }
                    }

                    // TODO: Check for new transactions
                    // if (_transactionSocket != null &&
                    //     _transactionSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out var txTopic))
                    // {
                    //     if (txTopic == "rawtx" && _transactionSocket.TryReceiveFrameBytes(out var rawTxBytes))
                    //     {
                    //         await ProcessNewTransaction(rawTxBytes);
                    //     }
                    // }

                    // Small delay to prevent CPU spinning
                    await Task.Delay(50, cancellationToken);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error in blockchain monitoring loop");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Blockchain monitoring loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in blockchain monitoring loop");
        }
    }

    private void InitializeZmqSockets()
    {
        try
        {
            // Subscribe to new blocks
            _blockSocket = new SubscriberSocket();
            _blockSocket.Connect($"tcp://{_bitcoinOptions.ZmqHost}:{_bitcoinOptions.ZmqBlockPort}");
            _blockSocket.Subscribe("rawblock");

            // // Subscribe to new transactions (for mempool monitoring)
            // _transactionSocket = new SubscriberSocket();
            // _transactionSocket.Connect($"tcp://{_bitcoinOptions.ZmqHost}:{_bitcoinOptions.ZmqTxPort}");
            // _transactionSocket.Subscribe("rawtx");

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("ZMQ sockets initialized - Block: {BlockPort}, Tx: {TxPort}",
                                       _bitcoinOptions.ZmqBlockPort, _bitcoinOptions.ZmqTxPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ZMQ sockets");
            CleanupZmqSockets();
            throw;
        }
    }

    private void CleanupZmqSockets()
    {
        try
        {
            _blockSocket?.Dispose();
            _blockSocket = null;

            // _transactionSocket?.Dispose();
            // _transactionSocket = null;

            _logger.LogDebug("ZMQ sockets cleaned up");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up ZMQ sockets");
        }
    }

    private async Task ProcessPendingBlocksAsync(IUnitOfWork uow)
    {
        try
        {
            await _blockBacklogSemaphore.WaitAsync();

            while (_blocksToProcess.Count > 0)
            {
                var blockKvp = _blocksToProcess.First();
                if (blockKvp.Key <= _lastProcessedBlockHeight)
                    _logger.LogWarning("Possible reorg detected: Block {Height} is already processed.", blockKvp.Key);

                ProcessBlock(blockKvp.Value, blockKvp.Key, uow);
            }
        }
        finally
        {
            _blockBacklogSemaphore.Release();
        }
    }

    private async Task AddMissingBlocksToProcessAsync(uint currentHeight)
    {
        var lastProcessedHeight = _lastProcessedBlockHeight + 1;
        if (currentHeight > lastProcessedHeight)
        {
            _logger.LogWarning("Processing missed blocks from height {LastProcessedHeight} to {CurrentHeight}",
                               lastProcessedHeight, currentHeight);

            for (var height = lastProcessedHeight; height < currentHeight; height++)
            {
                if (_blocksToProcess.ContainsKey(height))
                    continue;

                // Add the missing block to the process queue
                var blockAtHeight = await _bitcoinChainService.GetBlockAsync(height);
                if (blockAtHeight is not null)
                {
                    _blocksToProcess[height] = blockAtHeight;
                }
                else
                {
                    _logger.LogError("Missing block at height {Height}", height);
                }
            }
        }
    }

    private async Task ProcessNewBlock(Block block, uint currentHeight)
    {
        using var scope = _serviceProvider.CreateScope();
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var blockHash = block.GetHash();

        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Processing block at height {blockHeight}: {BlockHash}", currentHeight, blockHash);

            // Check for missed blocks first
            await AddMissingBlocksToProcessAsync(currentHeight);

            // Store the current block for processing
            _blocksToProcess[currentHeight] = block;

            // Process missing blocks
            await ProcessPendingBlocksAsync(uow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing new block {BlockHash}", blockHash);
        }

        await uow.SaveChangesAsync();
    }

    // TODO: Check for revocation transactions in mempool
    // private async Task ProcessNewTransaction(byte[] rawTxBytes)
    // {
    //     try
    //     {
    //         var transaction = Transaction.Load(rawTxBytes, Network.Main);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Error processing new transaction from mempool");
    //     }
    // }

    private void ProcessBlock(Block block, uint height, IUnitOfWork uow)
    {
        try
        {
            var blockHash = block.GetHash();

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Processing block {Height} with {TxCount} transactions", height,
                                 block.Transactions.Count);

            // Notify listeners of the new block
            OnNewBlockDetected?.Invoke(this, new NewBlockEventArgs(height, blockHash.ToBytes()));

            // Check if watched transactions are included in this block
            CheckBlockForWatchedTransactions(block.Transactions, height, uow);

            // Check for deposits in this block
            CheckBlockForWalletMovement(block.Transactions, height, uow);

            // Update blockchain state
            _blockchainState.UpdateState(blockHash.ToBytes(), height);
            uow.BlockchainStateDbRepository.Update(_blockchainState);

            _blocksToProcess.Remove(height);

            // Update our internal state
            _lastProcessedBlockHeight = height;

            // Check watched for all transactions' depth
            CheckWatchedTransactionsDepth(uow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing block at height {Height}", height);
        }
    }

    private void ConfirmTransaction(uint blockHeight, IUnitOfWork uow, WatchedTransactionModel watchedTransaction)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(
                "Transaction {TxId} reached required depth of {depth} confirmations at block {blockHeight}",
                watchedTransaction.TransactionId, watchedTransaction.RequiredDepth, blockHeight);

        watchedTransaction.MarkAsCompleted();
        uow.WatchedTransactionDbRepository.Update(watchedTransaction);
        OnTransactionConfirmed?.Invoke(
            this, new TransactionConfirmedEventArgs(watchedTransaction, blockHeight));

        _watchedTransactions.TryRemove(new uint256(watchedTransaction.TransactionId), out _);
    }

    private void CheckBlockForWatchedTransactions(List<Transaction> blockTransactions, uint blockHeight,
                                                  IUnitOfWork uow)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug(
                "Checking {watchedTransactionCount} watched transactions for block {height} with {TxCount} transactions",
                _watchedTransactions.Count, blockHeight, blockTransactions.Count);

        ushort index = 0;
        foreach (var transaction in blockTransactions)
        {
            var txId = transaction.GetHash();

            if (!_watchedTransactions.TryGetValue(txId, out var watchedTransaction))
                continue;

            _logger.LogInformation("Transaction {TxId} found in block at height {Height}", txId, blockHeight);

            try
            {
                // Update first seen height
                watchedTransaction.SetHeightAndIndex(blockHeight, index);
                uow.WatchedTransactionDbRepository.Update(watchedTransaction);

                if (watchedTransaction.RequiredDepth == 0)
                    ConfirmTransaction(blockHeight, uow, watchedTransaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking confirmations for transaction {TxId}", txId);
            }
            finally
            {
                index++;
            }
        }
    }

    private void CheckBlockForWalletMovement(List<Transaction> transactions, uint blockHeight, IUnitOfWork uow)
    {
        if (_watchedAddresses.IsEmpty)
            return;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Checking {AddressCount} watched addresses for deposits/spends in block {Height}",
                             _watchedAddresses.Count, blockHeight);

        foreach (var transaction in transactions)
        {
            var txId = transaction.GetHash();

            // Check each output for deposits
            for (var i = 0; i < transaction.Outputs.Count; i++)
            {
                var output = transaction.Outputs[i];
                var destinationAddress = output.ScriptPubKey.GetDestinationAddress(_network);
                if (destinationAddress == null)
                    continue;

                if (!_watchedAddresses.TryGetValue(destinationAddress.ToString(), out var watchedAddress))
                    continue;

                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation(
                        "Deposit detected: {amount} to address {destinationAddress} in tx {txId} at block {height}",
                        output.Value, destinationAddress, txId, blockHeight);

                // Save Utxo to the database
                var utxo = new UtxoModel(txId.ToBytes(), (uint)i, LightningMoney.Satoshis(output.Value.Satoshi),
                                         blockHeight, watchedAddress);
                uow.AddUtxo(utxo);

                if (!_watchedAddresses.TryRemove(destinationAddress.ToString(), out _))
                    _logger.LogError("Unable to remove watched address {DestinationAddress} from the list",
                                     destinationAddress);

                OnWalletMovementDetected
                  ?.Invoke(this, new WalletMovementEventArgs(destinationAddress.ToString(),
                                                             LightningMoney.Satoshis(output.Value.Satoshi),
                                                             txId.ToBytes(),
                                                             blockHeight));
            }

            // Check each input for spent utxos
            foreach (var input in transaction.Inputs)
                uow.TrySpendUtxo(new TxId(input.PrevOut.Hash.ToBytes()), input.PrevOut.N);
        }
    }

    private void CheckWatchedTransactionsDepth(IUnitOfWork uow)
    {
        foreach (var (txId, watchedTransaction) in _watchedTransactions)
        {
            try
            {
                // The FirstSeenAtHeight represents 1 confirmation, so we have to add 1
                var confirmations = _lastProcessedBlockHeight - watchedTransaction.FirstSeenAtHeight + 1;
                if (confirmations >= watchedTransaction.RequiredDepth)
                    ConfirmTransaction(_lastProcessedBlockHeight, uow, watchedTransaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking confirmations for transaction {TxId}", txId);
            }
        }
    }

    private async Task LoadPendingWatchedTransactionsAsync(IUnitOfWork uow)
    {
        _logger.LogInformation("Loading watched transactions from database");

        var watchedTransactions = await uow.WatchedTransactionDbRepository.GetAllPendingAsync();
        foreach (var watchedTransaction in watchedTransactions)
        {
            _watchedTransactions[new uint256(watchedTransaction.TransactionId)] = watchedTransaction;
        }
    }

    private void LoadBitcoinAddresses(IUnitOfWork uow)
    {
        _logger.LogInformation("Loading bitcoin addresses from database");

        var bitcoinAddresses = uow.WalletAddressesDbRepository.GetAllAddresses();
        foreach (var bitcoinAddress in bitcoinAddresses)
        {
            _watchedAddresses[bitcoinAddress.Address] = bitcoinAddress;
        }
    }

    private async Task LoadUtxoSetAsync(IUnitOfWork uow)
    {
        _logger.LogInformation("Loading Utxo set");

        var utxoSet = (await uow.UtxoDbRepository.GetUnspentAsync()).ToList();
        if (utxoSet.Count > 0)
        {
            var utxoMemoryRepository = _serviceProvider.GetService<IUtxoMemoryRepository>() ??
                                       throw new InvalidOperationException(
                                           $"Error getting required service {nameof(IUtxoMemoryRepository)}");
            utxoMemoryRepository.Load(utxoSet);
        }
    }
}