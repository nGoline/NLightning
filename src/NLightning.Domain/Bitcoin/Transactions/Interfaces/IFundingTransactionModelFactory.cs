namespace NLightning.Domain.Bitcoin.Transactions.Interfaces;

using Channels.Models;
using Models;
using Wallet.Models;

public interface IFundingTransactionModelFactory
{
    FundingTransactionModel Create(ChannelModel channel, List<UtxoModel> utxos, WalletAddressModel? changeAddress);
}