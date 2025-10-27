using Microsoft.EntityFrameworkCore;

namespace NLightning.Infrastructure.Repositories.Database.Bitcoin;

using Domain.Bitcoin.Interfaces;
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

    public async Task<IEnumerable<UtxoModel>> GetAllAsync()
    {
        var utxoSet = await Get(asNoTracking: true).ToListAsync();

        return utxoSet.Select(MapEntityToModel);
    }

    private UtxoEntity MapDomainToEntity(UtxoModel model)
    {
        return new UtxoEntity
        {
            TransactionId = model.TxId,
            Index = model.Index,
            AmountSats = model.Amount.Satoshi,
            BlockHeight = model.BlockHeight
        };
    }

    private UtxoModel MapEntityToModel(UtxoEntity entity)
    {
        return new UtxoModel(entity.TransactionId, entity.Index, LightningMoney.Satoshis(entity.AmountSats),
                             entity.BlockHeight);
    }
}