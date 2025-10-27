using Microsoft.EntityFrameworkCore;

namespace NLightning.Infrastructure.Persistence.EntityConfiguration.Bitcoin;

using Entities.Bitcoin;
using Enums;

public static class WalletAddressEntityConfiguration
{
    public static void ConfigureWalletAddressEntity(this ModelBuilder modelBuilder, DatabaseType databaseType)
    {
        modelBuilder.Entity<WalletAddressEntity>(entity =>
        {
            // Set Primary Key
            entity.HasKey(e => new { e.Index, e.IsChange, e.AddressType });

            // Set Required props
            entity.Property(e => e.Address)
                  .IsRequired();
            entity.Property(e => e.UtxoQty)
                  .IsRequired()
                  .HasDefaultValue(0);
        });
    }
}