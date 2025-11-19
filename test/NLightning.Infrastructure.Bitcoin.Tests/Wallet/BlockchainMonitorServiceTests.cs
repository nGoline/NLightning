using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using NLightning.Tests.Utils.Mocks;

namespace NLightning.Infrastructure.Bitcoin.Tests.Wallet;

using Bitcoin.Wallet;
using Bitcoin.Wallet.Interfaces;
using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.Transactions.Models;
using Domain.Bitcoin.ValueObjects;
using Domain.Channels.ValueObjects;
using Domain.Persistence.Interfaces;
using Options;

public class BlockchainMonitorServiceTests
{
    private readonly Mock<IOptions<BitcoinOptions>> _mockBitcoinOptions;
    private readonly Mock<IBitcoinChainService> _mockBitcoinChainService;
    private readonly Mock<ILogger<BlockchainMonitorService>> _mockLogger;
    private readonly Mock<IOptions<Domain.Node.Options.NodeOptions>> _mockNodeOptions;
    private readonly FakeServiceProvider _fakeServiceProvider;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IBlockchainStateDbRepository> _mockBlockchainStateRepository;
    private readonly Mock<IWatchedTransactionDbRepository> _mockWatchedTransactionRepository;
    private readonly Mock<IWalletAddressesDbRepository> _mockWalletAddressesDbRepository;
    private readonly Mock<IUtxoDbRepository> _mockUtxoDbRepository;

    private readonly BlockchainMonitorService _service;

    public BlockchainMonitorServiceTests()
    {
        // Set up mock dependencies
        _mockBitcoinOptions = new Mock<IOptions<BitcoinOptions>>();
        _mockBitcoinOptions.Setup(x => x.Value).Returns(new BitcoinOptions
        {
            RpcEndpoint = "",
            RpcUser = "",
            RpcPassword = "",
            ZmqHost = "127.0.0.1",
            ZmqBlockPort = 28332,
            ZmqTxPort = 28333
        });

        _mockBitcoinChainService = new Mock<IBitcoinChainService>();
        _mockLogger = new Mock<ILogger<BlockchainMonitorService>>();

        _mockNodeOptions = new Mock<IOptions<Domain.Node.Options.NodeOptions>>();
        _mockNodeOptions.Setup(x => x.Value).Returns(new Domain.Node.Options.NodeOptions
        {
            BitcoinNetwork = "regtest"
        });

        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _fakeServiceProvider = new FakeServiceProvider();
        _fakeServiceProvider.AddService(typeof(IUnitOfWork), _mockUnitOfWork.Object);
        _mockBlockchainStateRepository = new Mock<IBlockchainStateDbRepository>();
        _mockWatchedTransactionRepository = new Mock<IWatchedTransactionDbRepository>();
        _mockWalletAddressesDbRepository = new Mock<IWalletAddressesDbRepository>();
        _mockUtxoDbRepository = new Mock<IUtxoDbRepository>();

        // Set up unit of work to return repositories
        _mockUnitOfWork.Setup(x => x.BlockchainStateDbRepository).Returns(_mockBlockchainStateRepository.Object);
        _mockUnitOfWork.Setup(x => x.WatchedTransactionDbRepository).Returns(_mockWatchedTransactionRepository.Object);
        _mockUnitOfWork.Setup(x => x.WalletAddressesDbRepository).Returns(_mockWalletAddressesDbRepository.Object);
        _mockUnitOfWork.Setup(x => x.UtxoDbRepository).Returns(_mockUtxoDbRepository.Object);

        // Create the service
        _service = new BlockchainMonitorService(
            _mockBitcoinOptions.Object,
            _mockBitcoinChainService.Object,
            _mockLogger.Object,
            _mockNodeOptions.Object,
            _fakeServiceProvider);
    }

    [Fact]
    public async Task StartAsync_WithExistingBlockchainState_LoadsStateAndPendingTransactions()
    {
        // Arrange
        var state = new BlockchainState(100, new byte[32], DateTime.UtcNow);
        var pendingTransactions = new List<WatchedTransactionModel>
        {
            new(new ChannelId(new byte[32]), new TxId(new byte[32]), 6)
        };

        _mockBlockchainStateRepository.Setup(x => x.GetStateAsync())
                                      .ReturnsAsync(state);

        _mockWatchedTransactionRepository.Setup(x => x.GetAllPendingAsync())
                                         .ReturnsAsync(pendingTransactions);

        _mockBitcoinChainService.Setup(x => x.GetCurrentBlockHeightAsync())
                                .ReturnsAsync(110u);

        _mockBitcoinChainService.Setup(x => x.GetBlockAsync(It.IsAny<uint>()))
                                .ReturnsAsync(Consensus.RegTest.ConsensusFactory.CreateBlock());

        // Act
        await _service.StartAsync(0, CancellationToken.None);

        // Assert
        _mockBlockchainStateRepository.Verify(x => x.GetStateAsync(), Times.Once);
        _mockWatchedTransactionRepository.Verify(x => x.GetAllPendingAsync(), Times.Once);
        _mockBitcoinChainService.Verify(x => x.GetCurrentBlockHeightAsync(), Times.Once);
        _mockBitcoinChainService.Verify(x => x.GetBlockAsync(100), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithNoBlockchainState_CreatesNewState()
    {
        // Arrange
        _mockBlockchainStateRepository.Setup(x => x.GetStateAsync())
                                      .ReturnsAsync((BlockchainState)null!);

        _mockWatchedTransactionRepository.Setup(x => x.GetAllPendingAsync())
                                         .ReturnsAsync(
                                              new List<WatchedTransactionModel>());

        _mockBitcoinChainService.Setup(x => x.GetCurrentBlockHeightAsync())
                                .ReturnsAsync(100u);

        _mockBitcoinChainService.Setup(x => x.GetBlockAsync(It.IsAny<uint>()))
                                .ReturnsAsync(Consensus.RegTest.ConsensusFactory.CreateBlock());

        // Act
        await _service.StartAsync(0, CancellationToken.None);

        // Assert
        _mockBlockchainStateRepository.Verify(x => x.Add(It.IsAny<BlockchainState>()), Times.Once);
    }

    [Fact]
    public async Task WatchTransactionAsync_AddsTransactionToDbAndInMemory()
    {
        // Arrange
        var channelId = new ChannelId(new byte[32]);
        var txId = new TxId(new byte[32]);
        const uint requiredDepth = 6;

        // Act
        await _service.WatchTransactionAsync(channelId, txId, requiredDepth);

        // Assert
        _mockWatchedTransactionRepository.Verify(
            x => x.Add(
                It.Is<WatchedTransactionModel>(t => t.ChannelId.Equals(channelId)
                                                 && t.TransactionId.Equals(txId)
                                                 && t.RequiredDepth == requiredDepth)),
            Times.Once);

        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ProcessNewBlock_AddsMissingBlocksAndProcessesThem()
    {
        // Arrange
        const uint currentBlockHeight = 110u;
        var block = Consensus.Main.ConsensusFactory.CreateBlock();

        // Setup to simulate blockchain state at height 100
        var state = new BlockchainState(100, new byte[32], DateTime.UtcNow);
        _mockBlockchainStateRepository.Setup(x => x.GetStateAsync())
                                      .ReturnsAsync(state);

        _mockWatchedTransactionRepository.Setup(x => x.GetAllPendingAsync())
                                         .ReturnsAsync(
                                              new List<WatchedTransactionModel>());

        _mockBitcoinChainService.Setup(x => x.GetCurrentBlockHeightAsync())
                                .ReturnsAsync(currentBlockHeight);

        _mockBitcoinChainService.Setup(x => x.GetBlockAsync(It.IsAny<uint>()))
                                .ReturnsAsync(block);

        await _service.StartAsync(0, CancellationToken.None);

        // Setup for tracking new block events
        var newBlockEventCalled = false;
        _service.OnNewBlockDetected += (_, _) => newBlockEventCalled = true;

        // Create a private method invoker to test ProcessNewBlock which is private
        var processNewBlockMethod = typeof(BlockchainMonitorService).GetMethod("ProcessNewBlock",
                                                                               System.Reflection.BindingFlags
                                                                                  .NonPublic |
                                                                               System.Reflection.BindingFlags.Instance)
                                 ?? throw new InvalidCastException("Can't find ProcessNewBlock method");

        // Act
        await (processNewBlockMethod.Invoke(_service, [block, currentBlockHeight]) as Task ??
               throw new InvalidCastException("Can't box ProcessNewBlock method as Task"));

        // Assert
        Assert.True(newBlockEventCalled, "New block event should have been raised");
        _mockBlockchainStateRepository.Verify(x => x.Update(It.IsAny<BlockchainState>()), Times.AtLeastOnce);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task StopAsync_CancelsTasksAndCleansUp()
    {
        // Arrange
        await _service.StartAsync(0, CancellationToken.None);

        // Act
        await _service.StopAsync();

        // Assert - Nothing to verify explicitly, just ensuring it doesn't throw
    }

    [Fact]
    public void OnTransactionConfirmed_RaisedWhenTransactionReachesRequiredDepth()
    {
        // Arrange
        var channelId = new ChannelId(new byte[32]);
        var txId = new TxId(new byte[32]);
        const uint requiredDepth = 1;

        var watchedTx = new WatchedTransactionModel(channelId, txId, requiredDepth);
        watchedTx.SetHeightAndIndex(100, 1);

        var transactionConfirmedCalled = false;
        _service.OnTransactionConfirmed += (_, args) =>
        {
            transactionConfirmedCalled = true;
            Assert.Equal(watchedTx, args.WatchedTransaction);
        };

        // Use reflection to access private methods/fields for testing
        var checkWatchedTransactionsDepthMethod = typeof(BlockchainMonitorService).GetMethod(
                                                      "CheckWatchedTransactionsDepth",
                                                      System.Reflection.BindingFlags.NonPublic |
                                                      System.Reflection.BindingFlags.Instance) ??
                                                  throw new NullReferenceException(
                                                      "Can't find CheckWatchedTransactionsDepth method");

        var watchedTransactionsField = typeof(BlockchainMonitorService).GetField("_watchedTransactions",
                                           System.Reflection.BindingFlags.NonPublic |
                                           System.Reflection.BindingFlags.Instance) ??
                                       throw new NullReferenceException("Can't find watchedTransactions field");

        var lastProcessedBlockHeightField = typeof(BlockchainMonitorService).GetField("_lastProcessedBlockHeight",
                                                System.Reflection.BindingFlags.NonPublic |
                                                System.Reflection.BindingFlags.Instance)
                                         ?? throw new NullReferenceException(
                                                "Can't find lastProcessedBlockHeight field");

        // Set the private fields
        var watchedTransactions =
            watchedTransactionsField.GetValue(_service) as ConcurrentDictionary<uint256, WatchedTransactionModel> ??
            throw new InvalidCastException("Can't get watchedTransactions field");
        watchedTransactions[new uint256(txId)] = watchedTx;

        lastProcessedBlockHeightField.SetValue(_service, 101u); // Setting block height to 101 to trigger confirmation

        // Act
        checkWatchedTransactionsDepthMethod.Invoke(_service, [_mockUnitOfWork.Object]);

        // Assert
        Assert.True(transactionConfirmedCalled, "Transaction confirmed event should have been raised");
        _mockWatchedTransactionRepository.Verify(
            x => x.Update(
                It.Is<WatchedTransactionModel>(t => t.ChannelId.Equals(channelId) &&
                                                    t.IsCompleted)), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithHeightOfBirth_CreatesStateAtSpecifiedHeight()
    {
        // Arrange
        const uint heightOfBirth = 50;
        uint? capturedStateHeight = null;

        _mockBlockchainStateRepository.Setup(x => x.GetStateAsync())
                                      .ReturnsAsync((BlockchainState)null!);

        _mockBlockchainStateRepository.Setup(x => x.Add(It.IsAny<BlockchainState>()))
                                      .Callback<BlockchainState>(state => capturedStateHeight =
                                                                              state.LastProcessedHeight);

        _mockWatchedTransactionRepository.Setup(x => x.GetAllPendingAsync())
                                         .ReturnsAsync(
                                              new List<WatchedTransactionModel>());

        _mockBitcoinChainService.Setup(x => x.GetCurrentBlockHeightAsync())
                                .ReturnsAsync(100u);

        _mockBitcoinChainService.Setup(x => x.GetBlockAsync(It.IsAny<uint>()))
                                .ReturnsAsync(Consensus.RegTest.ConsensusFactory.CreateBlock());

        // Act
        await _service.StartAsync(heightOfBirth, CancellationToken.None);
        await _service.StopAsync();

        // Assert
        Assert.NotNull(capturedStateHeight);
        Assert.Equal(heightOfBirth, capturedStateHeight);
    }

    [Fact]
    public async Task StartAsync_WithExistingStateAndHeightOfBirth_UsesExistingState()
    {
        // Arrange
        const uint heightOfBirth = 50;
        const uint existingHeight = 100;
        var state = new BlockchainState(existingHeight, new byte[32], DateTime.UtcNow);

        _mockBlockchainStateRepository.Setup(x => x.GetStateAsync())
                                      .ReturnsAsync(state);

        _mockWatchedTransactionRepository.Setup(x => x.GetAllPendingAsync())
                                         .ReturnsAsync(
                                              new List<WatchedTransactionModel>());

        _mockBitcoinChainService.Setup(x => x.GetCurrentBlockHeightAsync())
                                .ReturnsAsync(110u);

        _mockBitcoinChainService.Setup(x => x.GetBlockAsync(It.IsAny<uint>()))
                                .ReturnsAsync(Consensus.RegTest.ConsensusFactory.CreateBlock());

        // Act
        await _service.StartAsync(heightOfBirth, CancellationToken.None);

        // Assert
        // Should not create a new state since one already exists
        _mockBlockchainStateRepository.Verify(
            x => x.Add(It.IsAny<BlockchainState>()),
            Times.Never);

        // Should use the existing height, not the height of birth
        _mockBitcoinChainService.Verify(x => x.GetBlockAsync(existingHeight), Times.Once);
        _mockBitcoinChainService.Verify(x => x.GetBlockAsync(heightOfBirth), Times.Never);
    }

    [Fact]
    public async Task StartAsync_WithHigherHeightOfBirth_ProcessesMissingBlocks()
    {
        // Arrange
        const uint heightOfBirth = 50;

        _mockBlockchainStateRepository.Setup(x => x.GetStateAsync())
                                      .ReturnsAsync((BlockchainState)null!);

        _mockWatchedTransactionRepository.Setup(x => x.GetAllPendingAsync())
                                         .ReturnsAsync(
                                              new List<WatchedTransactionModel>());

        _mockBitcoinChainService.Setup(x => x.GetCurrentBlockHeightAsync())
                                .ReturnsAsync(55u); // The current height is higher than the height of birth

        _mockBitcoinChainService.Setup(x => x.GetBlockAsync(It.IsAny<uint>()))
                                .ReturnsAsync(Consensus.RegTest.ConsensusFactory.CreateBlock());

        // Act
        await _service.StartAsync(heightOfBirth, CancellationToken.None);

        // Assert
        // Should fetch blocks from heightOfBirth to current height
        _mockBitcoinChainService.Verify(x => x.GetBlockAsync(heightOfBirth), Times.Once);
        _mockBitcoinChainService.Verify(x => x.GetBlockAsync(51), Times.Once);
        _mockBitcoinChainService.Verify(x => x.GetBlockAsync(52), Times.Once);
        _mockBitcoinChainService.Verify(x => x.GetBlockAsync(53), Times.Once);
        _mockBitcoinChainService.Verify(x => x.GetBlockAsync(54), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithHeightOfBirthZero_StartsFromGenesis()
    {
        // Arrange
        const uint heightOfBirth = 0;
        uint? capturedStateHeight = null;

        _mockBlockchainStateRepository.Setup(x => x.GetStateAsync())
                                      .ReturnsAsync((BlockchainState)null!);

        _mockBlockchainStateRepository.Setup(x => x.Add(It.IsAny<BlockchainState>()))
                                      .Callback<BlockchainState>(state => capturedStateHeight =
                                                                              state.LastProcessedHeight);

        _mockWatchedTransactionRepository.Setup(x => x.GetAllPendingAsync())
                                         .ReturnsAsync(
                                              new List<WatchedTransactionModel>());

        _mockBitcoinChainService.Setup(x => x.GetCurrentBlockHeightAsync())
                                .ReturnsAsync(5u);

        _mockBitcoinChainService.Setup(x => x.GetBlockAsync(It.IsAny<uint>()))
                                .ReturnsAsync(Consensus.RegTest.ConsensusFactory.CreateBlock());

        // Act
        await _service.StartAsync(heightOfBirth, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedStateHeight);
        Assert.Equal(heightOfBirth, capturedStateHeight);

        // Should fetch blocks from genesis (0) onwards
        _mockBitcoinChainService.Verify(x => x.GetBlockAsync(0), Times.Once);
    }
}