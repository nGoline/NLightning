namespace NLightning.Domain.Bitcoin.Events;

using Money;
using ValueObjects;

public class WalletMovementEventArgs : EventArgs
{
    public string WalletAddress { get; }
    public LightningMoney Amount { get; }
    public TxId TxId { get; }
    public uint BlockHeight { get; }

    public WalletMovementEventArgs(string walletAddress, LightningMoney amount, TxId txId, uint blockHeight)
    {
        WalletAddress = walletAddress;
        Amount = amount;
        TxId = txId;
        BlockHeight = blockHeight;
    }
}