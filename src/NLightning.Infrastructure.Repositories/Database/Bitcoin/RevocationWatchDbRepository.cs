namespace NLightning.Infrastructure.Repositories.Database.Bitcoin;

using Domain.Bitcoin.Interfaces;
using Persistence.Contexts;
using Persistence.Entities.Bitcoin;

public class RevocationWatchDbRepository(NLightningDbContext context)
    : BaseDbRepository<RevocationWatchEntity>(context), IRevocationWatchDbRepository
{
}