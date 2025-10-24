using System.Net;
using System.Text.RegularExpressions;

namespace NLightning.Infrastructure.Protocol.Models;

using NLightning.Domain.Crypto.ValueObjects;
using NLightning.Domain.Node.ValueObjects;

/// <summary>
/// Represents a peer address.
/// </summary>
public sealed partial class PeerAddress : IEquatable<PeerAddress>
{
    [GeneratedRegex(@"\d+")]
    private static partial Regex OnlyDigitsRegex();

    /// <summary>
    /// Gets the public key.
    /// </summary>
    public CompactPubKey PubKey { get; }

    /// <summary>
    /// Gets the host.
    /// </summary>
    public IPAddress Host { get; }

    /// <summary>
    /// Gets the port.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PeerAddress"/> class.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <remarks>
    /// The address is in the format of "pubkey@host:port".
    /// </remarks>
    public PeerAddress(string address)
    {
        var parts = address.Split('@');
        if (parts.Length != 2)
            throw new FormatException("Invalid address format, should be pubkey@host:port");

        PubKey = new CompactPubKey(Convert.FromHexString(parts[0]));

        // Check if the address starts with http
        if (parts[1].StartsWith("http"))
        {
            // split on first // to get the address
            var hostPort = parts[1].Split("//")[1].Split(":");
            Host = Dns.GetHostAddresses(hostPort[0])[0];

            // Port may have an extra / at the end. Use regex to keep only the number in the port
            Port = int.Parse(OnlyDigitsRegex().Match(hostPort[1]).Value);
        }
        else
        {
            var hostPort = parts[1].Split(':');
            Host = IPAddress.Parse(hostPort[0]);
            Port = int.Parse(hostPort[1]);
        }
    }

    public PeerAddress(PeerAddressInfo peerAddressInfo) : this(peerAddressInfo.Address)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PeerAddress"/> class.
    /// </summary>
    /// <param name="pubKey">The public key.</param>
    /// <param name="address">The address.</param>
    /// <remarks>
    /// The address is in the format of "http://host:port" or "host:port".
    /// </remarks>
    public PeerAddress(CompactPubKey pubKey, string address)
    {
        PubKey = pubKey;

        // Check if the address starts with http
        if (address.StartsWith("http"))
        {
            // split on first // to get the address
            var host = address.Split("//")[1];
            Host = Dns.GetHostAddresses(host.Split(":")[0])[0];

            // Port may have an extra / at the end. Use regex to keep only the number in the port
            Port = int.Parse(OnlyDigitsRegex().Match(host.Split(":")[1]).Value);
        }
        else
        {
            var hostPort = address.Split(':');
            Host = IPAddress.Parse(hostPort[0]);
            Port = int.Parse(hostPort[1]);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PeerAddress"/> class.
    /// </summary>
    /// <param name="pubKey">The public key.</param>
    /// <param name="host">The host.</param>
    /// <param name="port">The port.</param>
    public PeerAddress(CompactPubKey pubKey, string host, int port)
    {
        PubKey = pubKey;
        Host = IPAddress.Parse(host);
        Port = port;
    }

    /// <summary>
    /// Returns a string that represents the address.
    /// </summary>
    /// <returns>A string in the format of "pubkey@host:port".</returns>
    public override string ToString()
    {
        return $"{PubKey}@{Host}:{Port}";
    }

    public bool Equals(PeerAddress? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return PubKey.Equals(other.PubKey) && Host.Equals(other.Host) && Port == other.Port;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is PeerAddress other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(PubKey, Host, Port);
    }
}