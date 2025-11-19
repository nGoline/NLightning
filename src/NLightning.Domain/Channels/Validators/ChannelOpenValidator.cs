namespace NLightning.Domain.Channels.Validators;

using Bitcoin.Transactions.Constants;
using Constants;
using Domain.Enums;
using Exceptions;
using Interfaces;
using Money;
using Node.Options;
using Parameters;

public class ChannelOpenValidator : IChannelOpenValidator
{
    private readonly NodeOptions _nodeOptions;

    public ChannelOpenValidator(NodeOptions nodeOptions)
    {
        _nodeOptions = nodeOptions;
    }

    /// <inheritdoc/> 
    public void PerformOptionalChecks(ChannelOpenOptionalValidationParameters parameters)
    {
        // Check if Funding Satoshis is too small
        if (parameters.FundingAmount is not null && parameters.FundingAmount < _nodeOptions.MinimumChannelSize)
            throw new ChannelErrorException($"Funding amount is too small: {parameters.FundingAmount}");

        // Check if we consider htlc_minimum_msat too large. IE. 20% bigger than our htlc minimum amount
        if (parameters.HtlcMinimumAmount is not null
         && parameters.HtlcMinimumAmount > _nodeOptions.HtlcMinimumAmount * 1.2M)
            throw new ChannelErrorException($"Htlc minimum amount is too large: {parameters.HtlcMinimumAmount}");

        // Check if we consider max_htlc_value_in_flight_msat too small. IE. 20% smaller than our maximum htlc value
        if (parameters.FundingAmount is not null && parameters.MaxHtlcValueInFlight is not null)
        {
            var ourMaxHtlcValueInFlight =
                LightningMoney.Satoshis(_nodeOptions.AllowUpToPercentageOfChannelFundsInFlight *
                                        parameters.FundingAmount.Satoshi / 100M);
            if (parameters.MaxHtlcValueInFlight < ourMaxHtlcValueInFlight * 0.8M)
                throw new ChannelErrorException(
                    $"Max htlc value in flight is too small: {parameters.MaxHtlcValueInFlight}");
        }

        // Check if we consider channel_reserve_satoshis too large. IE. 20% bigger than our 1% channel reserve
        if (parameters.ChannelReserveAmount > parameters.OurChannelReserveAmount * 1.2M)
            throw new ChannelErrorException($"Channel reserve amount is too large: {parameters.ChannelReserveAmount}");

        // Check if we consider max_accepted_htlcs too small. IE. 20% smaller than our max-accepted htlcs
        if (parameters.MaxAcceptedHtlcs < (ushort)(_nodeOptions.MaxAcceptedHtlcs * 0.8M))
            throw new ChannelErrorException($"Max accepted htlcs is too small: {parameters.MaxAcceptedHtlcs}");

        // Check if we consider dust_limit_satoshis too large. IE. 75% bigger than our dust limit
        if (parameters.DustLimitAmount > _nodeOptions.DustLimitAmount * 1.75M)
            throw new ChannelErrorException($"Dust limit amount is too large: {parameters.DustLimitAmount}");
    }

    /// <inheritdoc/> 
    public void PerformMandatoryChecks(ChannelOpenMandatoryValidationParameters parameters,
                                       out uint minimumDepth)
    {
        // Check if ChainHash is compatible
        if (parameters.ChainHash is not null && parameters.ChainHash != _nodeOptions.BitcoinNetwork.ChainHash)
            throw new ChannelErrorException("ChainHash is not compatible");

        // Check if we consider to_self_delay unreasonably large. IE. 50% bigger than our to_self_delay
        if (parameters.ToSelfDelay > _nodeOptions.ToSelfDelay * 1.5M)
            throw new ChannelErrorException($"To self delay is too large: {parameters.ToSelfDelay}");

        // Check max_accepted_htlcs is too large
        if (parameters.MaxAcceptedHtlcs > ChannelConstants.MaxAcceptedHtlcs)
            throw new ChannelErrorException($"Max accepted htlcs is too small: {parameters.MaxAcceptedHtlcs}");

        if (parameters.FeeRatePerKw is not null)
        {
            // Check if we consider fee_rate_per_kw too large
            if (parameters.FeeRatePerKw > ChannelConstants.MaxFeePerKw)
                throw new ChannelErrorException($"Fee rate per kw is too large: {parameters.FeeRatePerKw}");

            // Check if we consider fee_rate_per_kw too small. IE. 20% smaller than our fee rate
            if (parameters.FeeRatePerKw < ChannelConstants.MinFeePerKw ||
                parameters.FeeRatePerKw < parameters.CurrentFeeRatePerKw * 0.8M)
                throw new ChannelErrorException(
                    $"Fee rate per kw is too small: {parameters.FeeRatePerKw}, currentFee{parameters.CurrentFeeRatePerKw}");
        }

        // Check if the dust limit is greater than the channel reserve amount 
        if (parameters.DustLimitAmount > parameters.ChannelReserveAmount)
            throw new ChannelErrorException(
                $"Dust limit({parameters.DustLimitAmount}) is greater than channel reserve({parameters.ChannelReserveAmount})");

        // Check if dust_limit_satoshis is too small
        if (parameters.DustLimitAmount < ChannelConstants.MinDustLimitAmount)
            throw new ChannelErrorException($"Dust limit amount is too small: {parameters.DustLimitAmount}");

        if (parameters.FundingAmount is not null)
        {
            // Check if the push amount is too large
            if (parameters.PushAmount is not null
             && parameters.PushAmount > 1_000 * parameters.FundingAmount)
                throw new ChannelErrorException($"Push amount is too large: {parameters.PushAmount}");

            // Check if there are enough funds to pay for fees
            var expectedWeight = parameters.NegotiatedFeatures.AnchorOutputs > FeatureSupport.No
                                     ? TransactionConstants.InitialCommitmentTransactionWeightNoAnchor
                                     : TransactionConstants.InitialCommitmentTransactionWeightWithAnchor;
            var expectedFee = LightningMoney.Satoshis(expectedWeight * parameters.CurrentFeeRatePerKw.Satoshi / 1000);
            if (parameters.FundingAmount < expectedFee + parameters.ChannelReserveAmount)
                throw new ChannelErrorException(
                    $"Funding amount is too small to cover fees: {parameters.FundingAmount}");

            // Check if this is a large channel and if we support it
            if (parameters.FundingAmount >= ChannelConstants.LargeChannelAmount &&
                parameters.NegotiatedFeatures.LargeChannels == FeatureSupport.No)
                throw new ChannelErrorException("We don't support large channels");
        }

        // Check ChannelType against negotiated options
        minimumDepth = _nodeOptions.MinimumDepth;
        if (parameters.ChannelTypeTlv is not null)
        {
            // Check if it set any non-negotiated features
            if (parameters.ChannelTypeTlv.Features.IsFeatureSet(Feature.OptionStaticRemoteKey, true))
            {
                if (parameters.NegotiatedFeatures.StaticRemoteKey == FeatureSupport.No)
                    throw new ChannelErrorException("Static remote key feature is not supported but requested by peer");

                if (parameters.ChannelTypeTlv.Features.IsFeatureSet(Feature.OptionAnchorOutputs, true)
                 && parameters.NegotiatedFeatures.AnchorOutputs == FeatureSupport.No)
                    throw new ChannelErrorException("Anchor outputs feature is not supported but requested by peer");

                if (parameters.ChannelTypeTlv.Features.IsFeatureSet(Feature.OptionScidAlias, true))
                {
                    if (parameters.ChannelFlags is not null && parameters.ChannelFlags.Value.AnnounceChannel)
                        throw new ChannelErrorException("Invalid channel flags for OPTION_SCID_ALIAS");
                }

                // Check for ZeroConf feature
                if (parameters.ChannelTypeTlv.Features.IsFeatureSet(Feature.OptionZeroconf, true))
                {
                    if (_nodeOptions.Features.ZeroConf == FeatureSupport.No)
                        throw new ChannelErrorException("ZeroConf feature not supported but requested by peer");

                    minimumDepth = 0U;
                }
            }
        }
    }
}