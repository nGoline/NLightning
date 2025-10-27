using System.Collections.Concurrent;

namespace NLightning.Infrastructure.Repositories.Memory;

using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.ValueObjects;
using Domain.Bitcoin.Wallet.Models;
using Domain.Money;

public class UtxoMemoryRepository : IUtxoMemoryRepository
{
    private readonly ConcurrentDictionary<(TxId, uint), UtxoModel> _utxoSet = [];

    public void Add(UtxoModel utxoModel)
    {
        if (!_utxoSet.TryAdd((utxoModel.TxId, utxoModel.Index), utxoModel))
            throw new InvalidOperationException("Cannot add Utxo");
    }

    public void Spend(UtxoModel utxoModel)
    {
        if (!_utxoSet.TryRemove((utxoModel.TxId, utxoModel.Index), out _))
            throw new InvalidOperationException("Cannot remove Utxo");
    }

    public LightningMoney GetConfirmedBalance(uint currentBlockHeight)
    {
        return LightningMoney.Satoshis(_utxoSet.Values
                                               .Where(x => x.BlockHeight + 3 <= currentBlockHeight)
                                               .Sum(x => x.Amount.Satoshi));
    }

    public LightningMoney GetUnconfirmedBalance(uint currentBlockHeight)
    {
        return LightningMoney.Satoshis(_utxoSet.Values
                                               .Where(x => x.BlockHeight + 3 > currentBlockHeight)
                                               .Sum(x => x.Amount.Satoshi));
    }

    public void Load(List<UtxoModel> utxoSet)
    {
        foreach (var utxoModel in utxoSet)
            _utxoSet.TryAdd((utxoModel.TxId, utxoModel.Index), utxoModel);
    }
}