namespace NLightning.Domain.Bitcoin.Transactions.Models;

using Money;
using Outputs;
using ValueObjects;
using Wallet.Models;

/// <summary>
/// Represents a funding transaction in the domain model.
/// This class encapsulates the logical structure of a Lightning Network funding transaction
/// as defined by BOLT specifications, without dependencies on specific Bitcoin libraries.
/// </summary>
public class FundingTransactionModel
{
    /// <summary>
    /// Gets the outputs to be spent by this transaction.
    /// </summary>
    public IEnumerable<UtxoModel> Utxos { get; }

    /// <summary>
    /// Gets the funding output that this transaction pays to.
    /// </summary>
    public FundingOutputInfo FundingOutput { get; }

    /// <summary>
    /// Gets or sets the transaction ID after the transaction is constructed.
    /// </summary>
    public TxId? TransactionId { get; set; }

    /// <summary>
    /// Gets the total fee for this transaction.
    /// </summary>
    public LightningMoney Fee { get; }

    public WalletAddressModel? ChangeAddress { get; set; }
    public LightningMoney? ChangeAmount { get; set; }

    public FundingTransactionModel(IEnumerable<UtxoModel> utxos, FundingOutputInfo fundingOutput, LightningMoney fee)
    {
        Utxos = utxos;
        FundingOutput = fundingOutput;
        Fee = fee;
    }
}