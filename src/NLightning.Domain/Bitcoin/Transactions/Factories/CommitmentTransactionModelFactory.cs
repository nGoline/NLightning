using NLightning.Domain.Bitcoin.Interfaces;
using NLightning.Domain.Bitcoin.Transactions.Constants;
using NLightning.Domain.Bitcoin.Transactions.Enums;
using NLightning.Domain.Bitcoin.Transactions.Interfaces;
using NLightning.Domain.Bitcoin.Transactions.Models;
using NLightning.Domain.Bitcoin.Transactions.Outputs;
using NLightning.Domain.Channels.Enums;
using NLightning.Domain.Channels.Models;
using NLightning.Domain.Channels.ValueObjects;
using NLightning.Domain.Exceptions;
using NLightning.Domain.Money;
using NLightning.Domain.Protocol.Interfaces;

namespace NLightning.Domain.Bitcoin.Transactions.Factories;

public class CommitmentTransactionModelFactory : ICommitmentTransactionModelFactory
{
    private readonly ICommitmentKeyDerivationService _commitmentKeyDerivationService;
    private readonly ILightningSigner _lightningSigner;

    public CommitmentTransactionModelFactory(ICommitmentKeyDerivationService commitmentKeyDerivationService,
                                             ILightningSigner lightningSigner)
    {
        _commitmentKeyDerivationService = commitmentKeyDerivationService;
        _lightningSigner = lightningSigner;
    }

    public CommitmentTransactionModel CreateCommitmentTransactionModel(ChannelModel channel, CommitmentSide side)
    {
        // Create base output information
        ToLocalOutputInfo? toLocalOutput = null;
        ToRemoteOutputInfo? toRemoteOutput = null;
        AnchorOutputInfo? localAnchorOutput = null;
        AnchorOutputInfo? remoteAnchorOutput = null;
        var offeredHtlcOutputs = new List<OfferedHtlcOutputInfo>();
        var receivedHtlcOutputs = new List<ReceivedHtlcOutputInfo>();

        // Get the HTLCs based on the commitment side
        var htlcs = new List<Htlc>();
        htlcs.AddRange(channel.LocalOfferedHtlcs?.ToList() ?? []);
        htlcs.AddRange(channel.RemoteOfferedHtlcs?.ToList() ?? []);

        // Get basepoints from the signer instead of the old key set model
        var localBasepoints = _lightningSigner.GetChannelBasepoints(channel.LocalKeySet.KeyIndex);
        var remoteBasepoints = new ChannelBasepoints(channel.RemoteKeySet!.FundingCompactPubKey,
                                                     channel.RemoteKeySet.RevocationCompactBasepoint,
                                                     channel.RemoteKeySet.PaymentCompactBasepoint,
                                                     channel.RemoteKeySet.DelayedPaymentCompactBasepoint,
                                                     channel.RemoteKeySet.HtlcCompactBasepoint);

        // Derive the commitment keys from the appropriate perspective
        var commitmentKeys = side switch
        {
            CommitmentSide.Local => _commitmentKeyDerivationService.DeriveLocalCommitmentKeys(
                channel.LocalKeySet.KeyIndex, localBasepoints, remoteBasepoints,
                channel.LocalKeySet.CurrentPerCommitmentIndex),

            CommitmentSide.Remote => _commitmentKeyDerivationService.DeriveRemoteCommitmentKeys(
                channel.LocalKeySet.KeyIndex, localBasepoints, remoteBasepoints,
                channel.RemoteKeySet.CurrentPerCommitmentCompactPoint, channel.RemoteKeySet.CurrentPerCommitmentIndex),

            _ => throw new ArgumentOutOfRangeException(nameof(side), side,
                                                       "You should use either Local or Remote commitment side.")
        };

        // Calculate base weight
        var weight = WeightConstants.TransactionBaseWeight
                   + TransactionConstants.CommitmentTransactionInputWeight
                     // + htlcs.Count * WeightConstants.HtlcOutputWeight
                   + WeightConstants.P2WshOutputWeight; // To Local Output

        // Set initial amounts for to_local and to_remote outputs
        var toLocalAmount = side == CommitmentSide.Local
                                ? channel.LocalBalance
                                : channel.RemoteBalance;

        var toRemoteAmount = side == CommitmentSide.Local
                                 ? channel.RemoteBalance
                                 : channel.LocalBalance;

        var localDustLimitAmount = side == CommitmentSide.Local
                                       ? channel.ChannelConfig.LocalDustLimitAmount
                                       : channel.ChannelConfig.RemoteDustLimitAmount;

        var remoteDustLimitAmount = side == CommitmentSide.Local
                                        ? channel.ChannelConfig.RemoteDustLimitAmount
                                        : channel.ChannelConfig.LocalDustLimitAmount;

        if (htlcs is { Count: > 0 })
        {
            // Calculate htlc weight and fee
            var offeredHtlcWeight = channel.ChannelConfig.OptionAnchorOutputs
                                        ? WeightConstants.HtlcTimeoutWeightAnchors
                                        : WeightConstants.HtlcTimeoutWeightNoAnchors;
            var offeredHtlcFee =
                LightningMoney.MilliSatoshis(offeredHtlcWeight * channel.ChannelConfig.FeeRateAmountPerKw.Satoshi);

            var receivedHtlcWeight = channel.ChannelConfig.OptionAnchorOutputs
                                         ? WeightConstants.HtlcSuccessWeightAnchors
                                         : WeightConstants.HtlcSuccessWeightNoAnchors;
            var receivedHtlcFee =
                LightningMoney.MilliSatoshis(receivedHtlcWeight * channel.ChannelConfig.FeeRateAmountPerKw.Satoshi);

            foreach (var htlc in htlcs)
            {
                // Determine if this is an offered or received HTLC from the perspective of the commitment holder
                var isOffered = side == CommitmentSide.Local
                                    ? htlc.Direction == HtlcDirection.Outgoing
                                    : htlc.Direction == HtlcDirection.Incoming;

                // Calculate the amounts after subtracting fees
                var htlcFee = isOffered ? offeredHtlcFee : receivedHtlcFee;
                var htlcAmount = htlc.Amount.Satoshi > htlcFee.Satoshi
                                     ? LightningMoney.Satoshis(htlc.Amount.Satoshi - htlcFee.Satoshi)
                                     : LightningMoney.Zero;

                // Always subtract the full HTLC amount from to_local
                toLocalAmount = toLocalAmount > htlc.Amount
                                    ? toLocalAmount - htlc.Amount
                                    : LightningMoney.Zero; // If not enough, set to zero

                // Offered or received depends on dust check
                if (htlcAmount.Satoshi < localDustLimitAmount.Satoshi)
                    continue;

                weight += WeightConstants.HtlcOutputWeight;
                if (isOffered)
                {
                    offeredHtlcOutputs.Add(new OfferedHtlcOutputInfo(
                                               htlc,
                                               commitmentKeys.LocalHtlcPubKey,
                                               commitmentKeys.RemoteHtlcPubKey,
                                               commitmentKeys.RevocationPubKey));
                }
                else
                {
                    receivedHtlcOutputs.Add(new ReceivedHtlcOutputInfo(
                                                htlc,
                                                commitmentKeys.LocalHtlcPubKey,
                                                commitmentKeys.RemoteHtlcPubKey,
                                                commitmentKeys.RevocationPubKey));
                }
            }
        }

        LightningMoney fee;
        // Create anchor outputs if option_anchors is negotiated
        if (channel.ChannelConfig.OptionAnchorOutputs)
        {
            localAnchorOutput = new AnchorOutputInfo(channel.LocalKeySet.FundingCompactPubKey, true);
            remoteAnchorOutput = new AnchorOutputInfo(channel.RemoteKeySet.FundingCompactPubKey, false);

            weight += WeightConstants.AnchorOutputWeight * 2
                    + WeightConstants.P2WshOutputWeight; // Add ToRemote Output weight
            fee = LightningMoney.MilliSatoshis(weight * channel.ChannelConfig.FeeRateAmountPerKw.Satoshi);

            ref var feePayerAmount =
                ref GetFeePayerAmount(side, channel.IsInitiator, ref toLocalAmount, ref toRemoteAmount);
            AdjustForAnchorOutputs(ref feePayerAmount, fee, TransactionConstants.AnchorOutputAmount);
        }
        else
        {
            weight += WeightConstants.P2WpkhOutputWeight; // Add ToRemote Output weight
            fee = LightningMoney.MilliSatoshis(weight * channel.ChannelConfig.FeeRateAmountPerKw.Satoshi);

            ref var feePayerAmount =
                ref GetFeePayerAmount(side, channel.IsInitiator, ref toLocalAmount, ref toRemoteAmount);

            // Simple fee deduction when no anchors
            feePayerAmount = feePayerAmount.Satoshi > fee.Satoshi
                                 ? LightningMoney.Satoshis(feePayerAmount.Satoshi - fee.Satoshi)
                                 : LightningMoney.Zero;
        }

        // Fail if both amounts are below ChannelReserve
        if (channel.ChannelConfig.ChannelReserveAmount is not null
         && toLocalAmount.Satoshi < channel.ChannelConfig.ChannelReserveAmount.Satoshi
         && toRemoteAmount.Satoshi < channel.ChannelConfig.ChannelReserveAmount.Satoshi)
            throw new ChannelErrorException("Both to_local and to_remote amounts are below the reserve limits.");

        // Only create output if the amount is above the dust limit
        if (toLocalAmount.Satoshi >= localDustLimitAmount.Satoshi)
        {
            toLocalOutput = new ToLocalOutputInfo(toLocalAmount, commitmentKeys.LocalDelayedPubKey,
                                                  commitmentKeys.RevocationPubKey,
                                                  channel.ChannelConfig.ToSelfDelay);
        }

        if (toRemoteAmount.Satoshi >= remoteDustLimitAmount.Satoshi)
        {
            var remotePubKey = side == CommitmentSide.Local
                                   ? channel.RemoteKeySet.PaymentCompactBasepoint
                                   : channel.LocalKeySet.PaymentCompactBasepoint;

            toRemoteOutput =
                new ToRemoteOutputInfo(toRemoteAmount, remotePubKey, channel.ChannelConfig.OptionAnchorOutputs);
        }

        if (offeredHtlcOutputs.Count == 0 && receivedHtlcOutputs.Count == 0)
        {
            // If no HTLCs and no to_local, we can remove our anchor output
            if (toLocalOutput is null)
                localAnchorOutput = null;

            // If no HTLCs and no to_remote, we can remove their anchor output
            if (toRemoteOutput is null)
                remoteAnchorOutput = null;
        }

        // Create and return the commitment transaction model
        return new CommitmentTransactionModel(channel.CommitmentNumber!, fee, channel.FundingOutput!,
                                              localAnchorOutput, remoteAnchorOutput, toLocalOutput, toRemoteOutput,
                                              offeredHtlcOutputs, receivedHtlcOutputs);
    }

    private static ref LightningMoney GetFeePayerAmount(CommitmentSide side, bool isInitiator,
                                                        ref LightningMoney toLocal,
                                                        ref LightningMoney toRemote)
    {
        // If we're the initiator, and it's our tx, deduct from toLocal
        // If not initiator and our tx, deduct from toRemote
        // For remote tx, logic is reversed
        if ((side == CommitmentSide.Local && isInitiator) || (side == CommitmentSide.Remote && !isInitiator))
            return ref toLocal;

        return ref toRemote;
    }

    private static void AdjustForAnchorOutputs(ref LightningMoney amount, LightningMoney fee,
                                               LightningMoney anchorAmount)
    {
        if (amount > fee)
        {
            amount -= fee;
            amount = amount > anchorAmount
                         ? amount - anchorAmount
                         : LightningMoney.Zero;
        }
        else
            amount = LightningMoney.Zero;
    }
}