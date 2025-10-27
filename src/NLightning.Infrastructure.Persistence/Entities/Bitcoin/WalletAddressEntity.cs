namespace NLightning.Infrastructure.Persistence.Entities.Bitcoin;

using Domain.Bitcoin.Enums;

public class WalletAddressEntity
{
    public uint Index { get; set; }
    public bool IsChange { get; set; }
    public required AddressType AddressType { get; set; }
    public required string Address { get; set; }
    public uint UtxoQty { get; set; }

    // Default constructor for EF Core
    internal WalletAddressEntity() { }
}