using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq.Protected;
using NBitcoin;
using NLightning.Application;
using NLightning.Daemon.Handlers;
using NLightning.Daemon.Interfaces;
using NLightning.Domain.Bitcoin.Events;
using NLightning.Domain.Bitcoin.Interfaces;
using NLightning.Domain.Bitcoin.Transactions.Factories;
using NLightning.Domain.Bitcoin.Transactions.Interfaces;
using NLightning.Domain.Channels.Enums;
using NLightning.Domain.Channels.Factories;
using NLightning.Domain.Channels.Interfaces;
using NLightning.Domain.Channels.Validators;
using NLightning.Domain.Client.Requests;
using NLightning.Domain.Client.Responses;
using NLightning.Domain.Crypto.Hashes;
using NLightning.Domain.Enums;
using NLightning.Domain.Money;
using NLightning.Domain.Node.Interfaces;
using NLightning.Domain.Node.Options;
using NLightning.Domain.Node.ValueObjects;
using NLightning.Domain.Protocol.Constants;
using NLightning.Domain.Protocol.Interfaces;
using NLightning.Domain.Protocol.ValueObjects;
using NLightning.Infrastructure;
using NLightning.Infrastructure.Bitcoin;
using NLightning.Infrastructure.Bitcoin.Builders;
using NLightning.Infrastructure.Bitcoin.Options;
using NLightning.Infrastructure.Bitcoin.Services;
using NLightning.Infrastructure.Bitcoin.Signers;
using NLightning.Infrastructure.Bitcoin.Wallet.Interfaces;
using NLightning.Infrastructure.Persistence;
using NLightning.Infrastructure.Persistence.Contexts;
using NLightning.Infrastructure.Repositories;
using NLightning.Infrastructure.Serialization;
using NLightning.Tests.Utils;
using ServiceStack;

namespace NLightning.Integration.Tests.Docker;

using Fixtures;
using Mock;
using TestCollections;
using Utils;

[Collection(LightningRegtestNetworkFixtureCollection.Name)]
public class ChannelOpeningFlowTests : IDisposable
{
    private readonly LightningRegtestNetworkFixture _lightningRegtestNetworkFixture;
    private readonly IPeerManager _peerManager;
    private readonly IChannelMemoryRepository _channelMemoryRepository;
    private readonly IBlockchainMonitor _blockchainMonitor;
    private readonly int _port;
    private readonly string _databaseFilePath = $"nlightning_channel_test_{Guid.NewGuid()}.db";
    private readonly IServiceProvider _serviceProvider;

    public ChannelOpeningFlowTests(LightningRegtestNetworkFixture fixture, ITestOutputHelper output)
    {
        _lightningRegtestNetworkFixture = fixture;
        Console.SetOut(new TestOutputWriter(output));

        _port = PortPoolUtil.GetAvailablePortAsync().GetAwaiter().GetResult();
        Assert.True(_port > 0);
        ISecureKeyManager secureKeyManager = new FakeSecureKeyManager();

        // Get Bitcoin network info
        Assert.NotNull(_lightningRegtestNetworkFixture.Builder);
        var bitcoinConfiguration = _lightningRegtestNetworkFixture.Builder.Configuration.BTCNodes[0];
        var zmqRawBlockPort =
            bitcoinConfiguration.Cmd.First(c => c.Contains("-zmqpubrawblock")).Split(':')[2];
        var zmqRawTxPort =
            bitcoinConfiguration.Cmd.First(c => c.Contains("-zmqpubrawtx")).Split(':')[2];
        var bitcoin = _lightningRegtestNetworkFixture.Builder.BitcoinRpcClient;
        Assert.NotNull(bitcoin);
        var bitcoinEndpoint = bitcoin.Address.ToString();

        // Mock HttpClient for FeeService
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        httpMessageHandlerMock.Protected()
                              .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                                                                ItExpr.IsAny<CancellationToken>())
                              .ReturnsAsync(() => new HttpResponseMessage
                              {
                                  StatusCode = HttpStatusCode.OK,
                                  Content = new StringContent("{\"fastestFee\": 2}")
                              });

        // Build configuration
        List<KeyValuePair<string, string?>> inMemoryConfiguration =
        [
            new("Serilog:MinimumLevel:NLightning", "Verbose"),
            new("Node:Network", "regtest"),
            new("Node:Daemon", "false"),
            new("Database:Provider", "Sqlite"),
            new("Database:ConnectionString", $"Data Source={_databaseFilePath}"),
            new("Bitcoin:RpcEndpoint", bitcoinEndpoint),
            new("Bitcoin:RpcUser", bitcoin.CredentialString.UserPassword.UserName),
            new("Bitcoin:RpcPassword", bitcoin.CredentialString.UserPassword.Password),
            new("Bitcoin:ZmqHost", bitcoin.Address.Host),
            new("Bitcoin:ZmqBlockPort", zmqRawBlockPort),
            new("Bitcoin:ZmqTxPort", zmqRawTxPort)
        ];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfiguration).Build();

        // Create a service collection
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpClient<IFeeService, FeeService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });
        services.AddSingleton(secureKeyManager);
        services.AddSingleton<IChannelOpenValidator>(sp =>
        {
            var nodeOptions = sp.GetRequiredService<IOptions<NodeOptions>>().Value;
            return new ChannelOpenValidator(nodeOptions);
        });
        services.AddSingleton<IChannelFactory>(sp =>
        {
            var channelIdFactory = sp.GetRequiredService<IChannelIdFactory>();
            var channelOpenValidator = sp.GetRequiredService<IChannelOpenValidator>();
            var feeService = sp.GetRequiredService<IFeeService>();
            var lightningSigner = sp.GetRequiredService<ILightningSigner>();
            var nodeOptions = sp.GetRequiredService<IOptions<NodeOptions>>().Value;
            var sha256 = sp.GetRequiredService<ISha256>();
            return new ChannelFactory(channelIdFactory, channelOpenValidator, feeService, lightningSigner, nodeOptions,
                                      sha256);
        });
        services.AddSingleton<ICommitmentTransactionModelFactory, CommitmentTransactionModelFactory>();
        services.AddSingleton<ILightningSigner>(serviceProvider =>
        {
            var fundingOutputBuilder = serviceProvider.GetRequiredService<IFundingOutputBuilder>();
            var keyDerivationService = serviceProvider.GetRequiredService<IKeyDerivationService>();
            var logger = serviceProvider.GetRequiredService<ILogger<LocalLightningSigner>>();
            var nodeOptions = serviceProvider.GetRequiredService<IOptions<NodeOptions>>().Value;
            var utxoMemoryRepository = serviceProvider.GetRequiredService<IUtxoMemoryRepository>();

            return new LocalLightningSigner(fundingOutputBuilder, keyDerivationService, logger, nodeOptions,
                                            secureKeyManager, utxoMemoryRepository);
        });
        services.AddApplicationServices();
        services.AddInfrastructureServices();
        services.AddPersistenceInfrastructureServices(configuration);
        services.AddRepositoriesInfrastructureServices();
        services.AddSerializationInfrastructureServices();
        services.AddBitcoinInfrastructure();
        services
           .AddScoped<IClientCommandHandler<OpenChannelClientRequest, OpenChannelClientResponse>,
                OpenChannelClientHandler>();
        services
           .AddScoped<IClientCommandHandler<OpenChannelClientSubscriptionRequest, OpenChannelClientSubscriptionResponse>
                ,
                OpenChannelClientSubscriptionHandler>();
        services.AddSingleton<IFundingTransactionModelFactory, FundingTransactionModelFactory>();
        services.AddOptions<BitcoinOptions>().BindConfiguration("Bitcoin").ValidateOnStart();
        services.AddOptions<FeeEstimationOptions>().BindConfiguration("FeeEstimation").ValidateOnStart();
        services.AddOptions<NodeOptions>()
                .BindConfiguration("Node")
                .PostConfigure(options =>
                 {
                     options.Features = new FeatureOptions
                     {
                         ChainHashes = [ChainConstants.Regtest]
                     };
                     options.ListenAddresses = [$"{IPAddress.Loopback}:{_port}"];
                     options.BitcoinNetwork = BitcoinNetwork.Regtest;
                     options.Features.ChainHashes = [options.BitcoinNetwork.ChainHash];
                     options.ToSelfDelay = 240;
                 })
                .ValidateOnStart();

        // Set up factories
        _serviceProvider = services.BuildServiceProvider();

        // Set up the database migration
        var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NLightningDbContext>();
        var pendingMigrations = context.Database.GetPendingMigrationsAsync().GetAwaiter().GetResult().ToList();
        if (pendingMigrations.Count > 0)
            context.Database.Migrate();

        // Get services
        _peerManager = _serviceProvider.GetRequiredService<IPeerManager>();
        _channelMemoryRepository = _serviceProvider.GetRequiredService<IChannelMemoryRepository>();
        _blockchainMonitor = _serviceProvider.GetRequiredService<IBlockchainMonitor>();
    }

    [Fact]
    public async Task GivenSingleP2WPKHInput_WhenHandleAsyncIsCalled_ChannelOpensCorrectly()
    {
        // Arrange
        var bitcoin = _lightningRegtestNetworkFixture.Builder!.BitcoinRpcClient!;
        var alice = _lightningRegtestNetworkFixture.Builder.LNDNodePool!.ReadyNodes
                                                   .First(x => x.LocalAlias == "alice");
        Assert.NotNull(alice);

        await _peerManager.StartAsync(TestContext.Current.CancellationToken);

        // Get the current block height
        var currentHeight = (uint)await bitcoin.GetBlockCountAsync(TestContext.Current.CancellationToken);

        // Start the blockchain monitor at the current height
        await _blockchainMonitor.StartAsync(currentHeight, TestContext.Current.CancellationToken);

        // Fund our wallet
        using (var scope = _serviceProvider.CreateScope())
        {
            var walletService = scope.ServiceProvider
                                     .GetRequiredService<IBitcoinWalletService>();
            var address = await walletService.GetUnusedAddressAsync(Domain.Bitcoin.Enums.AddressType.P2Wpkh, false);

            // Subscribe to blockchain monitor events
            TaskCompletionSource<bool> tsc = new();
            uint txFirstSeenInBlock = int.MaxValue;

            void OnWalletMovementDetected(object? _, WalletMovementEventArgs e)
            {
                if (e.Amount == LightningMoney.FromUnit(1, LightningMoneyUnit.Btc)
                 && e.WalletAddress == address.Address)
                {
                    txFirstSeenInBlock = e.BlockHeight;
                }
                else
                {
                    Assert.Fail("Unexpected wallet movement detected: " +
                                $"Address={e.WalletAddress}, Amount={e.Amount}, TxId={e.TxId}, BlockHeight={e.BlockHeight}");
                }
            }

            void OnNewBlockDetected(object? _, NewBlockEventArgs e)
            {
                if (e.Height >= txFirstSeenInBlock + 5)
                    tsc.TrySetResult(true);
            }

            _blockchainMonitor.OnWalletMovementDetected += OnWalletMovementDetected;
            _blockchainMonitor.OnNewBlockDetected += OnNewBlockDetected;

            // Send funds to our wallet
            await bitcoin.SendToAddressAsync(BitcoinAddress.Create(address.Address, NBitcoin.Network.RegTest),
                                             new Money(1, MoneyUnit.BTC), TestContext.Current.CancellationToken);

            // Mine blocks to confirm
            await bitcoin.GenerateToAddressAsync(
                6, await bitcoin.GetNewAddressAsync(TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken);

            // wait for funding transaction to be confirmed
            Assert.True(await tsc.Task);
            _blockchainMonitor.OnWalletMovementDetected -= OnWalletMovementDetected;
            _blockchainMonitor.OnNewBlockDetected -= OnNewBlockDetected;
        }

        // Verify we have balance
        var utxoRepository = _serviceProvider.GetRequiredService<IUtxoMemoryRepository>();
        var balance = utxoRepository.GetConfirmedBalance(_blockchainMonitor.LastProcessedBlockHeight);
        Assert.True(balance > LightningMoney.Zero);

        // Connect to Alice
        var aliceHost = new IPEndPoint(
            (await Dns.GetHostAddressesAsync(alice.Host.SplitOnFirst("//")[1].SplitOnFirst(":")[0],
                                             TestContext.Current.CancellationToken)).First(), 9735);
        var aliceAddress = $"{Convert.ToHexString(alice.LocalNodePubKeyBytes)}@{aliceHost}";

        await _peerManager.ConnectToPeerAsync(new PeerAddressInfo(aliceAddress));

        Task<OpenChannelClientResponse> openChannelTask;
        // Open channel - using the client handler
        using (var scope = _serviceProvider.CreateScope())
        {
            var clientHandler = scope.ServiceProvider.GetService(
                                        typeof(IClientCommandHandler<OpenChannelClientRequest,
                                            OpenChannelClientResponse>)) as
                                    OpenChannelClientHandler ??
                                throw new InvalidOperationException(
                                    $"Unable to get service {nameof(OpenChannelClientHandler)}");
            var request = new OpenChannelClientRequest(
                aliceAddress,
                LightningMoney.Satoshis(1000000) // 0.01 BTC,
            )
            {
                FeeRatePerKw = LightningMoney.Satoshis(10000)
            };

            // Act - Open the channel (this should send open_channel and wait for the first response)
            openChannelTask = clientHandler.HandleAsync(request, TestContext.Current.CancellationToken);
        }

        var channelResponse = await openChannelTask;
        Assert.NotNull(channelResponse);

        var channelOpen = false;
        while (!channelOpen)
        {
            Task<OpenChannelClientSubscriptionResponse> openChannelSubscriptionTask;
            // Open channel subscription - using the client handler
            using (var scope = _serviceProvider.CreateScope())
            {
                var subscriptionClientHandler = scope.ServiceProvider.GetService(
                                                        typeof(IClientCommandHandler<
                                                            OpenChannelClientSubscriptionRequest,
                                                            OpenChannelClientSubscriptionResponse>)) as
                                                    OpenChannelClientSubscriptionHandler ??
                                                throw new InvalidOperationException(
                                                    $"Unable to get service {nameof(OpenChannelClientSubscriptionHandler)}");
                var request = new OpenChannelClientSubscriptionRequest(channelResponse.ChannelId);

                // Act - Open the channel (this should send open_channel and wait for the first response)
                openChannelSubscriptionTask =
                    subscriptionClientHandler.HandleAsync(request, TestContext.Current.CancellationToken);
            }

            var channelSubscriptionResponse = await openChannelSubscriptionTask;
            Assert.NotNull(channelSubscriptionResponse);

            if (channelSubscriptionResponse.ChannelState == ChannelState.V1FundingSigned)
            {
                // Mine blocks to confirm
                _ = bitcoin.GenerateToAddressAsync(
                    6, await bitcoin.GetNewAddressAsync(TestContext.Current.CancellationToken),
                    TestContext.Current.CancellationToken);
            }
            else if (channelSubscriptionResponse.ChannelState is ChannelState.ReadyForThem or ChannelState.ReadyForUs
                  or ChannelState.Open)
            {
                channelOpen = true;
            }
        }

        // Check if the channel exists (temporary or permanent)
        var allChannels = _channelMemoryRepository.FindChannels(_ => true);

        Assert.True(allChannels.Count > 0, "Expected at least one channel to be created");
    }

    [Fact]
    public async Task GivenMultipleP2WPKHInput_WhenHandleAsyncIsCalled_ChannelOpensCorrectly()
    {
        // Arrange
        var bitcoin = _lightningRegtestNetworkFixture.Builder!.BitcoinRpcClient!;
        var alice = _lightningRegtestNetworkFixture.Builder.LNDNodePool!.ReadyNodes
                                                   .First(x => x.LocalAlias == "alice");
        Assert.NotNull(alice);

        await _peerManager.StartAsync(TestContext.Current.CancellationToken);

        // Get the current block height
        var currentHeight = (uint)await bitcoin.GetBlockCountAsync(TestContext.Current.CancellationToken);

        // Start the blockchain monitor at the current height
        await _blockchainMonitor.StartAsync(currentHeight, TestContext.Current.CancellationToken);

        // Fund our wallet
        using (var scope = _serviceProvider.CreateScope())
        {
            var walletService = scope.ServiceProvider
                                     .GetRequiredService<IBitcoinWalletService>();

            // Subscribe to blockchain monitor events
            TaskCompletionSource<bool> tsc = new();
            uint txFirstSeenInBlock = int.MaxValue;

            var address1 = await walletService.GetUnusedAddressAsync(Domain.Bitcoin.Enums.AddressType.P2Wpkh, false);
            var address2 = await walletService.GetUnusedAddressAsync(Domain.Bitcoin.Enums.AddressType.P2Wpkh, true);

            void OnWalletMovementDetected(object? _, WalletMovementEventArgs e)
            {
                if (e.Amount == LightningMoney.Satoshis(1100000) && e.WalletAddress == address1.Address)
                {
                    txFirstSeenInBlock = e.BlockHeight;
                }
                else
                {
                    Assert.Fail("Unexpected wallet movement detected: " +
                                $"Address={e.WalletAddress}, Amount={e.Amount}, TxId={e.TxId}, BlockHeight={e.BlockHeight}");
                }
            }

            void OnNewBlockDetected(object? _, NewBlockEventArgs e)
            {
                if (e.Height >= txFirstSeenInBlock + 5)
                    tsc.TrySetResult(true);
            }

            _blockchainMonitor.OnWalletMovementDetected += OnWalletMovementDetected;
            _blockchainMonitor.OnNewBlockDetected += OnNewBlockDetected;

            // Send funds to our wallet
            await bitcoin.SendToAddressAsync(BitcoinAddress.Create(address1.Address, NBitcoin.Network.RegTest),
                                             new Money(1100000, MoneyUnit.Satoshi),
                                             TestContext.Current.CancellationToken); // 0.0055
            await bitcoin.SendToAddressAsync(BitcoinAddress.Create(address2.Address, NBitcoin.Network.RegTest),
                                             new Money(1100000, MoneyUnit.Satoshi),
                                             TestContext.Current.CancellationToken); // 0.0055

            // Mine blocks to confirm
            await bitcoin.GenerateToAddressAsync(
                6, await bitcoin.GetNewAddressAsync(TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken);

            // wait for funding transaction to be confirmed
            Assert.True(await tsc.Task);
            _blockchainMonitor.OnWalletMovementDetected -= OnWalletMovementDetected;
            _blockchainMonitor.OnNewBlockDetected -= OnNewBlockDetected;
        }

        // Verify we have balance
        var utxoRepository = _serviceProvider.GetRequiredService<IUtxoMemoryRepository>();
        var balance = utxoRepository.GetConfirmedBalance(_blockchainMonitor.LastProcessedBlockHeight);
        Assert.True(balance > LightningMoney.Zero);

        // Connect to Alice
        var aliceHost = new IPEndPoint((await Dns.GetHostAddressesAsync(alice.Host
                                                                             .SplitOnFirst("//")[1]
                                                                             .SplitOnFirst(":")[0],
                                                                        TestContext.Current.CancellationToken)).First(),
                                       9735);
        var aliceAddress = $"{Convert.ToHexString(alice.LocalNodePubKeyBytes)}@{aliceHost}";

        await _peerManager.ConnectToPeerAsync(new PeerAddressInfo(aliceAddress));

        Task<OpenChannelClientResponse> openChannelTask;
        // Open channel - using the client handler
        using (var scope = _serviceProvider.CreateScope())
        {
            var clientHandler = scope.ServiceProvider.GetService(
                                        typeof(IClientCommandHandler<OpenChannelClientRequest,
                                            OpenChannelClientResponse>)) as
                                    OpenChannelClientHandler ??
                                throw new InvalidOperationException(
                                    $"Unable to get service {nameof(OpenChannelClientHandler)}");
            var request = new OpenChannelClientRequest(
                aliceAddress,
                LightningMoney.Satoshis(2100000) // 0.01 BTC,
            )
            {
                FeeRatePerKw = LightningMoney.Satoshis(10000)
            };

            // Act - Open the channel (this should send open_channel and wait for the flow to complete)
            openChannelTask = clientHandler.HandleAsync(request, TestContext.Current.CancellationToken);
        }

        var channelResponse = await openChannelTask;
        Assert.NotNull(channelResponse);

        var channelOpen = false;
        while (!channelOpen)
        {
            Task<OpenChannelClientSubscriptionResponse> openChannelSubscriptionTask;
            // Open channel subscription - using the client handler
            using (var scope = _serviceProvider.CreateScope())
            {
                var subscriptionClientHandler = scope.ServiceProvider.GetService(
                                                        typeof(IClientCommandHandler<
                                                            OpenChannelClientSubscriptionRequest,
                                                            OpenChannelClientSubscriptionResponse>)) as
                                                    OpenChannelClientSubscriptionHandler ??
                                                throw new InvalidOperationException(
                                                    $"Unable to get service {nameof(OpenChannelClientSubscriptionHandler)}");
                var request = new OpenChannelClientSubscriptionRequest(channelResponse.ChannelId);

                // Act - Open the channel (this should send open_channel and wait for the first response)
                openChannelSubscriptionTask =
                    subscriptionClientHandler.HandleAsync(request, TestContext.Current.CancellationToken);
            }

            var channelSubscriptionResponse = await openChannelSubscriptionTask;
            Assert.NotNull(channelSubscriptionResponse);

            if (channelSubscriptionResponse.ChannelState == ChannelState.V1FundingSigned)
            {
                // Mine blocks to confirm
                _ = bitcoin.GenerateToAddressAsync(
                    6, await bitcoin.GetNewAddressAsync(TestContext.Current.CancellationToken),
                    TestContext.Current.CancellationToken);
            }
            else if (channelSubscriptionResponse.ChannelState is ChannelState.ReadyForThem or ChannelState.ReadyForUs
                  or ChannelState.Open)
            {
                channelOpen = true;
            }
        }

        // Check if the channel exists (temporary or permanent)
        var allChannels = _channelMemoryRepository.FindChannels(_ => true);

        Assert.True(allChannels.Count > 0, "Expected at least one channel to be created");
    }

    [Fact]
    public async Task GivenSingleP2TRInput_WhenHandleAsyncIsCalled_ChannelOpensCorrectly()
    {
        // Arrange
        var bitcoin = _lightningRegtestNetworkFixture.Builder!.BitcoinRpcClient!;
        var alice = _lightningRegtestNetworkFixture.Builder.LNDNodePool!.ReadyNodes
                                                   .First(x => x.LocalAlias == "alice");
        Assert.NotNull(alice);

        await _peerManager.StartAsync(TestContext.Current.CancellationToken);

        // Get the current block height
        var currentHeight = (uint)await bitcoin.GetBlockCountAsync(TestContext.Current.CancellationToken);

        // Start the blockchain monitor at the current height
        await _blockchainMonitor.StartAsync(currentHeight, TestContext.Current.CancellationToken);

        // Fund our wallet
        using (var scope = _serviceProvider.CreateScope())
        {
            var walletService = scope.ServiceProvider
                                     .GetRequiredService<IBitcoinWalletService>();
            var address = await walletService.GetUnusedAddressAsync(Domain.Bitcoin.Enums.AddressType.P2Tr, false);

            // Subscribe to blockchain monitor events
            TaskCompletionSource<bool> tsc = new();
            uint txFirstSeenInBlock = int.MaxValue;

            void OnWalletMovementDetected(object? _, WalletMovementEventArgs e)
            {
                if (e.Amount == LightningMoney.FromUnit(1, LightningMoneyUnit.Btc)
                 && e.WalletAddress == address.Address)
                {
                    txFirstSeenInBlock = e.BlockHeight;
                }
                else
                {
                    Assert.Fail("Unexpected wallet movement detected: " +
                                $"Address={e.WalletAddress}, Amount={e.Amount}, TxId={e.TxId}, BlockHeight={e.BlockHeight}");
                }
            }

            void OnNewBlockDetected(object? _, NewBlockEventArgs e)
            {
                if (e.Height >= txFirstSeenInBlock + 5)
                    tsc.TrySetResult(true);
            }

            _blockchainMonitor.OnWalletMovementDetected += OnWalletMovementDetected;
            _blockchainMonitor.OnNewBlockDetected += OnNewBlockDetected;

            // Send funds to our wallet
            await bitcoin.SendToAddressAsync(BitcoinAddress.Create(address.Address, NBitcoin.Network.RegTest),
                                             new Money(1, MoneyUnit.BTC), TestContext.Current.CancellationToken);

            // Mine blocks to confirm
            await bitcoin.GenerateToAddressAsync(
                6, await bitcoin.GetNewAddressAsync(TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken);

            // wait for funding transaction to be confirmed
            Assert.True(await tsc.Task);
            _blockchainMonitor.OnWalletMovementDetected -= OnWalletMovementDetected;
            _blockchainMonitor.OnNewBlockDetected -= OnNewBlockDetected;
        }

        // Verify we have balance
        var utxoRepository = _serviceProvider.GetRequiredService<IUtxoMemoryRepository>();
        var balance = utxoRepository.GetConfirmedBalance(_blockchainMonitor.LastProcessedBlockHeight);
        Assert.True(balance > LightningMoney.Zero);

        // Connect to Alice
        var aliceHost = new IPEndPoint((await Dns.GetHostAddressesAsync(alice.Host
                                                                             .SplitOnFirst("//")[1]
                                                                             .SplitOnFirst(":")[0],
                                                                        TestContext.Current.CancellationToken)).First(),
                                       9735);
        var aliceAddress = $"{Convert.ToHexString(alice.LocalNodePubKeyBytes)}@{aliceHost}";

        await _peerManager.ConnectToPeerAsync(new PeerAddressInfo(aliceAddress));

        Task<OpenChannelClientResponse> openChannelTask;
        // Open channel - using the client handler
        using (var scope = _serviceProvider.CreateScope())
        {
            var clientHandler = scope.ServiceProvider.GetService(
                                        typeof(IClientCommandHandler<OpenChannelClientRequest,
                                            OpenChannelClientResponse>)) as
                                    OpenChannelClientHandler ??
                                throw new InvalidOperationException(
                                    $"Unable to get service {nameof(OpenChannelClientHandler)}");
            var request = new OpenChannelClientRequest(
                aliceAddress,
                LightningMoney.Satoshis(1000000) // 0.01 BTC,
            )
            {
                FeeRatePerKw = LightningMoney.Satoshis(10000)
            };

            // Act - Open the channel (this should send open_channel and wait for the flow to complete)
            openChannelTask = clientHandler.HandleAsync(request, TestContext.Current.CancellationToken);
        }

        var channelResponse = await openChannelTask;
        Assert.NotNull(channelResponse);

        var channelOpen = false;
        while (!channelOpen)
        {
            Task<OpenChannelClientSubscriptionResponse> openChannelSubscriptionTask;
            // Open channel subscription - using the client handler
            using (var scope = _serviceProvider.CreateScope())
            {
                var subscriptionClientHandler = scope.ServiceProvider.GetService(
                                                        typeof(IClientCommandHandler<
                                                            OpenChannelClientSubscriptionRequest,
                                                            OpenChannelClientSubscriptionResponse>)) as
                                                    OpenChannelClientSubscriptionHandler ??
                                                throw new InvalidOperationException(
                                                    $"Unable to get service {nameof(OpenChannelClientSubscriptionHandler)}");
                var request = new OpenChannelClientSubscriptionRequest(channelResponse.ChannelId);

                // Act - Open the channel (this should send open_channel and wait for the first response)
                openChannelSubscriptionTask =
                    subscriptionClientHandler.HandleAsync(request, TestContext.Current.CancellationToken);
            }

            var channelSubscriptionResponse = await openChannelSubscriptionTask;
            Assert.NotNull(channelSubscriptionResponse);

            if (channelSubscriptionResponse.ChannelState == ChannelState.V1FundingSigned)
            {
                // Mine blocks to confirm
                _ = bitcoin.GenerateToAddressAsync(
                    6, await bitcoin.GetNewAddressAsync(TestContext.Current.CancellationToken),
                    TestContext.Current.CancellationToken);
            }
            else if (channelSubscriptionResponse.ChannelState is ChannelState.ReadyForThem or ChannelState.ReadyForUs
                  or ChannelState.Open)
            {
                channelOpen = true;
            }
        }

        // Check if the channel exists (temporary or permanent)
        var allChannels = _channelMemoryRepository.FindChannels(_ => true);

        Assert.True(allChannels.Count > 0, "Expected at least one channel to be created");
    }

    [Fact]
    public async Task GivenMultipleP2TRInput_WhenHandleAsyncIsCalled_ChannelOpensCorrectly()
    {
        // Arrange
        var bitcoin = _lightningRegtestNetworkFixture.Builder!.BitcoinRpcClient!;
        var alice = _lightningRegtestNetworkFixture.Builder.LNDNodePool!.ReadyNodes
                                                   .First(x => x.LocalAlias == "alice");
        Assert.NotNull(alice);

        await _peerManager.StartAsync(TestContext.Current.CancellationToken);

        // Get the current block height
        var currentHeight = (uint)await bitcoin.GetBlockCountAsync(TestContext.Current.CancellationToken);

        // Start the blockchain monitor at the current height
        await _blockchainMonitor.StartAsync(currentHeight, TestContext.Current.CancellationToken);

        // Fund our wallet
        using (var scope = _serviceProvider.CreateScope())
        {
            var walletService = scope.ServiceProvider
                                     .GetRequiredService<IBitcoinWalletService>();

            // Subscribe to blockchain monitor events
            TaskCompletionSource<bool> tsc = new();
            uint txFirstSeenInBlock = int.MaxValue;

            var address1 = await walletService.GetUnusedAddressAsync(Domain.Bitcoin.Enums.AddressType.P2Tr, false);
            var address2 = await walletService.GetUnusedAddressAsync(Domain.Bitcoin.Enums.AddressType.P2Tr, true);

            void OnWalletMovementDetected(object? _, WalletMovementEventArgs e)
            {
                if (e.Amount == LightningMoney.Satoshis(1100000) && e.WalletAddress == address1.Address)
                {
                    txFirstSeenInBlock = e.BlockHeight;
                }
                else
                {
                    Assert.Fail("Unexpected wallet movement detected: " +
                                $"Address={e.WalletAddress}, Amount={e.Amount}, TxId={e.TxId}, BlockHeight={e.BlockHeight}");
                }
            }

            void OnNewBlockDetected(object? _, NewBlockEventArgs e)
            {
                if (e.Height >= txFirstSeenInBlock + 5)
                    tsc.TrySetResult(true);
            }

            _blockchainMonitor.OnWalletMovementDetected += OnWalletMovementDetected;
            _blockchainMonitor.OnNewBlockDetected += OnNewBlockDetected;

            // Send funds to our wallet
            await bitcoin.SendToAddressAsync(BitcoinAddress.Create(address1.Address, NBitcoin.Network.RegTest),
                                             new Money(1100000, MoneyUnit.Satoshi),
                                             TestContext.Current.CancellationToken); // 0.011
            await bitcoin.SendToAddressAsync(BitcoinAddress.Create(address2.Address, NBitcoin.Network.RegTest),
                                             new Money(1100000, MoneyUnit.Satoshi),
                                             TestContext.Current.CancellationToken); // 0.011

            // Mine blocks to confirm
            await bitcoin.GenerateToAddressAsync(
                6, await bitcoin.GetNewAddressAsync(TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken);

            // wait for funding transaction to be confirmed
            Assert.True(await tsc.Task);
            _blockchainMonitor.OnWalletMovementDetected -= OnWalletMovementDetected;
            _blockchainMonitor.OnNewBlockDetected -= OnNewBlockDetected;
        }

        // Verify we have balance
        var utxoRepository = _serviceProvider.GetRequiredService<IUtxoMemoryRepository>();
        var balance = utxoRepository.GetConfirmedBalance(_blockchainMonitor.LastProcessedBlockHeight);
        Assert.True(balance > LightningMoney.Zero);

        // Connect to Alice
        var aliceHost = new IPEndPoint((await Dns.GetHostAddressesAsync(alice.Host
                                                                             .SplitOnFirst("//")[1]
                                                                             .SplitOnFirst(":")[0],
                                                                        TestContext.Current.CancellationToken)).First(),
                                       9735);
        var aliceAddress = $"{Convert.ToHexString(alice.LocalNodePubKeyBytes)}@{aliceHost}";

        await _peerManager.ConnectToPeerAsync(new PeerAddressInfo(aliceAddress));

        Task<OpenChannelClientResponse> openChannelTask;
        // Open channel - using the client handler
        using (var scope = _serviceProvider.CreateScope())
        {
            var clientHandler = scope.ServiceProvider.GetService(
                                        typeof(IClientCommandHandler<OpenChannelClientRequest,
                                            OpenChannelClientResponse>)) as
                                    OpenChannelClientHandler ??
                                throw new InvalidOperationException(
                                    $"Unable to get service {nameof(OpenChannelClientHandler)}");
            var request = new OpenChannelClientRequest(
                aliceAddress,
                LightningMoney.Satoshis(2000000) // 0.02 BTC,
            )
            {
                FeeRatePerKw = LightningMoney.Satoshis(10000)
            };

            // Act - Open the channel (this should send open_channel and wait for the flow to complete)
            openChannelTask = clientHandler.HandleAsync(request, TestContext.Current.CancellationToken);
        }

        var channelResponse = await openChannelTask;
        Assert.NotNull(channelResponse);

        var channelOpen = false;
        while (!channelOpen)
        {
            Task<OpenChannelClientSubscriptionResponse> openChannelSubscriptionTask;
            // Open channel subscription - using the client handler
            using (var scope = _serviceProvider.CreateScope())
            {
                var subscriptionClientHandler = scope.ServiceProvider.GetService(
                                                        typeof(IClientCommandHandler<
                                                            OpenChannelClientSubscriptionRequest,
                                                            OpenChannelClientSubscriptionResponse>)) as
                                                    OpenChannelClientSubscriptionHandler ??
                                                throw new InvalidOperationException(
                                                    $"Unable to get service {nameof(OpenChannelClientSubscriptionHandler)}");
                var request = new OpenChannelClientSubscriptionRequest(channelResponse.ChannelId);

                // Act - Open the channel (this should send open_channel and wait for the first response)
                openChannelSubscriptionTask =
                    subscriptionClientHandler.HandleAsync(request, TestContext.Current.CancellationToken);
            }

            var channelSubscriptionResponse = await openChannelSubscriptionTask;
            Assert.NotNull(channelSubscriptionResponse);

            if (channelSubscriptionResponse.ChannelState == ChannelState.V1FundingSigned)
            {
                // Mine blocks to confirm
                _ = bitcoin.GenerateToAddressAsync(
                    6, await bitcoin.GetNewAddressAsync(TestContext.Current.CancellationToken),
                    TestContext.Current.CancellationToken);
            }
            else if (channelSubscriptionResponse.ChannelState is ChannelState.ReadyForThem or ChannelState.ReadyForUs
                  or ChannelState.Open)
            {
                channelOpen = true;
            }
        }

        // Check if the channel exists (temporary or permanent)
        var allChannels = _channelMemoryRepository.FindChannels(_ => true);

        Assert.True(allChannels.Count > 0, "Expected at least one channel to be created");
    }

    [Fact]
    public async Task GivenMixedInput_WhenHandleAsyncIsCalled_ChannelOpensCorrectly()
    {
        // Arrange
        var bitcoin = _lightningRegtestNetworkFixture.Builder!.BitcoinRpcClient!;
        var alice = _lightningRegtestNetworkFixture.Builder.LNDNodePool!.ReadyNodes
                                                   .First(x => x.LocalAlias == "alice");
        Assert.NotNull(alice);

        await _peerManager.StartAsync(TestContext.Current.CancellationToken);

        // Get the current block height
        var currentHeight = (uint)await bitcoin.GetBlockCountAsync(TestContext.Current.CancellationToken);

        // Start the blockchain monitor at the current height
        await _blockchainMonitor.StartAsync(currentHeight, TestContext.Current.CancellationToken);

        // Fund our wallet
        using (var scope = _serviceProvider.CreateScope())
        {
            var walletService = scope.ServiceProvider
                                     .GetRequiredService<IBitcoinWalletService>();

            // Subscribe to blockchain monitor events
            TaskCompletionSource<bool> tsc = new();
            uint txFirstSeenInBlock = int.MaxValue;

            var address1 = await walletService.GetUnusedAddressAsync(Domain.Bitcoin.Enums.AddressType.P2Wpkh, false);
            var address2 = await walletService.GetUnusedAddressAsync(Domain.Bitcoin.Enums.AddressType.P2Tr, false);

            void OnWalletMovementDetected(object? _, WalletMovementEventArgs e)
            {
                if (e.Amount == LightningMoney.Satoshis(1100000) && e.WalletAddress == address1.Address)
                {
                    txFirstSeenInBlock = e.BlockHeight;
                }
                else
                {
                    Assert.Fail("Unexpected wallet movement detected: " +
                                $"Address={e.WalletAddress}, Amount={e.Amount}, TxId={e.TxId}, BlockHeight={e.BlockHeight}");
                }
            }

            void OnNewBlockDetected(object? _, NewBlockEventArgs e)
            {
                if (e.Height >= txFirstSeenInBlock + 5)
                    tsc.TrySetResult(true);
            }

            _blockchainMonitor.OnWalletMovementDetected += OnWalletMovementDetected;
            _blockchainMonitor.OnNewBlockDetected += OnNewBlockDetected;

            // Send funds to our wallet
            await bitcoin.SendToAddressAsync(BitcoinAddress.Create(address1.Address, NBitcoin.Network.RegTest),
                                             new Money(1100000, MoneyUnit.Satoshi),
                                             TestContext.Current.CancellationToken); // 0.011
            await bitcoin.SendToAddressAsync(BitcoinAddress.Create(address2.Address, NBitcoin.Network.RegTest),
                                             new Money(1100000, MoneyUnit.Satoshi),
                                             TestContext.Current.CancellationToken); // 0.011

            // Mine blocks to confirm
            await bitcoin.GenerateToAddressAsync(
                6, await bitcoin.GetNewAddressAsync(TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken);

            // wait for funding transaction to be confirmed
            Assert.True(await tsc.Task);
            _blockchainMonitor.OnWalletMovementDetected -= OnWalletMovementDetected;
            _blockchainMonitor.OnNewBlockDetected -= OnNewBlockDetected;
        }

        // Verify we have balance
        var utxoRepository = _serviceProvider.GetRequiredService<IUtxoMemoryRepository>();
        var balance = utxoRepository.GetConfirmedBalance(_blockchainMonitor.LastProcessedBlockHeight);
        Assert.True(balance > LightningMoney.Zero);

        // Connect to Alice
        var aliceHost = new IPEndPoint((await Dns.GetHostAddressesAsync(alice.Host
                                                                             .SplitOnFirst("//")[1]
                                                                             .SplitOnFirst(":")[0],
                                                                        TestContext.Current.CancellationToken)).First(),
                                       9735);
        var aliceAddress = $"{Convert.ToHexString(alice.LocalNodePubKeyBytes)}@{aliceHost}";

        await _peerManager.ConnectToPeerAsync(new PeerAddressInfo(aliceAddress));

        Task<OpenChannelClientResponse> openChannelTask;
        // Open channel - using the client handler
        using (var scope = _serviceProvider.CreateScope())
        {
            var clientHandler = scope.ServiceProvider.GetService(
                                        typeof(IClientCommandHandler<OpenChannelClientRequest,
                                            OpenChannelClientResponse>)) as
                                    OpenChannelClientHandler ??
                                throw new InvalidOperationException(
                                    $"Unable to get service {nameof(OpenChannelClientHandler)}");
            var request = new OpenChannelClientRequest(
                aliceAddress,
                LightningMoney.Satoshis(2000000) // 0.02 BTC,
            )
            {
                FeeRatePerKw = LightningMoney.Satoshis(10000)
            };

            // Act - Open the channel (this should send open_channel and wait for the flow to complete)
            openChannelTask = clientHandler.HandleAsync(request, TestContext.Current.CancellationToken);
        }

        var channelResponse = await openChannelTask;
        Assert.NotNull(channelResponse);

        var channelOpen = false;
        while (!channelOpen)
        {
            Task<OpenChannelClientSubscriptionResponse> openChannelSubscriptionTask;
            // Open channel subscription - using the client handler
            using (var scope = _serviceProvider.CreateScope())
            {
                var subscriptionClientHandler = scope.ServiceProvider.GetService(
                                                        typeof(IClientCommandHandler<
                                                            OpenChannelClientSubscriptionRequest,
                                                            OpenChannelClientSubscriptionResponse>)) as
                                                    OpenChannelClientSubscriptionHandler ??
                                                throw new InvalidOperationException(
                                                    $"Unable to get service {nameof(OpenChannelClientSubscriptionHandler)}");
                var request = new OpenChannelClientSubscriptionRequest(channelResponse.ChannelId);

                // Act - Open the channel (this should send open_channel and wait for the first response)
                openChannelSubscriptionTask =
                    subscriptionClientHandler.HandleAsync(request, TestContext.Current.CancellationToken);
            }

            var channelSubscriptionResponse = await openChannelSubscriptionTask;
            Assert.NotNull(channelSubscriptionResponse);

            if (channelSubscriptionResponse.ChannelState == ChannelState.V1FundingSigned)
            {
                // Mine blocks to confirm
                _ = bitcoin.GenerateToAddressAsync(
                    6, await bitcoin.GetNewAddressAsync(TestContext.Current.CancellationToken),
                    TestContext.Current.CancellationToken);
            }
            else if (channelSubscriptionResponse.ChannelState is ChannelState.ReadyForThem or ChannelState.ReadyForUs
                  or ChannelState.Open)
            {
                channelOpen = true;
            }
        }

        // Check if the channel exists (temporary or permanent)
        var allChannels = _channelMemoryRepository.FindChannels(_ => true);

        Assert.True(allChannels.Count > 0, "Expected at least one channel to be created");
    }

    public void Dispose()
    {
        _blockchainMonitor.StopAsync().GetAwaiter().GetResult();
        PortPoolUtil.ReleasePort(_port);
        if (File.Exists(_databaseFilePath))
        {
            try
            {
                File.Delete(_databaseFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete database file: {ex.Message}");
            }
        }
    }
}