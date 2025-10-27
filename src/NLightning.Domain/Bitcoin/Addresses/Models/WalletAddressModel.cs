namespace NLightning.Domain.Bitcoin.Addresses.Models;

using Enums;

public sealed class WalletAddressModel
{
    public AddressType AddressType { get; }
    public uint Index { get; }
    public bool IsChange { get; }
    public string Address { get; }
    public uint UtxoQty { get; private set; }

    public WalletAddressModel(AddressType addressType, uint index, bool isChange, string address, uint utxoQty = 0)
    {
        AddressType = addressType;
        Index = index;
        IsChange = isChange;
        Address = address;
        UtxoQty = utxoQty;
    }

    public void IncrementUtxoQty()
    {
        UtxoQty++;
    }

    public override string ToString()
    {
        return Address;
    }
}