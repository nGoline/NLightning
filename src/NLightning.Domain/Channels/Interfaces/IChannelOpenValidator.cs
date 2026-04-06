namespace NLightning.Domain.Channels.Interfaces;

using Validators.Parameters;

public interface IChannelOpenValidator
{
    /// <summary>
    /// Conducts optional validation checks on channel parameters to ensure compliance with acceptable ranges
    /// and configurations beyond the mandatory requirements.
    /// </summary>
    /// <remarks>
    /// This method verifies that optional configuration parameters meet recommended safety and usability thresholds:
    /// - Validates that the funding amount meets the minimum channel size threshold.
    /// - Checks that the HTLC minimum amount is not excessively large relative to the node's configured minimum value.
    /// - Validates that the maximum HTLC value in flight is enough relative to the channel funds.
    /// - Ensures the channel reserve amount is not excessively high relative to the node's channel reserve configuration.
    /// - Verifies that the maximum number of accepted HTLCs meets a minimum threshold.
    /// - Confirms that the dust limit is not excessively large relative to the node's configured dust limit.
    /// </remarks>
    /// <param name="parameters">The parameters containing the channel's configuration parameters, including funding amount, HTLC limits, and related settings.</param>
    /// <exception cref="Exceptions.ChannelErrorException">
    /// Thrown when one of the optional checks fails, including missing channel type when required, insufficient funding,
    /// excessively high or low HTLC value limits, or incompatible reserve and dust limits.
    /// </exception>
    void PerformOptionalChecks(ChannelOpenOptionalValidationParameters parameters);

    /// <summary>
    /// Enforce mandatory checks when establishing a new Lightning Network channel.
    /// </summary>
    /// <remarks>
    /// The method validates channel parameters to ensure they comply with predefined safety and compatibility checks:
    /// - ChainHash must be compatible with the node's network.
    /// - Push amount must not exceed 1000 times the funding amount.
    /// - To_self_delay must not be unreasonably large compared to the node's configured value.
    /// - Max_accepted_htlcs must not exceed the allowed maximum.
    /// - Fee rate per kw must fall within acceptable limits.
    /// - Dust limit must be lower than or equal to the channel reserve amount and adhere to minimum thresholds.
    /// - Funding amount must be sufficient to cover fees and the channel reserve.
    /// - Large channels must only be supported if negotiated features include support for them.
    /// - Additional validation may apply to channel types based on negotiated options.
    /// </remarks>
    /// <param name="channelTypeTlv">Optional TLV data specifying the channel type, which may impose additional constraints.</param>
    /// <param name="currentFeeRatePerKw">The current network fee rate per kiloweight, used for fee validation.</param>
    /// <param name="negotiatedFeatures">Negotiated feature options between the participating nodes, affecting channel setup constraints.</param>
    /// <param name="payload">The payload containing the channel's configuration parameters and constraints.</param>
    /// <param name="minimumDepth">The minimum number of confirmations required for the channel to be considered operational.</param>
    /// <exception cref="Exceptions.ChannelErrorException">
    /// Thrown when any of the mandatory checks fail, such as invalid chain hash, excessive push amount, unreasonably large delay,
    /// invalid funding amount, unsupported large channel, or mismatched channel type.
    /// </exception>
    void PerformMandatoryChecks(ChannelOpenMandatoryValidationParameters parameters, out uint minimumDepth);
}