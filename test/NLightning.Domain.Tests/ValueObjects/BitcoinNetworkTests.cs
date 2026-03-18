namespace NLightning.Domain.Tests.ValueObjects;

using Domain.Protocol.Constants;
using Domain.Protocol.ValueObjects;

public class BitcoinNetworkTests
{
    [Fact]
    public void Given_NetworkInstances_When_ComparedForEquality_Then_ReturnsCorrectResult()
    {
        // Given
        var mainNet1 = BitcoinNetwork.Mainnet;
        var mainNet2 = new BitcoinNetwork("mainnet");
        var testNet = BitcoinNetwork.Testnet;

        // When & Then
        Assert.True(mainNet1 == mainNet2);
        Assert.False(mainNet1 == testNet);
        Assert.True(mainNet1.Equals(mainNet2));
        Assert.False(mainNet1.Equals(testNet));
    }

    [Fact]
    public void Given_NetworkInstance_When_ConvertedToString_Then_ReturnsCorrectName()
    {
        // Given
        var network = BitcoinNetwork.Mainnet;

        // When
        string networkName = network;

        // Then
        Assert.Equal("mainnet", networkName);
    }

    [Fact]
    public void Given_String_When_ConvertedToNetwork_Then_ReturnsCorrectNetwork()
    {
        // Given
        const string networkName = "testnet";

        // When
        BitcoinNetwork bitcoinNetwork = networkName;

        // Then
        Assert.Equal(BitcoinNetwork.Testnet, bitcoinNetwork);
    }

    [Theory]
    [InlineData(NetworkConstants.Mainnet)]
    [InlineData(NetworkConstants.Testnet)]
    [InlineData(NetworkConstants.Regtest)]
    public void Given_NetworkInstance_When_ChainHashAccessed_Then_ReturnsCorrectHash(string networkName)
    {
        // Given
        var network = new BitcoinNetwork(networkName);
        var expectedChain = networkName switch
        {
            NetworkConstants.Mainnet => ChainConstants.Main,
            NetworkConstants.Testnet => ChainConstants.Testnet,
            NetworkConstants.Regtest => ChainConstants.Regtest,
            _ => throw new InvalidOperationException("Chain not supported.")
        };

        // When
        var chainHash = network.ChainHash;

        // Then
        Assert.Equal(expectedChain, chainHash);
    }

    [Fact]
    public void Given_UnsupportedNetworkName_When_ChainHashAccessed_Then_ThrowsException()
    {
        // Given
        var network = new BitcoinNetwork("unsupported");

        // When & Then
        Assert.Throws<InvalidOperationException>(() => network.ChainHash);
    }

    [Fact]
    public void Given_NetworkInstance_When_ToStringCalled_Then_ReturnsCorrectName()
    {
        // Given
        var network = BitcoinNetwork.Mainnet;

        // When
        var networkName = network.ToString();

        // Then
        Assert.Equal("mainnet", networkName);
    }

    [Fact]
    public void Given_TwoEqualNetworkInstances_When_Compared_Then_AreEqual()
    {
        // Given
        var network1 = new BitcoinNetwork("mainnet");
        var network2 = BitcoinNetwork.Mainnet;

        // When & Then
        Assert.True(network1 == network2);
        Assert.False(network1 != network2);
        Assert.True(network1.Equals(network2));
        Assert.True(network2.Equals(network1));
    }

    [Fact]
    public void Given_TwoDifferentNetworkInstances_When_Compared_Then_AreNotEqual()
    {
        // Given
        var network1 = BitcoinNetwork.Mainnet;
        var network2 = BitcoinNetwork.Testnet;

        // When & Then
        Assert.False(network1 == network2);
        Assert.True(network1 != network2);
        Assert.False(network1.Equals(network2));
        Assert.False(network2.Equals(network1));
    }

    [Fact]
    public void Builtin_Networks_ChainHash_Matches()
    {
        Assert.Equal(ChainConstants.Main, BitcoinNetwork.Mainnet.ChainHash);
        Assert.Equal(ChainConstants.Testnet, BitcoinNetwork.Testnet.ChainHash);
        Assert.Equal(ChainConstants.Regtest, BitcoinNetwork.Regtest.ChainHash);
    }

    [Fact]
    public void Unregistered_CustomNetwork_Throws()
    {
        var net = new BitcoinNetwork("myinvelidnet");
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = net.ChainHash;
        });
    }

    [Fact]
    public void RegisterCustomNetwork_ReturnsExpectedChainHash()
    {
        var customChainHash = DummyChainHash(0x42);
        // Register
        var tmpNet = new BitcoinNetwork("mycustomnet");
        tmpNet.Register("mycustomnet", customChainHash);

        var net = new BitcoinNetwork("mycustomnet");
        Assert.Equal(customChainHash, net.ChainHash);
    }

    [Fact]
    public void Register_IsCaseInsensitive()
    {
        var customChainHash = DummyChainHash(0xDD);
        var lower = "lowercase";
        var upper = "LOWERCASE";

        var tmpNet = new BitcoinNetwork(lower);
        tmpNet.Register(upper, customChainHash);

        var net1 = new BitcoinNetwork(lower);
        var net2 = new BitcoinNetwork(upper);
        Assert.Equal(customChainHash, net1.ChainHash);
        Assert.Equal(customChainHash, net2.ChainHash);
    }

    [Fact]
    public void Unregister_RemovesChainHash()
    {
        var customChainHash = DummyChainHash(0xAB);
        var name = "toRemove";
        var net = new BitcoinNetwork(name);
        net.Register(name, customChainHash);

        var useNet = new BitcoinNetwork(name);
        Assert.Equal(customChainHash, useNet.ChainHash);

        BitcoinNetwork.Unregister(name);

        var afterRemove = new BitcoinNetwork(name);
        Assert.Throws<InvalidOperationException>(() =>
        {
            var _ = afterRemove.ChainHash;
        });
    }

    [Fact]
    public void ToString_And_Conversions_Work_For_Custom()
    {
        var customChainHash = DummyChainHash(0x5A);
        var name = "foo_bar";
        var bnet = new BitcoinNetwork(name);
        bnet.Register(name, customChainHash);

        Assert.Equal(name, bnet.ToString());

        string str = bnet;
        Assert.Equal(name.ToLowerInvariant(), str);

        BitcoinNetwork net2 = name;
        Assert.Equal(name, net2.Name);
        Assert.Equal(customChainHash, net2.ChainHash);
    }

    [Fact]
    public void Register_Throws_For_AlreadyExistingCustomNetwork()
    {
        var chainHash1 = DummyChainHash(0x13);
        var chainHash2 = DummyChainHash(0x53);
        var name = "networkdup";

        // Ensure clean slate
        BitcoinNetwork.Unregister(name);

        var net = new BitcoinNetwork(name);
        net.Register(name, chainHash1);

        // Attempting to add again (any value) must throw
        var ex = Assert.Throws<InvalidOperationException>(() => net.Register(name, chainHash2));
        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Clean up for other tests
        BitcoinNetwork.Unregister(name);
    }

    [Fact]
    public void Register_Throws_For_Null_Or_Whitespace_Name()
    {
        var net = new BitcoinNetwork("foo");
        var ch = DummyChainHash(0x77);

        Assert.Throws<ArgumentNullException>(() => net.Register(null!, ch));
        Assert.Throws<ArgumentNullException>(() => net.Register("", ch));
        Assert.Throws<ArgumentNullException>(() => net.Register("   ", ch));
    }

    private static ChainHash DummyChainHash(byte fill)
    {
        // Create a 32-byte chainhash with a pattern to differentiate
        return new ChainHash(new[]
        {
            fill, fill, fill, fill, fill, fill, fill, fill, fill, fill, fill, fill, fill, fill,
            fill, fill,
            fill, fill, fill, fill, fill, fill, fill, fill, fill, fill, fill, fill, fill, fill,
            fill, fill
        });
    }
}