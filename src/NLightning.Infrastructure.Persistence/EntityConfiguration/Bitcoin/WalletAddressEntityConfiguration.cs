using Microsoft.EntityFrameworkCore;

namespace NLightning.Infrastructure.Persistence.EntityConfiguration.Bitcoin;

using Entities.Bitcoin;
using Enums;

public static class WalletAddressEntityConfiguration
{
    public static void ConfigureWalletAddressEntity(this ModelBuilder modelBuilder, DatabaseType _)
    {
        modelBuilder.Entity<WalletAddressEntity>(entity =>
        {
            // Set Primary Key
            entity.HasKey(e => new { e.Index, e.IsChange, e.AddressType });

            // Set Required props
            entity.Property(e => e.Address)
                  .IsRequired();

            // Set relations
            entity.HasMany(x => x.Utxos)
                  .WithOne(x => x.WalletAddress)
                  .HasForeignKey(x => new { x.AddressIndex, x.IsAddressChange, x.AddressType })
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}