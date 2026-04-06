// using NBitcoin;
//
// namespace NLightning.Infrastructure.Bitcoin.Transactions;
//
// using Domain.Money;
// using Domain.Protocol.Constants;
// using Outputs;
//
// /// <summary>
// /// Represents a funding transaction.
// /// </summary>
// public class FundingTransaction : BaseTransaction
// {
//     private readonly LightningMoney _dustLimitAmount;
//     public FundingOutput FundingOutput { get; }
//     public ChangeOutput ChangeOutput { get; }
//
//     /// <summary>
//     /// Initializes a new instance of the <see cref="FundingTransaction"/> class.
//     /// </summary>
//     /// <param name="dustLimitAmount">The dust limit amount.</param>
//     /// <param name="hasAnchorOutput">Indicates if the transaction has an anchor output.</param>
//     /// <param name="network">The network type.</param>
//     /// <param name="pubkey1">The first public key in compressed format.</param>
//     /// <param name="pubkey2">The second public key in compressed format.</param>
//     /// <param name="amountSats">The amount of the output in satoshis.</param>
//     /// <param name="changeScript">The script for the change output.</param>
//     /// <param name="coins">The coins to be used in the transaction.</param>
//     internal FundingTransaction(LightningMoney dustLimitAmount, bool hasAnchorOutput, Network network, PubKey pubkey1,
//                                 PubKey pubkey2, LightningMoney amountSats, Script changeScript, params Coin[] coins)
//         : base(hasAnchorOutput, network, TransactionConstants.FundingTransactionVersion, SigHash.All, coins)
//     {
//         ArgumentNullException.ThrowIfNull(pubkey1);
//         ArgumentNullException.ThrowIfNull(pubkey2);
//
//         if (pubkey1 == pubkey2)
//             throw new ArgumentException("Public keys must be different.");
//
//         if (amountSats.IsZero)
//             throw new ArgumentException("Funding amount must be greater than zero.");
//
//         _dustLimitAmount = dustLimitAmount;
//
//         // Create the funding and change output
//         FundingOutput = new FundingOutput(amountSats, pubkey1, pubkey2);
//         ChangeOutput = new ChangeOutput(changeScript);
//
//         AddOutput(FundingOutput);
//         AddOutput(ChangeOutput);
//     }
//
//     internal FundingTransaction(LightningMoney dustLimitAmount, bool hasAnchorOutput, Network network, PubKey pubkey1,
//                                 PubKey pubkey2, LightningMoney amountSats, Script redeemScript, Script changeScript,
//                                 params Coin[] coins)
//         : base(hasAnchorOutput, network, TransactionConstants.FundingTransactionVersion, SigHash.All, coins)
//     {
//         ArgumentNullException.ThrowIfNull(pubkey1);
//         ArgumentNullException.ThrowIfNull(pubkey2);
//
//         if (pubkey1 == pubkey2)
//             throw new ArgumentException("Public keys must be different.");
//
//         if (amountSats.IsZero)
//             throw new ArgumentException("Funding amount must be greater than zero.");
//
//         _dustLimitAmount = dustLimitAmount;
//
//         // Create the funding and change output
//         FundingOutput = new FundingOutput(amountSats, pubkey1, pubkey2);
//         ChangeOutput = new ChangeOutput(redeemScript, changeScript);
//
//         AddOutput(FundingOutput);
//         AddOutput(ChangeOutput);
//     }
//
//     internal override void ConstructTransaction(LightningMoney currentFeePerKw)
//     {
//         // Calculate transaction fee
//         CalculateTransactionFee(currentFeePerKw);
//
//         // Remove the old change output (zero value)
//         RemoveOutput(ChangeOutput);
//
//         // Check if change is needed
//         var changeAmount = TotalInputAmount - TotalOutputAmount - CalculatedFee;
//         var hasChange = changeAmount >= _dustLimitAmount;
//         if (hasChange)
//         {
//             // Add the new one
//             ChangeOutput.Amount = changeAmount;
//             AddOutput(ChangeOutput);
//         }
//         else
//         {
//             ChangeOutput.Amount = LightningMoney.Zero;
//         }
//
//         // Order Outputs
//         AddOrderedOutputsToTransaction();
//
//         var changeIndex = Outputs.IndexOf(ChangeOutput);
//
//         FundingOutput.Index = hasChange
//                                   ? changeIndex == 0
//                                         ? 1
//                                         : 0
//                                   : 0;
//
//         if (hasChange)
//         {
//             // Set change output fields
//             ChangeOutput.Index = changeIndex;
//         }
//     }
//
//     internal new void SignTransaction(ILightningSigner signer, params BitcoinSecret[] secrets)
//     {
//         base.SignTransaction(signer, secrets);
//         // Set funding output fields
//         FundingOutput.TxId = TxId;
//
//         if (!ChangeOutput.Amount.IsZero)
//         {
//             // Set change output fields
//             ChangeOutput.TxId = TxId;
//         }
//     }
//
//     public Transaction GetSignedTransaction()
//     {
//         if (Finalized)
//         {
//             return FinalizedTransaction;
//         }
//
//         throw new InvalidOperationException("You have to sign and finalize the transaction first.");
//     }
// }