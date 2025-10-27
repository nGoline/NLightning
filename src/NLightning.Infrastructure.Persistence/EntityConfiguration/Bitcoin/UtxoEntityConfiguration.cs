using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NLightning.Infrastructure.Persistence.EntityConfiguration.Bitcoin;

using Domain.Crypto.Constants;
using Entities.Bitcoin;
using Enums;
using ValueConverters;

public static class UtxoEntityConfiguration
{
    public static void ConfigureUtxoEntity(this ModelBuilder modelBuilder, DatabaseType databaseType)
    {
        modelBuilder.Entity<UtxoEntity>(entity =>
        {
            // Set Primary Key
            entity.HasKey(e => new { e.TransactionId, e.Index });

            // Set Required props
            entity.Property(e => e.AmountSats)
                  .IsRequired();
            entity.Property(e => e.BlockHeight)
                  .IsRequired();

            // Set converters
            entity.Property(x => x.TransactionId)
                  .HasConversion<TxIdConverter>();

            if (databaseType == DatabaseType.MicrosoftSql)
                OptimizeConfigurationForSqlServer(entity);
        });
    }

    private static void OptimizeConfigurationForSqlServer(EntityTypeBuilder<UtxoEntity> entity)
    {
        entity.Property(e => e.TransactionId).HasColumnType($"varbinary({CryptoConstants.Sha256HashLen})");
    }
}