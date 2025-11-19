namespace NLightning.Infrastructure.Persistence.Entities.Bitcoin;

using Domain.Bitcoin.Enums;
using Domain.Bitcoin.ValueObjects;
using Domain.Channels.ValueObjects;

public class UtxoEntity
{
    public required TxId TransactionId { get; set; }
    public uint Index { get; set; }
    public long AmountSats { get; set; }
    public uint BlockHeight { get; set; }
    public uint AddressIndex { get; set; }
    public bool IsAddressChange { get; set; }
    public AddressType AddressType { get; set; }
    public ChannelId? LockedToChannelId { get; set; }
    public TxId? UsedInTransactionId { get; set; }

    public virtual WalletAddressEntity? WalletAddress { get; set; }

    // Default constructor for EF Core
    internal UtxoEntity() { }
}