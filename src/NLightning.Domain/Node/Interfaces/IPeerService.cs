namespace NLightning.Domain.Node.Interfaces;

using Crypto.ValueObjects;
using Domain.Protocol.Interfaces;
using Events;
using Exceptions;
using Options;

/// <summary>
/// Interface for the peer application service.
/// </summary>
public interface IPeerService : IDisposable
{
    /// <summary>
    /// Gets the peer's public key.
    /// </summary>
    CompactPubKey PeerPubKey { get; }

    /// <summary>
    /// Gets the feature options for the peer.
    /// </summary>
    FeatureOptions Features { get; }

    /// <summary>
    /// Event raised when the peer is disconnected.
    /// </summary>
    event EventHandler<PeerDisconnectedEventArgs> OnDisconnect;

    /// <summary>
    /// Occurs when a channel message is received from the connected peer.
    /// </summary>
    event EventHandler<ChannelMessageEventArgs> OnChannelMessageReceived;

    /// <summary>
    /// Occurs when an Error or Warning message is received from the connected peer.
    /// </summary>
    event EventHandler<AttentionMessageEventArgs>? OnAttentionMessageReceived;

    /// <summary>
    /// Occurs when an exception is raised during peer communication.
    /// </summary>
    event EventHandler<Exception>? OnExceptionRaised;

    public string? PreferredHost { get; }
    public ushort? PreferredPort { get; }

    /// <summary>
    /// Disconnects from the peer.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnection, if any.</param>
    void Disconnect(Exception? exception = null);

    /// <summary>
    /// Sends an asynchronous message to the peer.
    /// </summary>
    /// <param name="replyMessage">The message to be sent to the peer.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SendMessageAsync(IChannelMessage replyMessage);

    /// <summary>
    /// Sends a warning message to the peer.
    /// </summary>
    /// <param name="we">The warning exception containing the warning message to be sent to the peer.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SendWarningAsync(WarningException we);
}