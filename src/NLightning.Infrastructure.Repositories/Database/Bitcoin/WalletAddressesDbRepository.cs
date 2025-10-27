using Microsoft.EntityFrameworkCore;
using NLightning.Domain.Bitcoin.Wallet.Models;

namespace NLightning.Infrastructure.Repositories.Database.Bitcoin;

using Domain.Bitcoin.Enums;
using Domain.Bitcoin.Interfaces;
using Persistence.Contexts;
using Persistence.Entities.Bitcoin;

public class WalletAddressesDbRepository(NLightningDbContext context)
    : BaseDbRepository<WalletAddressEntity>(context), IWalletAddressesDbRepository
{
    public async Task<WalletAddressModel?> GetUnusedAddressAsync(AddressType type, bool isChange)
    {
        var walletAddressEntity = await DbSet
                                       .AsNoTracking()
                                       .Where(x => x.AddressType.Equals(type)
                                                && x.IsChange.Equals(isChange)
                                                && x.UtxoQty.Equals(0))
                                       .OrderBy(x => x.UtxoQty)
                                       .FirstOrDefaultAsync();

        return walletAddressEntity is null ? null : MapEntityToModel(walletAddressEntity);
    }

    public async Task<uint> GetLastUsedAddressIndex(AddressType addressType, bool isChange)
    {
        var walletAddressEntity = await DbSet
                                       .AsNoTracking()
                                       .Where(x => x.AddressType.Equals(addressType)
                                                && x.IsChange.Equals(isChange))
                                       .OrderByDescending(x => x.Index)
                                       .FirstOrDefaultAsync();

        return walletAddressEntity?.Index ?? 0;
    }

    public void AddRange(List<WalletAddressModel> addresses)
    {
        var walletAddressEntities = addresses.Select(MapDomainToEntity);
        DbSet.AddRange(walletAddressEntities);
    }

    public void UpdateAsync(WalletAddressModel address)
    {
        var walletAddressEntity = MapDomainToEntity(address);
        Update(walletAddressEntity);
    }

    public IEnumerable<WalletAddressModel> GetAllAddresses()
    {
        return DbSet.AsNoTracking().AsEnumerable().Select(MapEntityToModel);
    }

    private static WalletAddressEntity MapDomainToEntity(WalletAddressModel model)
    {
        return new WalletAddressEntity
        {
            Index = model.Index,
            IsChange = model.IsChange,
            AddressType = model.AddressType,
            Address = model.Address,
            UtxoQty = model.UtxoQty
        };
    }

    private static WalletAddressModel MapEntityToModel(WalletAddressEntity entity)
    {
        return new WalletAddressModel(entity.AddressType, entity.Index, entity.IsChange, entity.Address,
                                      entity.UtxoQty);
    }
}