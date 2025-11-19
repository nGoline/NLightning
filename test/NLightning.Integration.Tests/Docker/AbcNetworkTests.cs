using System.Collections.Immutable;
using System.Net;
using System.Reflection;
using Lnrpc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq.Protected;
using NLightning.Domain.Crypto.ValueObjects;
using NLightning.Domain.Node.Models;
using NLightning.Infrastructure.Persistence.Contexts;
using NLightning.Tests.Utils;
using ServiceStack;
using ServiceStack.Text;

namespace NLightning.Integration.Tests.Docker;

using Application;
using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.Transactions.Factories;
using Domain.Bitcoin.Transactions.Interfaces;
using Domain.Channels.Factories;
using Domain.Channels.Interfaces;
using Domain.Crypto.Hashes;
using Domain.Enums;
using Domain.Node.Interfaces;
using Domain.Node.Options;
using Domain.Node.ValueObjects;
using Domain.Protocol.Constants;
using Domain.Protocol.Interfaces;
using Domain.Protocol.ValueObjects;
using Fixtures;
using Infrastructure;
using Infrastructure.Bitcoin;
using Infrastructure.Bitcoin.Builders;
using Infrastructure.Bitcoin.Options;
using Infrastructure.Bitcoin.Signers;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Serialization;
using Mock;
using TestCollections;
using Utils;

// ReSharper disable AccessToDisposedClosure
#pragma warning disable xUnit1033 // Test classes decorated with 'Xunit.IClassFixture<TFixture>' or 'Xunit.ICollectionFixture<TFixture>' should add a constructor argument of type TFixture
[Collection(LightningRegtestNetworkFixtureCollection.Name)]
public class AbcNetworkTests : IDisposable
{
    private readonly LightningRegtestNetworkFixture _lightningRegtestNetworkFixture;
    private readonly IPeerManager _peerManager;
    private readonly int _port;
    private readonly ISecureKeyManager _secureKeyManager;
    private readonly string _databaseFilePath = $"nlightning_{Guid.NewGuid()}.db";

    public AbcNetworkTests(LightningRegtestNetworkFixture fixture, ITestOutputHelper output)
    {
        _lightningRegtestNetworkFixture = fixture;
        Console.SetOut(new TestOutputWriter(output));

        _port = PortPoolUtil.GetAvailablePortAsync().GetAwaiter().GetResult();
        Assert.True(_port > 0);
        _secureKeyManager = new FakeSecureKeyManager();

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
            new("Node:Network", "regtest"),
            new("Node:Daemon", "false"),
            new("Database:Provider", "Sqlite"),
            new("Database:ConnectionString", $"Data Source={_databaseFilePath}"),
            new("Bitcoin:RpcEndpoint", bitcoinEndpoint),
            new("Bitcoin:RpcUser", bitcoin.CredentialString.UserPassword.UserName),
            new("Bitcoin:RpcPassword", bitcoin.CredentialString.UserPassword.Password),
            new("Bitcoin:ZmqHost", bitcoinEndpoint),
            new("Bitcoin:ZmqBlockPort", zmqRawBlockPort),
            new("Bitcoin:ZmqTxPort", zmqRawTxPort)
        ];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfiguration).Build();

        // Create a service collection
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpClient("TestClient").ConfigurePrimaryHttpMessageHandler(() => httpMessageHandlerMock.Object);
        services.AddSingleton(_secureKeyManager);
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

            // Create the signer with the correct network
            return new LocalLightningSigner(fundingOutputBuilder, keyDerivationService, logger, nodeOptions,
                                            _secureKeyManager, utxoMemoryRepository);
        });
        services.AddApplicationServices();
        services.AddInfrastructureServices();
        services.AddPersistenceInfrastructureServices(configuration);
        services.AddRepositoriesInfrastructureServices();
        services.AddSerializationInfrastructureServices();
        services.AddBitcoinInfrastructure();
        services.AddOptions<BitcoinOptions>().BindConfiguration("Bitcoin").ValidateOnStart();
        services.AddOptions<FeeEstimationOptions>().BindConfiguration("FeeEstimation").ValidateOnStart();
        services.AddOptions<NodeOptions>()
                .BindConfiguration("Node")
                .PostConfigure(options =>
                 {
                     options.Features = new FeatureOptions
                     {
                         ChainHashes = [ChainConstants.Regtest],
                         DataLossProtect = FeatureSupport.Optional,
                         StaticRemoteKey = FeatureSupport.Optional,
                         PaymentSecret = FeatureSupport.Optional
                     };
                     options.ListenAddresses = [$"{IPAddress.Loopback}:{_port}"];
                     options.BitcoinNetwork = BitcoinNetwork.Regtest;
                     options.Features.ChainHashes = [options.BitcoinNetwork.ChainHash];
                 })
                .ValidateOnStart();

        // Set up factories
        var serviceProvider = services.BuildServiceProvider();

        // Set up the database migration
        var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NLightningDbContext>();
        var pendingMigrations = context.Database.GetPendingMigrationsAsync().GetAwaiter().GetResult().ToList();
        if (pendingMigrations.Count > 0)
            context.Database.Migrate();

        // Set up the peer manager
        _peerManager = serviceProvider.GetRequiredService<IPeerManager>();
        _peerManager.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task NLightning_BOLT8_Test_Connect_Alice()
    {
        // Arrange 
        var hex = Convert.ToHexString(_secureKeyManager.GetNodePubKey());

        var alice =
            _lightningRegtestNetworkFixture.Builder?.LNDNodePool?.ReadyNodes.First(x => x.LocalAlias == "alice");
        Assert.NotNull(alice);

        var aliceHost = new IPEndPoint((await Dns.GetHostAddressesAsync(alice.Host
                                                                             .SplitOnFirst("//")[1]
                                                                             .SplitOnFirst(":")[0],
                                                                        TestContext.Current.CancellationToken)).First(),
                                       9735);

        // Act
        await _peerManager.ConnectToPeerAsync(
            new PeerAddressInfo(
                $"{Convert.ToHexString(alice.LocalNodePubKeyBytes)}@{aliceHost.Address}:{aliceHost.Port}"));
        var alicePeers =
            alice.LightningClient.ListPeers(new ListPeersRequest(),
                                            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(alicePeers.Peers.FirstOrDefault(x => x.PubKey
                                                             .Equals(hex, StringComparison.CurrentCultureIgnoreCase)));

        // Cleanup
        _peerManager.DisconnectPeer(alice.LocalNodePubKeyBytes);
    }

    [Fact]
    public async Task NLightning_BOLT8_Test_Bob_Connect()
    {
        // Arrange
        var hostAddress = Environment.GetEnvironmentVariable("HOST_ADDRESS") ?? "host.docker.internal";
        var hex = Convert.ToHexString(_secureKeyManager.GetNodePubKey());

        var bob = _lightningRegtestNetworkFixture.Builder?.LNDNodePool?.ReadyNodes
                                                 .First(x => x.LocalAlias == "bob");
        Assert.NotNull(bob);
        var taskCompletionSource = new TaskCompletionSource<bool>();
        var field = _peerManager.GetType().GetField("_peers", BindingFlags.NonPublic | BindingFlags.Instance);
        var peers = (Dictionary<CompactPubKey, PeerModel>)field!.GetValue(_peerManager)!;
        _ = Task.Run(() =>
        {
            while (peers.Count == 0)
                Thread.Sleep(50);

            taskCompletionSource.SetResult(peers.TryGetValue(bob.LocalNodePubKeyBytes, out _));
        }, new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);

        // Act
        await bob.LightningClient.ConnectPeerAsync(new ConnectPeerRequest
        {
            Addr = new LightningAddress
            {
                Host = $"{hostAddress}:{_port}",
                Pubkey = hex
            }
        }, cancellationToken: TestContext.Current.CancellationToken);
        var bobPeers = bob.LightningClient.ListPeers(new ListPeersRequest(),
                                                     cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(await taskCompletionSource.Task);
        Assert.NotNull(
            bobPeers.Peers.FirstOrDefault(x => x.PubKey.Equals(hex, StringComparison.CurrentCultureIgnoreCase)));

        // Cleanup
        _peerManager.DisconnectPeer(bob.LocalNodePubKeyBytes);
    }

    [Fact]
    public async Task Verify_Alice_Bob_Carol_Setup()
    {
        var readyNodes = _lightningRegtestNetworkFixture.Builder!.LNDNodePool!.ReadyNodes.ToImmutableList();
        var nodeCount = readyNodes.Count;
        Assert.Equal(3, nodeCount);
        $"LND Nodes in Ready State: {nodeCount}".Print();
        foreach (var node in readyNodes)
        {
            var walletBalanceResponse =
                await node.LightningClient.WalletBalanceAsync(new WalletBalanceRequest(),
                                                              cancellationToken: TestContext.Current.CancellationToken);
            var channels =
                await node.LightningClient.ListChannelsAsync(new ListChannelsRequest(),
                                                             cancellationToken: TestContext.Current.CancellationToken);
            $"Node {node.LocalAlias} ({node.LocalNodePubKey})".Print();
            walletBalanceResponse.PrintDump();
            channels.PrintDump();
        }

        $"Bitcoin Node Balance: {(await _lightningRegtestNetworkFixture.Builder!.BitcoinRpcClient!.GetBalanceAsync()).Satoshi / 1e8}"
           .Print();
    }

    public void Dispose()
    {
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
#pragma warning restore xUnit1033 // Test classes decorated with 'Xunit.IClassFixture<TFixture>' or 'Xunit.ICollectionFixture<TFixture>' should add a constructor argument of type TFixture
// ReSharper restore AccessToDisposedClosure