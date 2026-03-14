namespace NLightning.Domain.Node.Interfaces;

using Crypto.ValueObjects;
using Exceptions;
using Protocol.Interfaces;

/// <summary>
/// Interface for communication with a single peer.
/// </summary>
public interface IPeerCommunicationService : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the connection is established.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the peer's public key.
    /// </summary>
    CompactPubKey PeerCompactPubKey { get; }

    /// <summary>
    /// Event raised when a message is received from the peer.
    /// </summary>
    event EventHandler<IMessage?> MessageReceived;

    /// <summary>
    /// Event raised when the peer is disconnected.
    /// </summary>
    event EventHandler? DisconnectEvent;

    /// <summary>
    /// Event raised when an exception occurs.
    /// </summary>
    event EventHandler<Exception>? ExceptionRaised;

    /// <summary>
    /// Sends a message to the peer.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SendMessageAsync(IMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a warning message to the peer.
    /// </summary>
    /// <param name="we">The warning exception to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SendWarningAsync(WarningException we, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the communication with the peer.
    /// </summary>
    /// <param name="networkTimeout">The network timeout.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task InitializeAsync(TimeSpan networkTimeout);

    /// <summary>
    /// Disconnects from the peer.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnection, if any.</param>
    void Disconnect(Exception? exception = null);
}