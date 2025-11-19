namespace NLightning.Domain.Bitcoin.Transactions.Factories;

using Bitcoin.Enums;
using Channels.Models;
using Constants;
using Interfaces;
using Models;
using Money;
using Wallet.Models;

public class FundingTransactionModelFactory : IFundingTransactionModelFactory
{
    public FundingTransactionModel Create(ChannelModel channel, List<UtxoModel> utxos,
                                          WalletAddressModel? changeAddress)
    {
        if (utxos.Count == 0)
            throw new ArgumentException("UTXO list cannot be empty", nameof(utxos));

        var fundingOutput = channel.FundingOutput ??
                            throw new NullReferenceException($"{nameof(channel.FundingOutput)} cannot be null");

        // Calculate the total input amount
        var totalInputAmount = LightningMoney.Satoshis(utxos.Sum(u => u.Amount.Satoshi));

        // Calculate the weight based on the input types
        // Starting with base transaction weight
        var weight = WeightConstants.TransactionBaseWeight;

        // Add weight for each input (assuming P2WPKH for now, which is most common)
        foreach (var utxo in utxos)
        {
            if (utxo.AddressType == AddressType.P2Wpkh)
            {
                weight += WeightConstants.P2WpkhInputWeight * 4
                        + WeightConstants.SingleSigWitnessWeight;
            }
            else if (utxo.AddressType == AddressType.P2Tr)
            {
                weight += WeightConstants.P2TrInputWeight * 4
                        + WeightConstants.TaprootSigWitnessWeight;
            }
            else
            {
                throw new NotSupportedException($"Unsupported utxo type {utxo.AddressType}");
            }
        }

        // Add weight for the funding output (P2WSH)
        weight += WeightConstants.P2WshOutputWeight;

        // Calculate fee based on the channel's fee rate
        var fee = LightningMoney.MilliSatoshis(weight * channel.ChannelConfig.FeeRateAmountPerKw.Satoshi);

        // Calculate what's left after funding output and fee
        var fundingAmount = fundingOutput.Amount;
        var remainingAmount = totalInputAmount - fundingAmount - fee;

        // Create the funding transaction model
        var fundingTransactionModel = new FundingTransactionModel(utxos, fundingOutput, fee);

        // If there's a remaining amount, we need a change output
        if (remainingAmount.Satoshi <= 0)
            return fundingTransactionModel;

        // Add change output weight to recalculate fee
        weight += WeightConstants.P2WpkhOutputWeight;
        fee = LightningMoney.MilliSatoshis(weight * channel.ChannelConfig.FeeRateAmountPerKw.Satoshi);

        // Recalculate remaining amount with updated fee
        fundingTransactionModel.ChangeAmount = totalInputAmount - fundingAmount - fee;
        fundingTransactionModel.ChangeAddress = changeAddress ??
                                                throw new ArgumentNullException(
                                                    nameof(changeAddress),
                                                    "We need a change address but none was provided.");

        return fundingTransactionModel;
    }
}