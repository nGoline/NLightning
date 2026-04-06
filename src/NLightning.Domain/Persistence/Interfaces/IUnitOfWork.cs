namespace NLightning.Domain.Persistence.Interfaces;

using Bitcoin.Interfaces;
using Bitcoin.ValueObjects;
using Bitcoin.Wallet.Models;
using Channels.Interfaces;
using Node.Interfaces;
using Node.Models;

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
    void TrySpendUtxo(TxId transactionId, uint index);

    void SaveChanges();
    Task SaveChangesAsync();
}