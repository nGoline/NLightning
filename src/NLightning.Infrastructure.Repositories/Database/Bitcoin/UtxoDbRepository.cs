using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace NLightning.Infrastructure.Repositories.Database.Bitcoin;

using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.ValueObjects;
using Domain.Bitcoin.Wallet.Models;
using Domain.Money;
using Persistence.Contexts;
using Persistence.Entities.Bitcoin;

public class UtxoDbRepository(NLightningDbContext context)
    : BaseDbRepository<UtxoEntity>(context), IUtxoDbRepository
{
    public void Add(UtxoModel utxoModel)
    {
        var utxoEntity = MapDomainToEntity(utxoModel);
        Insert(utxoEntity);
    }

    public void Spend(UtxoModel utxoModel)
    {
        var utxoEntity = MapDomainToEntity(utxoModel);
        Delete(utxoEntity);
    }

    public void Update(UtxoModel utxoModel)
    {
        var utxoEntity = MapDomainToEntity(utxoModel);
        Update(utxoEntity);
    }

    public async Task<IEnumerable<UtxoModel>> GetUnspentAsync(bool includeWalletAddress = false)
    {
        var query = Get(asNoTracking: true).AsQueryable();
        if (includeWalletAddress)
            query.Include(x => x.WalletAddress);

        var utxoSet = await query.ToListAsync();

        return utxoSet.Select(MapEntityToModel);
    }

    public async Task<UtxoModel?> GetByIdAsync(TxId txId, uint index, bool includeWalletAddress = false)
    {
        Expression<Func<UtxoEntity, object>>? include = includeWalletAddress
                                                            ? entity => entity.WalletAddress!
                                                            : null;
        var utxoEntity = await GetByIdAsync(new { txId, index }, true, include);
        return utxoEntity is null
                   ? null
                   : MapEntityToModel(utxoEntity);
    }

    private UtxoEntity MapDomainToEntity(UtxoModel model)
    {
        return new UtxoEntity
        {
            TransactionId = model.TxId,
            Index = model.Index,
            AmountSats = model.Amount.Satoshi,
            BlockHeight = model.BlockHeight,
            AddressIndex = model.AddressIndex,
            IsAddressChange = model.IsAddressChange,
            AddressType = model.AddressType
        };
    }

    private UtxoModel MapEntityToModel(UtxoEntity entity)
    {
        var utxoModel = new UtxoModel(entity.TransactionId, entity.Index, LightningMoney.Satoshis(entity.AmountSats),
                                      entity.BlockHeight, entity.AddressIndex, entity.IsAddressChange,
                                      entity.AddressType);

        if (entity.WalletAddress is not null)
        {
            var walletAddressModel = WalletAddressesDbRepository.MapEntityToModel(entity.WalletAddress);
            utxoModel.SetWalletAddress(walletAddressModel);
        }

        return utxoModel;
    }
}