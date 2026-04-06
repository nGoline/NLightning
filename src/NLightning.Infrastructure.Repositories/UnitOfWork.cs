using Microsoft.Extensions.Logging;

namespace NLightning.Infrastructure.Repositories;

using Database.Bitcoin;
using Database.Channel;
using Database.Node;
using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.ValueObjects;
using Domain.Bitcoin.Wallet.Models;
using Domain.Channels.Interfaces;
using Domain.Channels.Models;
using Domain.Crypto.Hashes;
using Domain.Node.Interfaces;
using Domain.Node.Models;
using Domain.Persistence.Interfaces;
using Domain.Serialization.Interfaces;
using Persistence.Contexts;

public class UnitOfWork : IUnitOfWork
{
    private readonly NLightningDbContext _context;
    private readonly ILogger<UnitOfWork> _logger;
    private readonly IMessageSerializer _messageSerializer;
    private readonly ISha256 _sha256;
    private readonly IUtxoMemoryRepository _utxoMemoryRepository;

    // Bitcoin repositories
    private BlockchainStateDbRepository? _blockchainStateDbRepository;
    private WatchedTransactionDbRepository? _watchedTransactionDbRepository;
    private WalletAddressesDbRepository? _walletAddressesDbRepository;
    private UtxoDbRepository? _utxoDbRepository;

    // Channel repositories
    private ChannelConfigDbRepository? _channelConfigDbRepository;
    private ChannelDbRepository? _channelDbRepository;
    private ChannelKeySetDbRepository? _channelKeySetDbRepository;
    private HtlcDbRepository? _htlcDbRepository;

    // Node repositories
    private PeerDbRepository? _peerDbRepository;

    public IBlockchainStateDbRepository BlockchainStateDbRepository =>
        _blockchainStateDbRepository ??= new BlockchainStateDbRepository(_context);

    public IWatchedTransactionDbRepository WatchedTransactionDbRepository =>
        _watchedTransactionDbRepository ??= new WatchedTransactionDbRepository(_context);

    public IWalletAddressesDbRepository WalletAddressesDbRepository =>
        _walletAddressesDbRepository ??= new WalletAddressesDbRepository(_context);

    public IUtxoDbRepository UtxoDbRepository => _utxoDbRepository ??= new UtxoDbRepository(_context);

    public IChannelConfigDbRepository ChannelConfigDbRepository =>
        _channelConfigDbRepository ??= new ChannelConfigDbRepository(_context);

    public IChannelDbRepository ChannelDbRepository =>
        _channelDbRepository ??= new ChannelDbRepository(_context, _messageSerializer, _sha256);

    public IChannelKeySetDbRepository ChannelKeySetDbRepository =>
        _channelKeySetDbRepository ??= new ChannelKeySetDbRepository(_context);

    public IHtlcDbRepository HtlcDbRepository =>
        _htlcDbRepository ??= new HtlcDbRepository(_context, _messageSerializer);

    public IPeerDbRepository PeerDbRepository =>
        _peerDbRepository ??= new PeerDbRepository(_context);

    public UnitOfWork(NLightningDbContext context, ILogger<UnitOfWork> logger, IMessageSerializer messageSerializer,
                      ISha256 sha256, IUtxoMemoryRepository utxoMemoryRepository)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
        _messageSerializer = messageSerializer;
        _sha256 = sha256;
        _utxoMemoryRepository = utxoMemoryRepository;
    }

    public async Task<ICollection<PeerModel>> GetPeersForStartupAsync()
    {
        var peers = await PeerDbRepository.GetAllAsync();
        var peerList = peers.ToList();
        foreach (var peer in peerList)
        {
            var channels = await ChannelDbRepository.GetByPeerIdAsync(peer.NodeId);
            var channelList = channels.ToList();
            if (channelList.Count > 0)
                peer.Channels = channelList as List<ChannelModel>;
        }

        return peerList;
    }

    public void AddUtxo(UtxoModel utxoModel)
    {
        try
        {
            _utxoMemoryRepository.Add(utxoModel);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to add Utxo to memory repository");
            throw;
        }

        try
        {
            UtxoDbRepository.Add(utxoModel);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to add Utxo to the database");

            // Rollback memory repository operation
            _utxoMemoryRepository.Spend(utxoModel);
        }
    }

    public void TrySpendUtxo(TxId transactionId, uint index)
    {
        // Check if utxo exists in memory
        if (!_utxoMemoryRepository.TryGetUtxo(transactionId, index, out var utxoModel))
            return;

        try
        {
            _utxoMemoryRepository.Spend(utxoModel);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to spend Utxo from memory repository");
            throw;
        }

        try
        {
            UtxoDbRepository.Spend(utxoModel);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to spend Utxo from the database");

            // Rollback memory repository operation
            _utxoMemoryRepository.Add(utxoModel);
        }
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }

    #region Dispose Pattern

    private bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
                _context.Dispose();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}