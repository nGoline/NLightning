using Microsoft.EntityFrameworkCore;

namespace NLightning.Infrastructure.Repositories.Database.Node;

using Domain.Crypto.ValueObjects;
using Domain.Node.Interfaces;
using Domain.Node.Models;
using Persistence.Contexts;
using Persistence.Entities.Node;

public class PeerDbRepository : BaseDbRepository<PeerEntity>, IPeerDbRepository
{
    public PeerDbRepository(NLightningDbContext context) : base(context)
    {
    }

    public async Task AddOrUpdateAsync(PeerModel peerModel)
    {
        var peerEntity = MapDomainToEntity(peerModel);

        var existingEntity = await GetByIdAsync(peerEntity.NodeId);
        if (existingEntity is null)
        {
            Insert(peerEntity);
        }
        else
        {
            Update(peerEntity);
        }
    }

    public void Update(PeerModel peerModel)
    {
        var peerEntity = MapDomainToEntity(peerModel);
        base.Update(peerEntity);
    }

    public async Task<IEnumerable<PeerModel>> GetAllAsync()
    {
        var peerEntities = await Get().ToListAsync();

        return peerEntities.Select(MapEntityToDomain);
    }

    public async Task<PeerModel?> GetByNodeIdAsync(CompactPubKey nodeId)
    {
        var peerEntity = await GetByIdAsync(nodeId);
        if (peerEntity == null)
            return null;

        return MapEntityToDomain(peerEntity);
    }

    public async Task UpdatePeerLastSeenAsync(CompactPubKey peerCompactPubKey)
    {
        var existingPeer = await GetByIdAsync(peerCompactPubKey);
        if (existingPeer is not null)
        {
            existingPeer.LastSeenAt = DateTime.UtcNow;
            Update(existingPeer);
        }
    }

    private static PeerEntity MapDomainToEntity(PeerModel peerModel)
    {
        return new PeerEntity
        {
            NodeId = peerModel.NodeId,
            Host = peerModel.Host,
            Port = peerModel.Port,
            Type = peerModel.Type,
            LastSeenAt = peerModel.LastSeenAt
        };
    }

    private static PeerModel MapEntityToDomain(PeerEntity peerEntity)
    {
        return new PeerModel(peerEntity.NodeId, peerEntity.Host, peerEntity.Port, peerEntity.Type)
        {
            LastSeenAt = peerEntity.LastSeenAt
        };
    }
}