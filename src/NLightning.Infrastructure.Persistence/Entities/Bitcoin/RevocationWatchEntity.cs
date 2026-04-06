namespace NLightning.Infrastructure.Persistence.Entities.Bitcoin;

using Domain.Bitcoin.ValueObjects;
using Domain.Channels.ValueObjects;

public class RevocationWatchEntity
{
    public required ChannelId ChannelId { get; set; }
    public required ulong CommitmentNumber { get; set; }
    public required TxId CommitmentTransactionId { get; set; }
    public required byte[] PenaltyTransactionBytes { get; set; }
    public uint? TriggeredAtHeight { get; set; }
    public uint? IncludedInHeight { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? TriggeredAt { get; set; }
    public DateTime? IncludedAt { get; set; }

    // Default constructor for EF Core
    internal RevocationWatchEntity() { }
}