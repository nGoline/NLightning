namespace NLightning.Infrastructure.Persistence.Entities.Bitcoin;

using Domain.Bitcoin.ValueObjects;

public class UtxoEntity
{
    public required TxId TransactionId { get; set; }
    public uint Index { get; set; }
    public long AmountSats { get; set; }
    public uint BlockHeight { get; set; }

    // Default constructor for EF Core
    internal UtxoEntity() { }
}