namespace NLightning.Domain.Bitcoin.Wallet.Models;

using Channels.ValueObjects;
using Enums;
using Money;
using ValueObjects;

public sealed class UtxoModel
{
    public TxId TxId { get; }
    public uint Index { get; }
    public LightningMoney Amount { get; }
    public uint BlockHeight { get; }
    public uint AddressIndex { get; private set; }
    public bool IsAddressChange { get; private set; }
    public AddressType AddressType { get; private set; }
    public ChannelId? LockedToChannelId { get; set; }

    public WalletAddressModel? WalletAddress { get; private set; }

    public UtxoModel(TxId txId, uint index, LightningMoney amount, uint blockHeight, uint addressIndex,
                     bool isAddressChange, AddressType addressType)
    {
        TxId = txId;
        Index = index;
        Amount = amount;
        BlockHeight = blockHeight;
        AddressIndex = addressIndex;
        IsAddressChange = isAddressChange;
        AddressType = addressType;
    }

    public UtxoModel(TxId txId, uint index, LightningMoney amount, uint blockHeight, WalletAddressModel walletAddress)
    {
        TxId = txId;
        Index = index;
        Amount = amount;
        BlockHeight = blockHeight;
        SetWalletAddress(walletAddress);
    }

    public void SetWalletAddress(WalletAddressModel walletAddress)
    {
        WalletAddress = walletAddress;

        AddressIndex = walletAddress.Index;
        IsAddressChange = walletAddress.IsChange;
        AddressType = walletAddress.AddressType;
    }
}