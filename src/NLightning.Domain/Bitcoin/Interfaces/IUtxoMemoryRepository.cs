using NLightning.Domain.Money;

namespace NLightning.Domain.Bitcoin.Interfaces;

using Wallet.Models;

public interface IUtxoMemoryRepository
{
    void Add(UtxoModel utxoModel);
    void Spend(UtxoModel utxoModel);
    LightningMoney GetConfirmedBalance(uint currentBlockHeight);
    LightningMoney GetUnconfirmedBalance(uint currentBlockHeight);
    void Load(List<UtxoModel> utxoSet);
}