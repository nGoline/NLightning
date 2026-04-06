namespace NLightning.Domain.Protocol.Payloads;

using Domain.Channels.ValueObjects;
using Domain.Crypto.ValueObjects;
using Interfaces;
using Money;

/// <summary>
/// Represents the payload for the accept_channel message.
/// </summary>
/// <remarks>
/// Initializes a new instance of the AcceptChannel1Payload class.
/// </remarks>
public class AcceptChannel1Payload : IChannelMessagePayload
{
    /// <summary>
    /// The temporary_channel_id is used to identify this channel on a per-peer basis until the funding transaction
    /// is established, at which point it is replaced by the channel_id, which is derived from the funding transaction.
    /// </summary>
    public ChannelId ChannelId { get; set; }

    /// <summary>
    /// dust_limit_satoshis is the threshold below which outputs should not be generated for this node's commitment or
    /// HTLC transactions
    /// </summary>
    public LightningMoney DustLimitAmount { get; }

    /// <summary>
    /// max_htlc_value_in_flight_msat is a cap on the total value of outstanding HTLCs offered by the remote node, which
    /// allows the local node to limit its exposure to HTLCs
    /// </summary>
    public LightningMoney MaxHtlcValueInFlightAmount { get; }

    /// <summary>
    /// channel_reserve_satoshis is the amount the acceptor is reserving for the channel, which is not available for
    /// spending
    /// </summary>
    public LightningMoney ChannelReserveAmount { get; set; }

    /// <summary>
    /// htlc_minimum_msat indicates the smallest value HTLC this node will accept.
    /// </summary>
    public LightningMoney HtlcMinimumAmount { get; }

    /// <summary>
    /// minimum_depth is the number of blocks we consider reasonable to avoid double-spending of the funding transaction.
    /// In case channel_type includes option_zeroconf, this MUST be 0
    /// </summary>
    public uint MinimumDepth { get; set; }

    /// <summary>
    /// to_self_delay is how long (in blocks) the other node will have to wait in case of breakdown before redeeming
    /// its own funds.
    /// </summary>
    public ushort ToSelfDelay { get; }

    /// <summary>
    /// max_accepted_htlcs limits the number of outstanding HTLCs the remote node can offer.
    /// </summary>
    public ushort MaxAcceptedHtlcs { get; }

    /// <summary>
    /// funding_pubkey is the public key in the 2-of-2 multisig script of the funding transaction output.
    /// </summary>
    public CompactPubKey FundingPubKey { get; set; }

    /// <summary>
    /// revocation_basepoint is used to regenerate the scripts required for the penalty transaction
    /// </summary>
    public CompactPubKey RevocationBasepoint { get; set; }

    /// <summary>
    /// payment_basepoint is used to produce payment signatures for the protocol
    /// </summary>
    public CompactPubKey PaymentBasepoint { get; set; }

    /// <summary>
    /// delayed_payment_basepoint is used to regenerate the scripts required for the penalty transaction
    /// </summary>
    public CompactPubKey DelayedPaymentBasepoint { get; set; }

    /// <summary>
    /// htlc_basepoint is used to produce HTLC signatures for the protocol
    /// </summary>
    public CompactPubKey HtlcBasepoint { get; set; }

    /// <summary>
    /// first_per_commitment_point is the per-commitment point used for the first commitment transaction
    /// </summary>
    public CompactPubKey FirstPerCommitmentPoint { get; set; }

    public AcceptChannel1Payload(ChannelId channelId, LightningMoney channelReserveAmount,
                                 CompactPubKey delayedPaymentBasepoint, LightningMoney dustLimitAmount,
                                 CompactPubKey firstPerCommitmentPoint, CompactPubKey fundingPubKey,
                                 CompactPubKey htlcBasepoint, LightningMoney htlcMinimumAmount, ushort maxAcceptedHtlcs,
                                 LightningMoney maxHtlcValueInFlight, uint minimumDepth, CompactPubKey paymentBasepoint,
                                 CompactPubKey revocationBasepoint, ushort toSelfDelay)
    {
        ChannelId = channelId;
        DustLimitAmount = dustLimitAmount;
        MaxHtlcValueInFlightAmount = maxHtlcValueInFlight;
        ChannelReserveAmount = channelReserveAmount;
        HtlcMinimumAmount = htlcMinimumAmount;
        MinimumDepth = minimumDepth;
        ToSelfDelay = toSelfDelay;
        MaxAcceptedHtlcs = maxAcceptedHtlcs;
        FundingPubKey = fundingPubKey;
        RevocationBasepoint = revocationBasepoint;
        PaymentBasepoint = paymentBasepoint;
        DelayedPaymentBasepoint = delayedPaymentBasepoint;
        HtlcBasepoint = htlcBasepoint;
        FirstPerCommitmentPoint = firstPerCommitmentPoint;
    }
}