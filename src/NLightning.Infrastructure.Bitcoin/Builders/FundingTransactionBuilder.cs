using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;

namespace NLightning.Infrastructure.Bitcoin.Builders;

using Domain.Bitcoin.Transactions.Constants;
using Domain.Bitcoin.Transactions.Models;
using Domain.Bitcoin.ValueObjects;
using Domain.Node.Options;
using Interfaces;
using Outputs;

public class FundingTransactionBuilder : IFundingTransactionBuilder
{
    private readonly Network _network;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FundingTransactionBuilder> _logger;

    public FundingTransactionBuilder(IOptions<NodeOptions> nodeOptions, IServiceProvider serviceProvider,
                                     ILogger<FundingTransactionBuilder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _network = Network.GetNetwork(nodeOptions.Value.BitcoinNetwork) ??
                   throw new ArgumentException("Invalid Bitcoin network specified", nameof(nodeOptions));
    }

    public SignedTransaction Build(FundingTransactionModel transaction)
    {
        var coins = transaction.Utxos.ToArray();
        if (coins.Length == 0)
            throw new ArgumentException("UTXO set cannot be empty");

        var totalInputAmount = coins.Sum(x => x.Amount);

        _logger.LogTrace("Building funding transaction with {UtxoCount} UTXOs for amount {FundingAmount}",
                         coins.Length, transaction.FundingOutput.Amount);

        // Create a new Bitcoin transaction
        var tx = Transaction.Create(_network);

        // Set the transaction version as per BOLT spec
        tx.Version = TransactionConstants.FundingTransactionVersion;

        // Add all inputs from the UTXO set
        foreach (var coin in coins)
            tx.Inputs.Add(new OutPoint(new uint256(coin.TxId), coin.Index));

        // Convert and add the funding output
        var fundingOutput = new FundingOutput(transaction.FundingOutput.Amount,
                                              new PubKey(transaction.FundingOutput.LocalFundingPubKey),
                                              new PubKey(transaction.FundingOutput.RemoteFundingPubKey));
        tx.Outputs.Add(fundingOutput.ToTxOut());

        // Check if we are paying a change address
        if (transaction.ChangeAddress is not null)
        {
            var changeAmount = totalInputAmount - transaction.Fee - fundingOutput.Amount;
            tx.Outputs.Add(new TxOut(new Money(changeAmount.Satoshi),
                                     _network.CreateBitcoinAddress(transaction.ChangeAddress.Address)));
        }

        // Update the funding output info with transaction details
        transaction.FundingOutput.TransactionId = tx.GetHash().ToBytes();
        transaction.FundingOutput.Index = 0;

        _logger.LogInformation("Built funding transaction {TxId} with funding output at index 0", tx.GetHash());

        // Return as SignedTransaction (note: needs to be signed by the signer afterwards)
        return new SignedTransaction(tx.GetHash().ToBytes(), tx.ToBytes());
    }
}