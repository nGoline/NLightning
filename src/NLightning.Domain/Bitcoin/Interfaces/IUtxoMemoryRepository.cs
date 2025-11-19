using System.Diagnostics.CodeAnalysis;

namespace NLightning.Domain.Bitcoin.Interfaces;

using Channels.ValueObjects;
using Money;
using ValueObjects;
using Wallet.Models;

public interface IUtxoMemoryRepository
{
    void Add(UtxoModel utxoModel);
    void Spend(UtxoModel utxoModel);
    bool TryGetUtxo(TxId txId, uint index, [MaybeNullWhen(false)] out UtxoModel utxoModel);
    LightningMoney GetConfirmedBalance(uint currentBlockHeight);
    LightningMoney GetUnconfirmedBalance(uint currentBlockHeight);
    LightningMoney GetLockedBalance();
    void Load(List<UtxoModel> utxoSet);
    List<UtxoModel> LockUtxosToSpendOnChannel(LightningMoney requestFundingAmount, ChannelId channelId);
    List<UtxoModel> GetLockedUtxosForChannel(ChannelId channelId);
    List<UtxoModel> ReturnUtxosNotSpentOnChannel(ChannelId channelId);
    void ConfirmSpendOnChannel(ChannelId channelId);
}