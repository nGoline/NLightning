namespace NLightning.Domain.Node.Interfaces;

using Crypto.ValueObjects;
using Models;
using ValueObjects;

/// <summary>
/// Interface for the peer manager.
/// </summary>
public interface IPeerManager
{
    /// <summary>
    /// Starts the peer manager asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the peer manager asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task StopAsync();

    /// <summary>
    /// Connects to a peer.
    /// </summary>
    /// <param name="peerAddressInfo">The peer address to connect to.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<PeerModel> ConnectToPeerAsync(PeerAddressInfo peerAddressInfo);

    /// <summary>
    /// Disconnects a peer.
    /// </summary>
    /// <param name="compactPubKey" cref="CompactPubKey">CompactPubKey of the peer</param>
    void DisconnectPeer(CompactPubKey compactPubKey);

    List<PeerModel> ListPeers();
    PeerModel? GetPeer(CompactPubKey peerId);
}