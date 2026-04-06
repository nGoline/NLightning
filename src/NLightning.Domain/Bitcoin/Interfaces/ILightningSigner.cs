namespace NLightning.Domain.Bitcoin.Interfaces;

using Channels.ValueObjects;
using Crypto.ValueObjects;
using ValueObjects;

/// <summary>
/// Interface for transaction signing services that can be implemented either locally 
/// or delegated to external services like VLS (Validating Lightning Signer)
/// </summary>
public interface ILightningSigner
{
    /// <summary>
    /// Generate a new channel key set and return the channel key index
    /// </summary>
    uint CreateNewChannel(out ChannelBasepoints basepoints, out CompactPubKey firstPerCommitmentPoint);

    /// <summary>
    /// Generate or retrieve channel basepoints for a channel
    /// </summary>
    ChannelBasepoints GetChannelBasepoints(uint channelKeyIndex);

    /// <summary>
    /// Generate or retrieve channel basepoints for a channel
    /// </summary>
    ChannelBasepoints GetChannelBasepoints(ChannelId channelId);

    /// <summary>
    /// Get the node's public key
    /// </summary>
    CompactPubKey GetNodePublicKey();

    /// <summary>
    /// Generate a per-commitment point for a specific commitment number
    /// </summary>
    CompactPubKey GetPerCommitmentPoint(uint channelKeyIndex, ulong commitmentNumber);

    /// <summary>
    /// Generate a per-commitment point for a specific commitment number
    /// </summary>
    CompactPubKey GetPerCommitmentPoint(ChannelId channelId, ulong commitmentNumber);

    /// <summary>
    /// Store channel information needed for signing
    /// </summary>
    void RegisterChannel(ChannelId channelId, ChannelSigningInfo signingInfo);

    /// <summary>
    /// Release (reveal) a per-commitment secret for revocation
    /// </summary>
    Secret ReleasePerCommitmentSecret(uint channelKeyIndex, ulong commitmentNumber);

    /// <summary>
    /// Release (reveal) a per-commitment secret for revocation
    /// </summary>
    Secret ReleasePerCommitmentSecret(ChannelId channelId, ulong commitmentNumber);

    /// <summary>
    /// Sign a general transaction using the wallet signing context
    /// </summary>
    bool SignWalletTransaction(SignedTransaction unsignedTransaction);

    /// <summary>
    /// Sign a funding transaction using the wallet signing context  and validating using the channel context
    /// </summary>
    bool SignFundingTransaction(ChannelId channelId, SignedTransaction unsignedTransaction);

    /// <summary>
    /// Sign a transaction using the channel's signing context
    /// </summary>
    CompactSignature SignChannelTransaction(ChannelId channelId, SignedTransaction unsignedTransaction);

    /// <summary>
    /// Verify a signature against a transaction
    /// </summary>
    void ValidateSignature(ChannelId channelId, CompactSignature signature, SignedTransaction unsignedTransaction);
}