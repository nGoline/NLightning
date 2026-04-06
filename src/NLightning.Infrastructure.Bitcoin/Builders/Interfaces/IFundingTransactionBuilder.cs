namespace NLightning.Infrastructure.Bitcoin.Builders.Interfaces;

using Domain.Bitcoin.Transactions.Models;
using Domain.Bitcoin.ValueObjects;

public interface IFundingTransactionBuilder
{
    /// <summary>
    /// Builds a funding transaction from UTXOs
    /// </summary>
    /// <param name="transaction">The funding transaction model</param>
    /// <returns>A signed transaction with the funding output</returns>
    SignedTransaction Build(FundingTransactionModel transaction);
}