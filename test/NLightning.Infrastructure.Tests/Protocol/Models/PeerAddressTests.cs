using System.Net;
using NBitcoin;
using NLightning.Domain.Crypto.ValueObjects;

namespace NLightning.Infrastructure.Tests.Protocol.Models;

using Infrastructure.Protocol.Models;

public class PeerAddressTests
{
    [Fact]
    public void Given_SingleStringAddress_When_ConstructingPeerAddress_Then_PropertiesAreCorrectlyInitialized()
    {
        // Arrange
        const string address = "028d7500dd4c12685d1f568b4c2b5048e8534b873319f3a8daa612b469132ec7f7@127.0.0.1:8080";

        // Act
        var peerAddress = new PeerAddress(address);

        // Assert
        Assert.Equal("028d7500dd4c12685d1f568b4c2b5048e8534b873319f3a8daa612b469132ec7f7",
                     peerAddress.PubKey.ToString());
        Assert.Equal(IPAddress.Parse("127.0.0.1"), peerAddress.Host);
        Assert.Equal(8080, peerAddress.Port);
    }

    [Fact]
    public void Given_HttpAddress_When_ConstructingPeerAddress_Then_HostAndPortAreCorrectlyResolved()
    {
        // Arrange
        CompactPubKey pubKey =
            Convert.FromHexString("028d7500dd4c12685d1f568b4c2b5048e8534b873319f3a8daa612b469132ec7f7");
        const string address = "http://dnstest.nlightn.ing:8080/";

        // Act
        var peerAddress = new PeerAddress(pubKey, address);

        // Assert
        Assert.Equal(pubKey, peerAddress.PubKey);
        Assert.Equal(peerAddress.Host.IsIPv4()
                         ? IPAddress.Parse("127.0.0.1")
                         : IPAddress.Parse("0000:0000:0000:0000:0000:0000:0000:0001"),
                     peerAddress.Host);
        Assert.Equal(8080, peerAddress.Port);
    }

    [Fact]
    public void Given_PubKeyHostAndPort_When_ConstructingPeerAddress_Then_PropertiesAreCorrectlyInitialized()
    {
        // Arrange
        CompactPubKey pubKey =
            Convert.FromHexString("028d7500dd4c12685d1f568b4c2b5048e8534b873319f3a8daa612b469132ec7f7");
        const string host = "127.0.0.1";
        const int port = 8080;

        // Act
        var peerAddress = new PeerAddress(pubKey, host, port);

        // Assert
        Assert.Equal(pubKey, peerAddress.PubKey);
        Assert.Equal(IPAddress.Parse(host), peerAddress.Host);
        Assert.Equal(port, peerAddress.Port);
    }

    [Fact]
    public void Given_PeerAddressInstance_When_CallingToString_Then_ReturnsExpectedFormat()
    {
        // Arrange
        CompactPubKey pubKey =
            Convert.FromHexString("028d7500dd4c12685d1f568b4c2b5048e8534b873319f3a8daa612b469132ec7f7");
        const string host = "127.0.0.1";
        const int port = 8080;
        var peerAddress = new PeerAddress(pubKey, host, port);

        // Act
        var result = peerAddress.ToString();

        // Assert
        Assert.Equal("028d7500dd4c12685d1f568b4c2b5048e8534b873319f3a8daa612b469132ec7f7@127.0.0.1:8080", result);
    }
}