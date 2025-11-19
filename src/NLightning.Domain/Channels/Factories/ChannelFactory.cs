namespace NLightning.Domain.Channels.Factories;

using Bitcoin.Interfaces;
using Bitcoin.Transactions.Constants;
using Bitcoin.Transactions.Outputs;
using Bitcoin.ValueObjects;
using Client.Requests;
using Constants;
using Crypto.Hashes;
using Crypto.ValueObjects;
using Domain.Enums;
using Enums;
using Exceptions;
using Interfaces;
using Models;
using Money;
using Node.Options;
using Protocol.Interfaces;
using Protocol.Messages;
using Protocol.Models;
using Validators.Parameters;
using ValueObjects;

public class ChannelFactory : IChannelFactory
{
    private readonly IChannelIdFactory _channelIdFactory;
    private readonly IChannelOpenValidator _channelOpenValidator;
    private readonly IFeeService _feeService;
    private readonly ILightningSigner _lightningSigner;
    private readonly NodeOptions _nodeOptions;
    private readonly ISha256 _sha256;

    public ChannelFactory(IChannelIdFactory channelIdFactory, IChannelOpenValidator channelOpenValidator,
                          IFeeService feeService, ILightningSigner lightningSigner, NodeOptions nodeOptions,
                          ISha256 sha256)
    {
        _channelIdFactory = channelIdFactory;
        _channelOpenValidator = channelOpenValidator;
        _feeService = feeService;
        _lightningSigner = lightningSigner;
        _nodeOptions = nodeOptions;
        _sha256 = sha256;
    }

    public async Task<ChannelModel> CreateChannelV1AsNonInitiatorAsync(OpenChannel1Message message,
                                                                       FeatureOptions negotiatedFeatures,
                                                                       CompactPubKey remoteNodeId)
    {
        var payload = message.Payload;

        // If dual fund is negotiated fail the channel
        if (negotiatedFeatures.DualFund == FeatureSupport.Compulsory)
            throw new ChannelErrorException("We can only accept dual fund channels");

        // Check if the channel type was negotiated and the channel type is present
        if (message.ChannelTypeTlv is not null && negotiatedFeatures.ChannelType == FeatureSupport.Compulsory)
            throw new ChannelErrorException("Channel type was negotiated but not provided");

        // Perform optional checks for the channel
        var ourChannelReserveAmount = GetOurChannelReserveFromFundingAmount(payload.FundingAmount);
        _channelOpenValidator.PerformOptionalChecks(
            ChannelOpenOptionalValidationParameters.FromOpenChannel1Payload(payload, ourChannelReserveAmount));

        // Perform mandatory checks for the channel
        var currentFee = await _feeService.GetFeeRatePerKwAsync();
        _channelOpenValidator.PerformMandatoryChecks(
            ChannelOpenMandatoryValidationParameters.FromOpenChannel1Payload(
                message.ChannelTypeTlv, currentFee, negotiatedFeatures, payload), out var minimumDepth);

        // Check for the upfront shutdown script
        if (message.UpfrontShutdownScriptTlv is null
         && (negotiatedFeatures.UpfrontShutdownScript > FeatureSupport.No || message.ChannelTypeTlv is not null))
            throw new ChannelErrorException("Upfront shutdown script is required but not provided");

        BitcoinScript? remoteUpfrontShutdownScript = null;
        if (message.UpfrontShutdownScriptTlv is not null && message.UpfrontShutdownScriptTlv.Value.Length > 0)
            remoteUpfrontShutdownScript = message.UpfrontShutdownScriptTlv.Value;

        // Calculate the amounts
        var toLocalAmount = payload.PushAmount;
        var toRemoteAmount = payload.FundingAmount - payload.PushAmount;

        // Generate local keys through the signer
        var localKeyIndex = _lightningSigner.CreateNewChannel(out var localBasepoints, out var firstPerCommitmentPoint);

        // Create the local key set
        var localKeySet = new ChannelKeySetModel(localKeyIndex, localBasepoints.FundingPubKey,
                                                 localBasepoints.RevocationBasepoint, localBasepoints.PaymentBasepoint,
                                                 localBasepoints.DelayedPaymentBasepoint, localBasepoints.HtlcBasepoint,
                                                 firstPerCommitmentPoint);

        // Create the remote key set from the message
        var remoteKeySet = ChannelKeySetModel.CreateForRemote(message.Payload.FundingPubKey,
                                                              message.Payload.RevocationBasepoint,
                                                              message.Payload.PaymentBasepoint,
                                                              message.Payload.DelayedPaymentBasepoint,
                                                              message.Payload.HtlcBasepoint,
                                                              message.Payload.FirstPerCommitmentPoint);

        BitcoinScript? localUpfrontShutdownScript = null;
        // Generate our upfront shutdown script
        if (_nodeOptions.Features.UpfrontShutdownScript > FeatureSupport.No)
        {
            // Generate our upfront shutdown script
            // TODO: Generate a script from the local key set
            // localUpfrontShutdownScript = ;
        }

        // Generate the channel configuration
        var useScidAlias = FeatureSupport.No;
        if (negotiatedFeatures.ScidAlias > FeatureSupport.No)
        {
            if (message.ChannelTypeTlv?.Features.IsFeatureSet(Feature.OptionScidAlias, true) ?? false)
                useScidAlias = FeatureSupport.Compulsory;
            else
                useScidAlias = FeatureSupport.Optional;
        }

        var channelConfig = new ChannelConfig(payload.ChannelReserveAmount, payload.FeeRatePerKw,
                                              payload.HtlcMinimumAmount, _nodeOptions.DustLimitAmount,
                                              payload.MaxAcceptedHtlcs, payload.MaxHtlcValueInFlight, minimumDepth,
                                              negotiatedFeatures.AnchorOutputs != FeatureSupport.No,
                                              payload.DustLimitAmount, payload.ToSelfDelay, useScidAlias,
                                              localUpfrontShutdownScript, remoteUpfrontShutdownScript);

        // Generate the commitment number
        var commitmentNumber = new CommitmentNumber(remoteKeySet.PaymentCompactBasepoint,
                                                    localKeySet.PaymentCompactBasepoint, _sha256);

        try
        {
            var fundingOutput = new FundingOutputInfo(payload.FundingAmount, localKeySet.FundingCompactPubKey,
                                                      remoteKeySet.FundingCompactPubKey);

            // Create the channel
            return new ChannelModel(channelConfig, payload.ChannelId, commitmentNumber, fundingOutput, false, null,
                                    null, toLocalAmount, localKeySet, 1, 0, toRemoteAmount, remoteKeySet, 1,
                                    remoteNodeId, 0, ChannelState.V1Opening, ChannelVersion.V1);
        }
        catch (Exception e)
        {
            throw new ChannelErrorException("Error creating commitment transaction", e);
        }
    }

    public async Task<ChannelModel> CreateChannelV1AsInitiatorAsync(OpenChannelClientRequest request,
                                                                    FeatureOptions negotiatedFeatures,
                                                                    CompactPubKey remoteNodeId)
    {
        // If dual fund is negotiated fail the channel
        if (negotiatedFeatures.DualFund == FeatureSupport.Compulsory)
            throw new ChannelErrorException("We can only open dual fund channels to this peer");

        // Check if the FundingAmount is too small
        if (request.FundingAmount < _nodeOptions.MinimumChannelSize)
            throw new ChannelErrorException(
                $"Funding amount is smaller than our MinimumChannelSize: {request.FundingAmount} < {_nodeOptions.MinimumChannelSize}");

        // Check if our fee is too big
        if (request.FeeRatePerKw is not null && request.FeeRatePerKw > ChannelConstants.MaxFeePerKw)
            throw new ChannelErrorException($"Fee rate per kw is too large: {request.FeeRatePerKw}");

        // Check if the dust limit is greater than the channel reserve amount
        var channelReserveAmount = GetOurChannelReserveFromFundingAmount(request.FundingAmount);
        if (request.ChannelReserveAmount is not null && request.ChannelReserveAmount > channelReserveAmount)
            channelReserveAmount = request.ChannelReserveAmount;

        if (request.DustLimitAmount is not null)
        {
            if (request.DustLimitAmount > channelReserveAmount)
                throw new ChannelErrorException(
                    $"Dust limit({request.DustLimitAmount}) is greater than channel reserve({channelReserveAmount})");

            // Check if dust_limit_satoshis is too small
            if (request.DustLimitAmount < ChannelConstants.MinDustLimitAmount)
                throw new ChannelErrorException($"Dust limit amount is too small: {request.DustLimitAmount}");
        }

        // Check if there are enough funds to pay for fees
        var currentFeeRatePerKw = await _feeService.GetFeeRatePerKwAsync();
        var expectedWeight = negotiatedFeatures.AnchorOutputs > FeatureSupport.No
                                 ? TransactionConstants.InitialCommitmentTransactionWeightNoAnchor
                                 : TransactionConstants.InitialCommitmentTransactionWeightWithAnchor;
        var expectedFee = LightningMoney.Satoshis(expectedWeight * currentFeeRatePerKw.Satoshi / 1000);
        if (request.FundingAmount < expectedFee + channelReserveAmount)
            throw new ChannelErrorException($"Funding amount is too small to cover fees: {request.FundingAmount}");

        // Check if this is a large channel and if we support it
        if (request.FundingAmount >= ChannelConstants.LargeChannelAmount &&
            negotiatedFeatures.LargeChannels == FeatureSupport.No)
            throw new ChannelErrorException("The peer don't support large channels");

        // Check if we want zeroconf and if it's negotiated
        var minimumDepth = _nodeOptions.MinimumDepth;
        if (request.IsZeroConfChannel)
        {
            if (_nodeOptions.Features.ZeroConf == FeatureSupport.No)
                throw new ChannelErrorException(
                    "ZeroConf feature not supported, change our configuration and try again");

            if (negotiatedFeatures.ZeroConf == FeatureSupport.No)
                throw new ChannelErrorException("ZeroConf not supported by our peer");

            minimumDepth = 0U;
        }

        // Calculate the amounts
        var toRemoteAmount = request.PushAmount ?? LightningMoney.Zero;
        var toLocalAmount = request.FundingAmount - toRemoteAmount;

        // Generate our MaxHtlcValueInFlight if not provided
        var maxHtlcValueInFlight = request.MaxHtlcValueInFlight
                                ?? LightningMoney.Satoshis(_nodeOptions.AllowUpToPercentageOfChannelFundsInFlight *
                                                           request.FundingAmount.Satoshi / 100M);

        // Generate local keys through the signer
        var localKeyIndex = _lightningSigner.CreateNewChannel(out var localBasepoints, out var firstPerCommitmentPoint);

        // Create the local key set
        var localKeySet = new ChannelKeySetModel(localKeyIndex, localBasepoints.FundingPubKey,
                                                 localBasepoints.RevocationBasepoint, localBasepoints.PaymentBasepoint,
                                                 localBasepoints.DelayedPaymentBasepoint, localBasepoints.HtlcBasepoint,
                                                 firstPerCommitmentPoint);

        BitcoinScript? localUpfrontShutdownScript = null;
        // Generate our upfront shutdown script
        if (negotiatedFeatures.UpfrontShutdownScript == FeatureSupport.Compulsory)
            throw new ChannelErrorException("Upfront shutdown script is compulsory but we are not able to send it");

        if (_nodeOptions.Features.UpfrontShutdownScript > FeatureSupport.No)
        {
            // Generate our upfront shutdown script
            // TODO: Generate a script from the local key set
            // localUpfrontShutdownScript = ;
        }

        // Generate the channel configuration
        var channelConfig = new ChannelConfig(channelReserveAmount, request.FeeRatePerKw ?? currentFeeRatePerKw,
                                              request.HtlcMinimumAmount ?? _nodeOptions.HtlcMinimumAmount,
                                              request.DustLimitAmount ?? _nodeOptions.DustLimitAmount,
                                              request.MaxAcceptedHtlcs ?? _nodeOptions.MaxAcceptedHtlcs,
                                              maxHtlcValueInFlight, minimumDepth,
                                              negotiatedFeatures.AnchorOutputs != FeatureSupport.No,
                                              LightningMoney.Zero, request.ToSelfDelay ?? _nodeOptions.ToSelfDelay,
                                              negotiatedFeatures.ScidAlias, localUpfrontShutdownScript);

        try
        {
            // Create the channel using only our data
            return new ChannelModel(channelConfig, _channelIdFactory.CreateTemporaryChannelId(), null,
                                    null, true, null, null, toLocalAmount, localKeySet, 1, 0, toRemoteAmount,
                                    null, 1, remoteNodeId, 0, ChannelState.V1Opening, ChannelVersion.V1);
        }
        catch (Exception e)
        {
            throw new ChannelErrorException("Error creating commitment transaction", e);
        }
    }

    private LightningMoney GetOurChannelReserveFromFundingAmount(LightningMoney fundingAmount)
    {
        return fundingAmount * 0.01M;
    }
}