using Microsoft.Extensions.Options;

namespace NLightning.Application.Protocol.Factories;

using Domain.Bitcoin.ValueObjects;
using Domain.Channels.ValueObjects;
using Domain.Crypto.ValueObjects;
using Domain.Money;
using Domain.Node.Options;
using Domain.Protocol.Interfaces;
using Domain.Protocol.Messages;
using Domain.Protocol.Models;
using Domain.Protocol.Payloads;
using Domain.Protocol.Tlv;
using Domain.Protocol.ValueObjects;

/// <summary>
/// Factory for creating messages.
/// </summary>
public class MessageFactory : IMessageFactory
{
    private readonly NodeOptions _nodeOptions;
    private readonly BitcoinNetwork _bitcoinNetwork;

    public MessageFactory(IOptions<NodeOptions> nodeOptions)
    {
        _nodeOptions = nodeOptions.Value;
        _bitcoinNetwork = _nodeOptions.BitcoinNetwork;
    }

    #region Init Message

    /// <summary>
    /// Create an Init message.
    /// </summary>
    /// <returns>The Init message.</returns>
    /// <seealso cref="InitMessage"/>
    /// <seealso cref="InitPayload"/>
    public InitMessage CreateInitMessage()
    {
        // Get features from options
        var features = _nodeOptions.Features.GetNodeFeatures();
        var payload = new InitPayload(features);

        return new InitMessage(payload, _nodeOptions.Features.GetInitTlvs());
    }

    #endregion

    #region Control Messages

    /// <summary>
    /// Create a Warning message.
    /// </summary>
    /// <param name="message">The message to be sent.</param>
    /// <param name="channelId">The channel id, if any.</param>
    /// <returns>The Warning message.</returns>
    /// <seealso cref="WarningMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="ErrorPayload"/>
    public WarningMessage CreateWarningMessage(string message, ChannelId? channelId)
    {
        var payload = channelId is null ? new ErrorPayload(message) : new ErrorPayload(channelId, message);
        return new WarningMessage(payload);
    }

    /// <summary>
    /// Create a Warning message.
    /// </summary>
    /// <param name="data">The data to be sent.</param>
    /// <param name="channelId">The channel id, if any.</param>
    /// <returns>The Warning message.</returns>
    /// <seealso cref="WarningMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="ErrorPayload"/>
    public WarningMessage CreateWarningMessage(byte[] data, ChannelId? channelId)
    {
        var payload = channelId is null ? new ErrorPayload(data) : new ErrorPayload(channelId.Value, data);
        return new WarningMessage(payload);
    }

    /// <summary>
    /// Create a Stfu message.
    /// </summary>
    /// <param name="channelId">The channel id, if any.</param>
    /// <param name="initiator">If we are the sender of the message.</param>
    /// <returns>The Stfu message.</returns>
    /// <seealso cref="StfuMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="StfuPayload"/>
    public StfuMessage CreateStfuMessage(ChannelId channelId, bool initiator)
    {
        var payload = new StfuPayload(channelId, initiator);
        return new StfuMessage(payload);
    }

    /// <summary>
    /// Create an Error message.
    /// </summary>
    /// <param name="message">The message to be sent.</param>
    /// <param name="channelId">The channel id, if any.</param>
    /// <returns>The Error message.</returns>
    /// <seealso cref="ErrorMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="ErrorPayload"/>
    public ErrorMessage CreateErrorMessage(string message, ChannelId? channelId)
    {
        var payload = channelId is null ? new ErrorPayload(message) : new ErrorPayload(channelId.Value, message);
        return new ErrorMessage(payload);
    }

    /// <summary>
    /// Create an Error message.
    /// </summary>
    /// <param name="data">The data to be sent.</param>
    /// <param name="channelId">The channel id, if any.</param>
    /// <returns>The Error message.</returns>
    /// <seealso cref="ErrorMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="ErrorPayload"/>
    public ErrorMessage CreateErrorMessage(byte[] data, ChannelId? channelId)
    {
        var payload = channelId is null ? new ErrorPayload(data) : new ErrorPayload(channelId.Value, data);
        return new ErrorMessage(payload);
    }

    /// <summary>
    /// Create a Ping message.
    /// </summary>
    /// <returns>The Ping message.</returns>
    /// <seealso cref="PingMessage"/>
    /// <seealso cref="PingPayload"/>
    public PingMessage CreatePingMessage()
    {
        return new PingMessage();
    }

    /// <summary>
    /// Create a Pong message.
    /// </summary>
    /// <param name="pingMessage">The ping message we're responding to</param>
    /// <returns>The Pong message.</returns>
    /// <seealso cref="PongMessage"/>
    /// <seealso cref="PongPayload"/>
    public PongMessage CreatePongMessage(IMessage pingMessage)
    {
        if (pingMessage is not PingMessage ping)
        {
            throw new ArgumentException("Ping message is required", nameof(pingMessage));
        }

        return new PongMessage(ping.Payload.NumPongBytes);
    }

    #endregion

    #region Interactive Transaction Construction

    /// <summary>
    /// Create a TxAddInput message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="serialId">The serial id.</param>
    /// <param name="prevTx">The previous transaction.</param>
    /// <param name="prevTxVout">The previous transaction vout.</param>
    /// <param name="sequence">The sequence number.</param>
    /// <returns>The TxAddInput message.</returns>
    /// <seealso cref="TxAddInputMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="TxAddInputPayload"/>
    public TxAddInputMessage CreateTxAddInputMessage(ChannelId channelId, ulong serialId, byte[] prevTx,
                                                     uint prevTxVout,
                                                     uint sequence)
    {
        var payload = new TxAddInputPayload(channelId, serialId, prevTx, prevTxVout, sequence);

        return new TxAddInputMessage(payload);
    }

    /// <summary>
    /// Create a TxAddOutput message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="serialId">The serial id.</param>
    /// <param name="amount">The number of satoshis.</param>
    /// <param name="script">The script.</param>
    /// <returns>The TxAddOutput message.</returns>
    /// <seealso cref="TxAddOutputMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="TxAddOutputPayload"/>
    public TxAddOutputMessage CreateTxAddOutputMessage(ChannelId channelId, ulong serialId, LightningMoney amount,
                                                       BitcoinScript script)
    {
        var payload = new TxAddOutputPayload(amount, channelId, script, serialId);

        return new TxAddOutputMessage(payload);
    }

    /// <summary>
    /// Create a TxRemoveInput message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="serialId">The serial id.</param>
    /// <returns>The TxRemoveInput message.</returns>
    /// <seealso cref="TxRemoveInputMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="TxRemoveInputPayload"/>
    public TxRemoveInputMessage CreateTxRemoveInputMessage(ChannelId channelId, ulong serialId)
    {
        var payload = new TxRemoveInputPayload(channelId, serialId);

        return new TxRemoveInputMessage(payload);
    }

    /// <summary>
    /// Create a TxRemoveOutput message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="serialId">The serial id.</param>
    /// <returns>The TxRemoveOutput message.</returns>
    /// <seealso cref="TxRemoveOutputMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="TxRemoveOutputPayload"/>
    public TxRemoveOutputMessage CreateTxRemoveOutputMessage(ChannelId channelId, ulong serialId)
    {
        var payload = new TxRemoveOutputPayload(channelId, serialId);

        return new TxRemoveOutputMessage(payload);
    }

    /// <summary>
    /// Create a TxComplete message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <returns>The TxComplete message.</returns>
    /// <seealso cref="TxCompleteMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="TxCompletePayload"/>
    public TxCompleteMessage CreateTxCompleteMessage(ChannelId channelId)
    {
        var payload = new TxCompletePayload(channelId);

        return new TxCompleteMessage(payload);
    }

    /// <summary>
    /// Create a TxSignatures message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="txId">The transaction id.</param>
    /// <param name="witnesses">The witnesses.</param>
    /// <returns>The TxSignatures message.</returns>
    /// <seealso cref="TxSignaturesMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="TxSignaturesPayload"/>
    public TxSignaturesMessage CreateTxSignaturesMessage(ChannelId channelId, byte[] txId, List<Witness> witnesses)
    {
        var payload = new TxSignaturesPayload(channelId, txId, witnesses);

        return new TxSignaturesMessage(payload);
    }

    /// <summary>
    /// Create a TxInitRbf message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="locktime">The locktime.</param>
    /// <param name="feerate">The feerate</param>
    /// <param name="fundingOutputContrubution">The output contribution.</param>
    /// <param name="requireConfirmedInputs">How many confirmed inputs we need.</param>
    /// <returns>The TxInitRbf message.</returns>
    /// <seealso cref="TxInitRbfMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="TxInitRbfPayload"/>
    public TxInitRbfMessage CreateTxInitRbfMessage(ChannelId channelId, uint locktime, uint feerate,
                                                   long fundingOutputContrubution, bool requireConfirmedInputs)
    {
        FundingOutputContributionTlv? fundingOutputContributionTlv = null;
        RequireConfirmedInputsTlv? requireConfirmedInputsTlv = null;

        if (fundingOutputContrubution > 0)
        {
            fundingOutputContributionTlv = new FundingOutputContributionTlv(fundingOutputContrubution);
        }

        if (requireConfirmedInputs)
        {
            requireConfirmedInputsTlv = new RequireConfirmedInputsTlv();
        }

        var payload = new TxInitRbfPayload(channelId, locktime, feerate);

        return new TxInitRbfMessage(payload, fundingOutputContributionTlv, requireConfirmedInputsTlv);
    }

    /// <summary>
    /// Create a TxAckRbf message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="fundingOutputContrubution">The output contribution.</param>
    /// <param name="requireConfirmedInputs">How many confirmed inputs we need.</param>
    /// <returns>The TxAckRbf message.</returns>
    /// <seealso cref="TxAckRbfMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="TxAckRbfPayload"/>
    /// <seealso cref="TlvStream"/>
    /// <seealso cref="FundingOutputContributionTlv"/>
    /// <seealso cref="RequireConfirmedInputsTlv"/>
    public TxAckRbfMessage CreateTxAckRbfMessage(ChannelId channelId, long fundingOutputContrubution,
                                                 bool requireConfirmedInputs)
    {
        FundingOutputContributionTlv? fundingOutputContributionTlv = null;
        RequireConfirmedInputsTlv? requireConfirmedInputsTlv = null;

        if (fundingOutputContrubution > 0)
        {
            fundingOutputContributionTlv = new FundingOutputContributionTlv(fundingOutputContrubution);
        }

        if (requireConfirmedInputs)
        {
            requireConfirmedInputsTlv = new RequireConfirmedInputsTlv();
        }

        var payload = new TxAckRbfPayload(channelId);

        return new TxAckRbfMessage(payload, fundingOutputContributionTlv, requireConfirmedInputsTlv);
    }

    /// <summary>
    /// Create a TxAbort message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="data">The data.</param>
    /// <returns>The TxAbort message.</returns>
    /// <seealso cref="TxAbortMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="TxAbortPayload"/>
    public TxAbortMessage CreateTxAbortMessage(ChannelId channelId, byte[] data)
    {
        var payload = new TxAbortPayload(channelId, data);

        return new TxAbortMessage(payload);
    }

    #endregion

    #region Channel Messages

    /// <summary>
    /// Create a ChannelReady message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="secondPerCommitmentPoint">The second per commitment point.</param>
    /// <param name="shortChannelId">The channel's shortChannelId.</param>
    /// <returns>The ChannelReady message.</returns>
    /// <seealso cref="ChannelReadyMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="CompactPubKey"/>
    /// <seealso cref="ShortChannelId"/>
    /// <seealso cref="ChannelReadyPayload"/>
    public ChannelReadyMessage CreateChannelReadyMessage(ChannelId channelId, CompactPubKey secondPerCommitmentPoint,
                                                         ShortChannelId? shortChannelId = null)
    {
        var payload = new ChannelReadyPayload(channelId, secondPerCommitmentPoint);

        return new ChannelReadyMessage(payload,
                                       shortChannelId is null ? null : new ShortChannelIdTlv(shortChannelId.Value));
    }

    /// <summary>
    /// Create a Shutdown message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="scriptPubkey">The ScriptPubKey to send closing funds to.</param>
    /// <returns>The Shutdown message.</returns>
    /// <seealso cref="ShutdownMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="BitcoinScript"/>
    /// <seealso cref="ShutdownPayload"/>
    public ShutdownMessage CreateShutdownMessage(ChannelId channelId, BitcoinScript scriptPubkey)
    {
        var payload = new ShutdownPayload(channelId, scriptPubkey);

        return new ShutdownMessage(payload);
    }

    /// <summary>
    /// Create a ClosingSigned message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="feeSatoshis">The fee we want them to pay for closing the channel.</param>
    /// <param name="signature">The signature for closing the channel.</param>
    /// <param name="minFeeSatoshis">The min fee we will accept them to pay to close the channel.</param>
    /// <param name="maxFeeSatoshis">The max fee we will accept them to pay to close the channel.</param>
    /// <returns>The ClosingSigned message.</returns>
    /// <seealso cref="ClosingSignedMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="CompactSignature"/>
    /// <seealso cref="ClosingSignedPayload"/>
    public ClosingSignedMessage CreateClosingSignedMessage(ChannelId channelId, ulong feeSatoshis,
                                                           CompactSignature signature,
                                                           ulong minFeeSatoshis, ulong maxFeeSatoshis)
    {
        var payload = new ClosingSignedPayload(channelId, feeSatoshis, signature);

        return new ClosingSignedMessage(payload, new FeeRangeTlv(minFeeSatoshis, maxFeeSatoshis));
    }

    /// <summary>
    /// Create an OpenChannel1 message.
    /// </summary>
    /// <param name="temporaryChannelId">The temporary channel id.</param>
    /// <param name="fundingAmount">The amount of satoshis we're adding to the channel.</param>
    /// <param name="fundingPubKey">The funding pubkey of the channel.</param>
    /// <param name="pushAmount">The amount of satoshis we're pushing to the other side.</param>
    /// <param name="channelReserveAmount">The channel reserve amount.</param>
    /// <param name="feeRatePerKw">The fee rate per kw.</param>
    /// <param name="maxAcceptedHtlcs">The max accepted htlcs.</param>
    /// <param name="revocationBasepoint">The revocation pubkey.</param>
    /// <param name="paymentBasepoint">The payment pubkey.</param>
    /// <param name="delayedPaymentBasepoint">The delayed payment pubkey.</param>
    /// <param name="htlcBasepoint">The htlc pubkey.</param>
    /// <param name="firstPerCommitmentPoint">The first per-commitment pubkey.</param>
    /// <param name="channelFlags">The flags for the channel.</param>
    /// <param name="upfrontShutdownScriptTlv">The upfront shutdown script tlv.</param>
    /// <param name="channelTypeTlv">The channel type tlv.</param>
    /// <returns>The OpenChannel1 message.</returns>
    /// <seealso cref="OpenChannel1Message"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="LightningMoney"/>
    /// <seealso cref="CompactPubKey"/>
    /// <seealso cref="UpfrontShutdownScriptTlv"/>
    /// <seealso cref="ChannelTypeTlv"/>
    public OpenChannel1Message CreateOpenChannel1Message(ChannelId temporaryChannelId, LightningMoney fundingAmount,
                                                         CompactPubKey fundingPubKey, LightningMoney pushAmount,
                                                         LightningMoney channelReserveAmount,
                                                         LightningMoney feeRatePerKw, ushort maxAcceptedHtlcs,
                                                         CompactPubKey revocationBasepoint,
                                                         CompactPubKey paymentBasepoint,
                                                         CompactPubKey delayedPaymentBasepoint,
                                                         CompactPubKey htlcBasepoint,
                                                         CompactPubKey firstPerCommitmentPoint,
                                                         ChannelFlags channelFlags,
                                                         UpfrontShutdownScriptTlv? upfrontShutdownScriptTlv,
                                                         ChannelTypeTlv? channelTypeTlv)
    {
        var maxHtlcValueInFlight =
            LightningMoney.Satoshis(_nodeOptions.AllowUpToPercentageOfChannelFundsInFlight * fundingAmount.Satoshi /
                                    100M);
        var payload = new OpenChannel1Payload(_nodeOptions.BitcoinNetwork.ChainHash, channelFlags, temporaryChannelId,
                                              channelReserveAmount, delayedPaymentBasepoint,
                                              _nodeOptions.DustLimitAmount, feeRatePerKw, firstPerCommitmentPoint,
                                              fundingAmount, fundingPubKey, htlcBasepoint,
                                              _nodeOptions.HtlcMinimumAmount, maxAcceptedHtlcs, maxHtlcValueInFlight,
                                              paymentBasepoint, pushAmount, revocationBasepoint,
                                              _nodeOptions.ToSelfDelay);

        return new OpenChannel1Message(payload, upfrontShutdownScriptTlv, channelTypeTlv);
    }

    /// <summary>
    /// Create an OpenChannel2 message.
    /// </summary>
    /// <param name="temporaryChannelId">The temporary channel id.</param>
    /// <param name="fundingFeeRatePerKw">The funding fee rate to open the channel.</param>
    /// <param name="commitmentFeeRatePerKw">The commitment fee rate.</param>
    /// <param name="fundingSatoshis">The amount of satoshis we're adding to the channel.</param>
    /// <param name="fundingPubKey">The funding pubkey of the channel.</param>
    /// <param name="revocationBasepoint">The revocation pubkey.</param>
    /// <param name="paymentBasepoint">The payment pubkey.</param>
    /// <param name="delayedPaymentBasepoint">The delayed payment pubkey.</param>
    /// <param name="htlcBasepoint">The htlc pubkey.</param>
    /// <param name="firstPerCommitmentPoint">The first per-commitment pubkey.</param>
    /// <param name="secondPerCommitmentPoint">The second per-commitment pubkey.</param>
    /// <param name="channelFlags">The flags for the channel.</param>
    /// <param name="shutdownScriptPubkey">The shutdown script to be used when closing the channel.</param>
    /// <param name="channelType">The type of the channel.</param>
    /// <param name="requireConfirmedInputs">If we want confirmed inputs to open the channel.</param>
    /// <returns>The OpenChannel2 message.</returns>
    /// <seealso cref="OpenChannel2Message"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="CompactPubKey"/>
    /// <seealso cref="ChannelFlags"/>
    /// <seealso cref="BitcoinScript"/>
    /// <seealso cref="OpenChannel2Payload"/>
    public OpenChannel2Message CreateOpenChannel2Message(ChannelId temporaryChannelId, uint fundingFeeRatePerKw,
                                                         uint commitmentFeeRatePerKw, ulong fundingSatoshis,
                                                         CompactPubKey fundingPubKey,
                                                         CompactPubKey revocationBasepoint,
                                                         CompactPubKey paymentBasepoint,
                                                         CompactPubKey delayedPaymentBasepoint,
                                                         CompactPubKey htlcBasepoint,
                                                         CompactPubKey firstPerCommitmentPoint,
                                                         CompactPubKey secondPerCommitmentPoint,
                                                         ChannelFlags channelFlags,
                                                         BitcoinScript? shutdownScriptPubkey = null,
                                                         byte[]? channelType = null,
                                                         bool requireConfirmedInputs = false)
    {
        var maxHtlcValueInFlight =
            LightningMoney.Satoshis(_nodeOptions.AllowUpToPercentageOfChannelFundsInFlight * fundingSatoshis / 100M);

        var payload = new OpenChannel2Payload(_bitcoinNetwork.ChainHash, channelFlags, commitmentFeeRatePerKw,
                                              delayedPaymentBasepoint, _nodeOptions.DustLimitAmount,
                                              firstPerCommitmentPoint, fundingSatoshis, fundingFeeRatePerKw,
                                              fundingPubKey, htlcBasepoint, _nodeOptions.HtlcMinimumAmount,
                                              _nodeOptions.Locktime, _nodeOptions.MaxAcceptedHtlcs,
                                              maxHtlcValueInFlight, paymentBasepoint, revocationBasepoint,
                                              secondPerCommitmentPoint, _nodeOptions.ToSelfDelay, temporaryChannelId);

        return new OpenChannel2Message(payload,
                                       shutdownScriptPubkey is null
                                           ? null
                                           : new UpfrontShutdownScriptTlv(shutdownScriptPubkey.Value),
                                       channelType is null
                                           ? null
                                           : new ChannelTypeTlv(channelType),
                                       requireConfirmedInputs ? new RequireConfirmedInputsTlv() : null);
    }

    /// <summary>
    /// Creates an AcceptChannel1 message.
    /// </summary>
    /// <param name="channelReserveAmount">The reserve amount for the channel.</param>
    /// <param name="channelTypeTlv">Optional parameter specifying the channel type.</param>
    /// <param name="delayedPaymentBasepoint">The basepoint for the delayed payment key.</param>
    /// <param name="firstPerCommitmentPoint">The first per-commitment point for the channel.</param>
    /// <param name="fundingPubKey">Public key associated with the channel funding.</param>
    /// <param name="htlcBasepoint">The basepoint for the HTLC key.</param>
    /// <param name="maxAcceptedHtlcs">The maximum number of HTLCs to be accepted for this channel.</param>
    /// <param name="maxHtlcValueInFlight">The maximum HTLC value that can be in flight.</param>
    /// <param name="minimumDepth">The minimum confirmation depth required for the channel opening transaction.</param>
    /// <param name="paymentBasepoint">The basepoint for the payment key.</param>
    /// <param name="revocationBasepoint">The basepoint for the revocation key.</param>
    /// <param name="temporaryChannelId">The temporary identifier for the channel negotiation.</param>
    /// <param name="toSelfDelay">The delay in blocks before self outputs can be claimed.</param>
    /// <param name="upfrontShutdownScriptTlv">Optional parameter specifying the upfront shutdown script TLV.</param>
    /// <returns>The created AcceptChannel1 message.</returns>
    /// <seealso cref="AcceptChannel1Message"/>
    /// <seealso cref="AcceptChannel1Payload"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="LightningMoney"/>
    /// <seealso cref="CompactPubKey"/>
    /// <seealso cref="UpfrontShutdownScriptTlv"/>
    /// <seealso cref="ChannelTypeTlv"/>
    public AcceptChannel1Message CreateAcceptChannel1Message(LightningMoney channelReserveAmount,
                                                             ChannelTypeTlv? channelTypeTlv,
                                                             CompactPubKey delayedPaymentBasepoint,
                                                             CompactPubKey firstPerCommitmentPoint,
                                                             CompactPubKey fundingPubKey, CompactPubKey htlcBasepoint,
                                                             ushort maxAcceptedHtlcs,
                                                             LightningMoney maxHtlcValueInFlight, uint minimumDepth,
                                                             CompactPubKey paymentBasepoint,
                                                             CompactPubKey revocationBasepoint,
                                                             ChannelId temporaryChannelId, ushort toSelfDelay,
                                                             UpfrontShutdownScriptTlv? upfrontShutdownScriptTlv)
    {
        var payload = new AcceptChannel1Payload(temporaryChannelId, channelReserveAmount, delayedPaymentBasepoint,
                                                _nodeOptions.DustLimitAmount, firstPerCommitmentPoint, fundingPubKey,
                                                htlcBasepoint, _nodeOptions.HtlcMinimumAmount, maxAcceptedHtlcs,
                                                maxHtlcValueInFlight, minimumDepth, paymentBasepoint,
                                                revocationBasepoint, toSelfDelay);

        return new AcceptChannel1Message(payload, upfrontShutdownScriptTlv, channelTypeTlv);
    }

    /// <summary>
    /// Create an AcceptChannel2 message.
    /// </summary>
    /// <param name="temporaryChannelId">The temporary channel id.</param>
    /// <param name="fundingSatoshis">The amount of satoshis we're adding to the channel.</param>
    /// <param name="fundingPubKey">The funding pubkey of the channel.</param>
    /// <param name="revocationBasepoint">The revocation pubkey.</param>
    /// <param name="paymentBasepoint">The payment pubkey.</param>
    /// <param name="delayedPaymentBasepoint">The delayed payment pubkey.</param>
    /// <param name="htlcBasepoint">The htlc pubkey.</param>
    /// <param name="firstPerCommitmentPoint">The first per-commitment pubkey.</param>
    /// <param name="maxHtlcValueInFlight">Maximum HTLC value that can be in flight.</param>
    /// <param name="shutdownScriptPubkey">The shutdown script to be used when closing the channel.</param>
    /// <param name="channelType">The type of the channel.</param>
    /// <param name="requireConfirmedInputs">If we want confirmed inputs to open the channel.</param>
    /// <returns>The AcceptChannel2 message.</returns>
    /// <seealso cref="AcceptChannel2Message"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="CompactPubKey"/>
    /// <seealso cref="BitcoinScript"/>
    /// <seealso cref="AcceptChannel2Payload"/>
    public AcceptChannel2Message CreateAcceptChannel2Message(ChannelId temporaryChannelId,
                                                             LightningMoney fundingSatoshis,
                                                             CompactPubKey fundingPubKey,
                                                             CompactPubKey revocationBasepoint,
                                                             CompactPubKey paymentBasepoint,
                                                             CompactPubKey delayedPaymentBasepoint,
                                                             CompactPubKey htlcBasepoint,
                                                             CompactPubKey firstPerCommitmentPoint,
                                                             LightningMoney maxHtlcValueInFlight,
                                                             BitcoinScript? shutdownScriptPubkey = null,
                                                             byte[]? channelType = null,
                                                             bool requireConfirmedInputs = false)
    {
        var payload = new AcceptChannel2Payload(delayedPaymentBasepoint, _nodeOptions.DustLimitAmount,
                                                firstPerCommitmentPoint, fundingSatoshis, fundingPubKey,
                                                htlcBasepoint, _nodeOptions.HtlcMinimumAmount,
                                                _nodeOptions.MaxAcceptedHtlcs, maxHtlcValueInFlight,
                                                _nodeOptions.MinimumDepth, paymentBasepoint, revocationBasepoint,
                                                temporaryChannelId, _nodeOptions.ToSelfDelay);

        return new AcceptChannel2Message(payload,
                                         shutdownScriptPubkey is null
                                             ? null
                                             : new UpfrontShutdownScriptTlv(shutdownScriptPubkey.Value),
                                         channelType is null
                                             ? null
                                             : new ChannelTypeTlv(channelType),
                                         requireConfirmedInputs ? new RequireConfirmedInputsTlv() : null);
    }

    /// <summary>
    /// Create a FundingCreated message.
    /// </summary>
    /// <param name="temporaryChannelId">The temporary channel id.</param>
    /// <param name="fundingTxId">The funding transaction id.</param>
    /// <param name="fundingOutputIndex">The funding output index.</param>
    /// <param name="signature">The signature for the funding transaction.</param>
    /// <returns>The FundingCreated message.</returns>
    /// <seealso cref="FundingCreatedMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="CompactSignature"/>
    /// <seealso cref="FundingCreatedPayload"/>
    public FundingCreatedMessage CreateFundingCreatedMessage(ChannelId temporaryChannelId, TxId fundingTxId,
                                                             ushort fundingOutputIndex, CompactSignature signature)
    {
        var payload = new FundingCreatedPayload(temporaryChannelId, fundingTxId, fundingOutputIndex, signature);

        return new FundingCreatedMessage(payload);
    }

    /// <summary>
    /// Create a FundingSigned message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="signature"></param>
    /// <returns>The FundingSigned message.</returns>
    /// <seealso cref="FundingCreatedMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="CompactSignature"/>
    /// <seealso cref="FundingCreatedPayload"/>
    public FundingSignedMessage CreateFundingSignedMessage(ChannelId channelId, CompactSignature signature)
    {
        var payload = new FundingSignedPayload(channelId, signature);

        return new FundingSignedMessage(payload);
    }

    #endregion

    #region Commitment

    /// <summary>
    /// Create an UpdateAddHtlc message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="id">The htlc id.</param>
    /// <param name="amountMsat">The amount for this htlc.</param>
    /// <param name="paymentHash">The htlc payment hash.</param>
    /// <param name="cltvExpiry">The cltv expiry.</param>
    /// <param name="onionRoutingPacket">The onion routing packet.</param>
    /// <returns>The UpdateAddHtlc message.</returns>
    /// <seealso cref="UpdateAddHtlcMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="UpdateAddHtlcPayload"/>
    public UpdateAddHtlcMessage CreateUpdateAddHtlcMessage(ChannelId channelId, ulong id, ulong amountMsat,
                                                           ReadOnlyMemory<byte> paymentHash, uint cltvExpiry,
                                                           ReadOnlyMemory<byte>? onionRoutingPacket = null)
    {
        var payload = new UpdateAddHtlcPayload(amountMsat, channelId, cltvExpiry, id, paymentHash, onionRoutingPacket);

        return new UpdateAddHtlcMessage(payload);
    }

    /// <summary>
    /// Create an UpdateFulfillHtlc message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="id">The htlc id.</param>
    /// <param name="preimage">The preimage for this htlc.</param>
    /// <returns>The UpdateFulfillHtlc message.</returns>
    /// <seealso cref="UpdateFulfillHtlcMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="UpdateFulfillHtlcPayload"/>
    public UpdateFulfillHtlcMessage CreateUpdateFulfillHtlcMessage(ChannelId channelId, ulong id,
                                                                   ReadOnlyMemory<byte> preimage)
    {
        var payload = new UpdateFulfillHtlcPayload(channelId, id, preimage);

        return new UpdateFulfillHtlcMessage(payload);
    }

    /// <summary>
    /// Create an UpdateFailHtlc message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="id">The htlc id.</param>
    /// <param name="reason">The reason for failure.</param>
    /// <returns>The UpdateFailHtlc message.</returns>
    /// <seealso cref="UpdateFailHtlcMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="UpdateFailHtlcPayload"/>
    public UpdateFailHtlcMessage CreateUpdateFailHtlcMessage(ChannelId channelId, ulong id, ReadOnlyMemory<byte> reason)
    {
        var payload = new UpdateFailHtlcPayload(channelId, id, reason);

        return new UpdateFailHtlcMessage(payload);
    }

    /// <summary>
    /// Create a CommitmentSigned message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="signature">The signature for the commitment transaction.</param>
    /// <param name="htlcSignatures">The signatures for each open htlc.</param>
    /// <returns>The CommitmentSigned message.</returns>
    /// <seealso cref="CommitmentSignedMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="CompactSignature"/>
    /// <seealso cref="CommitmentSignedPayload"/>
    public CommitmentSignedMessage CreateCommitmentSignedMessage(ChannelId channelId, CompactSignature signature,
                                                                 IEnumerable<CompactSignature> htlcSignatures)
    {
        var payload = new CommitmentSignedPayload(channelId, htlcSignatures, signature);

        return new CommitmentSignedMessage(payload);
    }

    /// <summary>
    /// Create a RevokeAndAck message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="perCommitmentSecret">The secret for the commitment transaction.</param>
    /// <param name="nextPerCommitmentPoint">The next per commitment point.</param>
    /// <returns>The RevokeAndAck message.</returns>
    /// <seealso cref="RevokeAndAckMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="CompactSignature"/>
    /// <seealso cref="RevokeAndAckPayload"/>
    public RevokeAndAckMessage CreateRevokeAndAckMessage(ChannelId channelId, ReadOnlyMemory<byte> perCommitmentSecret,
                                                         CompactPubKey nextPerCommitmentPoint)
    {
        var payload = new RevokeAndAckPayload(channelId, nextPerCommitmentPoint, perCommitmentSecret);

        return new RevokeAndAckMessage(payload);
    }

    /// <summary>
    /// Create a UpdateFee message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="feeratePerKw">The fee rate for the commitment transaction.</param>
    /// <returns>The UpdateFee message.</returns>
    /// <seealso cref="UpdateFeeMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="UpdateFeePayload"/>
    public UpdateFeeMessage CreateUpdateFeeMessage(ChannelId channelId, uint feeratePerKw)
    {
        var payload = new UpdateFeePayload(channelId, feeratePerKw);

        return new UpdateFeeMessage(payload);
    }

    /// <summary>
    /// Create an UpdateFailMalformedHtlc message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="id">The htlc id.</param>
    /// <param name="sha256OfOnion">The sha256OfOnion for this htlc.</param>
    /// <param name="failureCode">The failureCode.</param>
    /// <returns>The UpdateFailMalformedHtlc message.</returns>
    /// <seealso cref="UpdateFailMalformedHtlcMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="UpdateFailMalformedHtlcPayload"/>
    public UpdateFailMalformedHtlcMessage CreateUpdateFailMalformedHtlcMessage(ChannelId channelId, ulong id,
                                                                               ReadOnlyMemory<byte> sha256OfOnion,
                                                                               ushort failureCode)
    {
        var payload = new UpdateFailMalformedHtlcPayload(channelId, failureCode, id, sha256OfOnion);

        return new UpdateFailMalformedHtlcMessage(payload);
    }

    /// <summary>
    /// Create a ChannelReestablish message.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="nextCommitmentNumber">The next commitment number.</param>
    /// <param name="nextRevocationNumber">The next revocation number.</param>
    /// <param name="yourLastPerCommitmentSecret">The peer's last per-commitment secret.</param>
    /// <param name="myCurrentPerCommitmentPoint">Our current per commitment point.</param>
    /// <returns>The ChannelReestablish message.</returns>
    /// <seealso cref="ChannelReestablishMessage"/>
    /// <seealso cref="ChannelId"/>
    /// <seealso cref="ChannelReestablishPayload"/>
    public ChannelReestablishMessage CreateChannelReestablishMessage(ChannelId channelId, ulong nextCommitmentNumber,
                                                                     ulong nextRevocationNumber,
                                                                     ReadOnlyMemory<byte> yourLastPerCommitmentSecret,
                                                                     CompactPubKey myCurrentPerCommitmentPoint)
    {
        var payload = new ChannelReestablishPayload(channelId, myCurrentPerCommitmentPoint, nextCommitmentNumber,
                                                    nextRevocationNumber, yourLastPerCommitmentSecret);

        return new ChannelReestablishMessage(payload);
    }

    #endregion
}