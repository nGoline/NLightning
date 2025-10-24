namespace NLightning.Infrastructure.Transport.Interfaces;

using Events;
using Node.ValueObjects;
using Protocol.Models;

public interface ITcpService
{
    /// <summary>
    /// Event triggered when a new peer successfully establishes a connection.
    /// </summary>
    /// <remarks>
    /// This event provides the means to handle actions or logic that should occur
    /// when a new peer connects to the TCP listener.
    /// </remarks>
    event EventHandler<NewPeerConnectedEventArgs> OnNewPeerConnected;

    /// <summary>
    /// Starts the TCP listener service and begins listening for incoming connections on the configured addresses.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests, which can be used to stop listening.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartListeningAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the TCP listener service and cleans up all active listeners, ensuring that no further connections can be made.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopListeningAsync();

    /// <summary>
    /// Establishes a connection to a peer specified by the provided address information.
    /// </summary>
    /// <param name="peerAddressInfo">The address information of the peer to connect to.</param>
    /// <returns>A task representing the asynchronous operation. The result contains a <see cref="ConnectedPeer"/> object representing the connected peer.</returns>
    Task<ConnectedPeer> ConnectToPeerAsync(PeerAddress peerAddress);
}