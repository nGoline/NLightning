namespace NLightning.Domain.Bitcoin.Wallet.Models;

using Money;
using ValueObjects;

public sealed class UtxoModel
{
    public TxId TxId { get; }
    public uint Index { get; }
    public LightningMoney Amount { get; }
    public uint BlockHeight { get; }

    public UtxoModel(TxId txId, uint index, LightningMoney amount, uint blockHeight)
    {
        TxId = txId;
        Index = index;
        Amount = amount;
        BlockHeight = blockHeight;
    }
}