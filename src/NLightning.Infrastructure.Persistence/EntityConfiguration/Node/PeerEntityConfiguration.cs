using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NLightning.Infrastructure.Persistence.EntityConfiguration.Node;

using Domain.Crypto.Constants;
using Entities.Node;
using Enums;
using ValueConverters;

public static class PeerEntityConfiguration
{
    public static void ConfigurePeerEntity(this ModelBuilder modelBuilder, DatabaseType databaseType)
    {
        modelBuilder.Entity<PeerEntity>(entity =>
        {
            // Set PrimaryKey
            entity.HasKey(e => e.NodeId);

            // Set required props
            entity.Property(e => e.Host).IsRequired();
            entity.Property(e => e.Port).IsRequired();
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.LastSeenAt).IsRequired();

            // Required byte[] properties
            entity.Property(e => e.NodeId)
                  .HasConversion<CompactPubKeyConverter>()
                  .IsRequired();

            if (databaseType == DatabaseType.MicrosoftSql)
                OptimizeConfigurationForSqlServer(entity);
        });
    }

    private static void OptimizeConfigurationForSqlServer(EntityTypeBuilder<PeerEntity> entity)
    {
        entity.Property(e => e.NodeId).HasColumnType($"varbinary({CryptoConstants.CompactPubkeyLen})");
    }
}