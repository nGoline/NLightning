namespace NLightning.Domain.Bitcoin.Wallet.Models;

using Enums;

public sealed class WalletAddressModel
{
    public AddressType AddressType { get; }
    public uint Index { get; }
    public bool IsChange { get; }
    public string Address { get; }

    public WalletAddressModel(AddressType addressType, uint index, bool isChange, string address)
    {
        AddressType = addressType;
        Index = index;
        IsChange = isChange;
        Address = address;
    }

    public override string ToString()
    {
        return Address;
    }
}