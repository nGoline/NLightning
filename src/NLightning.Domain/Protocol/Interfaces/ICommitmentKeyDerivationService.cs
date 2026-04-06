namespace NLightning.Domain.Protocol.Interfaces;

using Channels.ValueObjects;
using Domain.Crypto.ValueObjects;

public interface ICommitmentKeyDerivationService
{
    /// <summary>
    /// Derives the local commitment keys based on the provided parameters, including local and remote basepoints and the commitment number.
    /// </summary>
    /// <param name="localChannelKeyIndex">An index representing the local channel key for deriving commitment keys.</param>
    /// <param name="localBasepoints">The set of cryptographic basepoints associated with the local channel.</param>
    /// <param name="remoteBasepoints">The set of cryptographic basepoints associated with the remote channel.</param>
    /// <param name="commitmentNumber">A numeric identifier representing the specific commitment.</param>
    /// <returns>A <see cref="CommitmentKeys"/> instance containing the derived keys for the local commitment.</returns>
    CommitmentKeys DeriveLocalCommitmentKeys(uint localChannelKeyIndex, ChannelBasepoints localBasepoints,
                                             ChannelBasepoints remoteBasepoints, ulong commitmentNumber);

    /// <summary>
    /// Derives the remote commitment keys based on the provided parameters, including local and remote basepoints,
    /// the remote per-commitment point, and the commitment number.
    /// </summary>
    /// <param name="localBasepoints">The set of cryptographic basepoints associated with the local channel.</param>
    /// <param name="remoteBasepoints">The set of cryptographic basepoints associated with the remote channel.</param>
    /// <param name="remotePerCommitmentPoint">The per-commitment point provided by the remote party, used for key derivation.</param>
    /// <returns>A <see cref="CommitmentKeys"/> instance containing the derived keys for the remote commitment.</returns>
    CommitmentKeys DeriveRemoteCommitmentKeys(ChannelBasepoints localBasepoints, ChannelBasepoints remoteBasepoints,
                                              CompactPubKey remotePerCommitmentPoint);
}