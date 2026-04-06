using Microsoft.EntityFrameworkCore;

namespace NLightning.Infrastructure.Persistence.Contexts;

using Entities.Bitcoin;
using Entities.Channel;
using Entities.Node;
using EntityConfiguration.Bitcoin;
using EntityConfiguration.Channel;
using EntityConfiguration.Node;
using Enums;
using Providers;

public class NLightningDbContext : DbContext
{
    private readonly DatabaseType _databaseType;

    public NLightningDbContext(DbContextOptions<NLightningDbContext> options, DatabaseTypeProvider databaseTypeProvider)
        : base(options)
    {
        _databaseType = databaseTypeProvider.DatabaseType;
    }

    // Bitcoin DbSets
    public DbSet<BlockchainStateEntity> BlockchainStates { get; set; }
    public DbSet<WatchedTransactionEntity> WatchedTransactions { get; set; }
    public DbSet<WalletAddressEntity> WalletAddresses { get; set; }
    public DbSet<UtxoEntity> Utxos { get; set; }

    // Channel DbSets
    public DbSet<ChannelEntity> Channels { get; set; }
    public DbSet<ChannelConfigEntity> ChannelConfigs { get; set; }
    public DbSet<ChannelKeySetEntity> ChannelKeySets { get; set; }
    public DbSet<HtlcEntity> Htlcs { get; set; }

    // Node DbSets
    public DbSet<PeerEntity> Peers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Bitcoin entities
        modelBuilder.ConfigureBlockchainStateEntity(_databaseType);
        modelBuilder.ConfigureWatchedTransactionEntity(_databaseType);
        modelBuilder.ConfigureWalletAddressEntity(_databaseType);
        modelBuilder.ConfigureUtxoEntity(_databaseType);

        // Channel entities
        modelBuilder.ConfigureChannelEntity(_databaseType);
        modelBuilder.ConfigureChannelConfigEntity(_databaseType);
        modelBuilder.ConfigureChannelKeySetEntity(_databaseType);
        modelBuilder.ConfigureHtlcEntity(_databaseType);

        // Node entities
        modelBuilder.ConfigurePeerEntity(_databaseType);
    }
}