namespace NLightning.Infrastructure.Bitcoin.Services;

using Domain.Bitcoin.Interfaces;
using Domain.Channels.ValueObjects;
using Domain.Crypto.ValueObjects;
using Domain.Protocol.Interfaces;

public class CommitmentKeyDerivationService : ICommitmentKeyDerivationService
{
    private readonly IKeyDerivationService _keyDerivationService;
    private readonly ILightningSigner _lightningSigner;

    public CommitmentKeyDerivationService(IKeyDerivationService keyDerivationService, ILightningSigner lightningSigner)
    {
        _keyDerivationService = keyDerivationService;
        _lightningSigner = lightningSigner;
    }

    /// <inheritdoc />
    public CommitmentKeys DeriveLocalCommitmentKeys(uint localChannelKeyIndex, ChannelBasepoints localBasepoints,
                                                    ChannelBasepoints remoteBasepoints, ulong commitmentNumber)
    {
        // Get our per-commitment point for this commitment number from the signer
        var perCommitmentPoint = _lightningSigner.GetPerCommitmentPoint(localChannelKeyIndex, commitmentNumber);

        // Get our per-commitment secret from the signer
        var perCommitmentSecret = _lightningSigner.ReleasePerCommitmentSecret(localChannelKeyIndex, commitmentNumber);

        // For our local commitment transaction:
        // - localpubkey = our payment_basepoint + SHA256(our_per_commitment_point || our_payment_basepoint) * G
        var localPubKey = _keyDerivationService.DerivePublicKey(localBasepoints.PaymentBasepoint, perCommitmentPoint);

        // - local_delayedpubkey = our delayed_payment_basepoint + SHA256(our_per_commitment_point || our_delayed_payment_basepoint) * G
        var localDelayedPubKey =
            _keyDerivationService.DerivePublicKey(localBasepoints.DelayedPaymentBasepoint, perCommitmentPoint);

        // - local_htlcpubkey = our htlc_basepoint + SHA256(our_per_commitment_point || our_htlc_basepoint) * G
        var localHtlcPubKey = _keyDerivationService.DerivePublicKey(localBasepoints.HtlcBasepoint, perCommitmentPoint);

        // - remote_htlcpubkey = their htlc_basepoint + SHA256(our_per_commitment_point || their_htlc_basepoint) * G
        var remoteHtlcPubKey =
            _keyDerivationService.DerivePublicKey(remoteBasepoints.HtlcBasepoint, perCommitmentPoint);

        // - revocationpubkey = derived from their revocation_basepoint and our per_commitment_point
        // This allows them to revoke our commitment if we broadcast an old one
        var revocationPubKey =
            _keyDerivationService.DeriveRevocationPubKey(remoteBasepoints.RevocationBasepoint, perCommitmentPoint);

        return new CommitmentKeys(
            localPubKey, // localpubkey (for to_local output)
            localDelayedPubKey, // local_delayedpubkey (for to_local output with delay)
            revocationPubKey, // revocationpubkey (allows them to revoke our commitment)
            localHtlcPubKey, // local_htlcpubkey (for our HTLC outputs)
            remoteHtlcPubKey, // remote_htlcpubkey (for their HTLC outputs)
            perCommitmentPoint, // our per_commitment_point
            perCommitmentSecret // our per_commitment_secret
        );
    }

    /// <inheritdoc />
    public CommitmentKeys DeriveRemoteCommitmentKeys(ChannelBasepoints localBasepoints,
                                                     ChannelBasepoints remoteBasepoints,
                                                     CompactPubKey remotePerCommitmentPoint)
    {
        // For their commitment transaction, we use their provided per-commitment point
        // they should provide this via commitment_signed or update messages

        // For their remote commitment transaction:
        // - localpubkey (from their perspective) = their payment_basepoint + SHA256(their_per_commitment_point || their_payment_basepoint) * G
        var theirLocalPubKey = _keyDerivationService.DerivePublicKey(
            remoteBasepoints.PaymentBasepoint, remotePerCommitmentPoint);

        // - local_delayedpubkey (from their perspective) = their delayed_payment_basepoint + SHA256(their_per_commitment_point || their_delayed_payment_basepoint) * G
        var theirDelayedPubKey = _keyDerivationService.DerivePublicKey(
            remoteBasepoints.DelayedPaymentBasepoint, remotePerCommitmentPoint);

        // - revocationpubkey = derived from our revocation_basepoint and their per_commitment_point
        // This allows us to revoke their commitment if they broadcast an old one
        var revocationPubKey = _keyDerivationService.DeriveRevocationPubKey(
            localBasepoints.RevocationBasepoint, remotePerCommitmentPoint);

        // - local_htlcpubkey (from their perspective) = their htlc_basepoint + SHA256(their_per_commitment_point || their_htlc_basepoint) * G
        var theirHtlcPubKey = _keyDerivationService.DerivePublicKey(
            remoteBasepoints.HtlcBasepoint, remotePerCommitmentPoint);

        // - remote_htlcpubkey (from their perspective) = our htlc_basepoint + SHA256(their_per_commitment_point || our_htlc_basepoint) * G
        var ourHtlcPubKey = _keyDerivationService.DerivePublicKey(
            localBasepoints.HtlcBasepoint, remotePerCommitmentPoint);

        return new CommitmentKeys(
            theirLocalPubKey, // localpubkey (from their perspective, for their to_local output)
            theirDelayedPubKey, // local_delayedpubkey (from their perspective)
            revocationPubKey, // revocationpubkey (allows us to revoke their commitment)
            theirHtlcPubKey, // local_htlcpubkey (from their perspective)
            ourHtlcPubKey, // remote_htlcpubkey (from their perspective)
            remotePerCommitmentPoint, // their per_commitment_point
            null // We don't have their secret
        );
    }
}