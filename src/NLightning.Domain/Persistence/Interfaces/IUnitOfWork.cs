using NLightning.Domain.Bitcoin.Wallet.Models;
using NLightning.Domain.Node.Models;

namespace NLightning.Domain.Persistence.Interfaces;

using Bitcoin.Interfaces;
using Channels.Interfaces;
using Node.Interfaces;

public interface IUnitOfWork : IDisposable
{
    // Bitcoin repositories
    IBlockchainStateDbRepository BlockchainStateDbRepository { get; }
    IWatchedTransactionDbRepository WatchedTransactionDbRepository { get; }
    IWalletAddressesDbRepository WalletAddressesDbRepository { get; }
    IUtxoDbRepository UtxoDbRepository { get; }

    // Chanel repositories
    IChannelConfigDbRepository ChannelConfigDbRepository { get; }
    IChannelDbRepository ChannelDbRepository { get; }
    IChannelKeySetDbRepository ChannelKeySetDbRepository { get; }
    IHtlcDbRepository HtlcDbRepository { get; }

    // Node repositories
    IPeerDbRepository PeerDbRepository { get; }

    Task<ICollection<PeerModel>> GetPeersForStartupAsync();
    void AddUtxo(UtxoModel utxoModel);

    void SaveChanges();
    Task SaveChangesAsync();
}