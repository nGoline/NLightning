using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NLightning.Daemon.Services;

using Contracts.Control;
using Domain.Node.Options;
using Domain.Persistence.Interfaces;
using Interfaces;

public sealed class NodeInfoQueryService : INodeInfoQueryService
{
    private readonly IServiceProvider _services;
    private readonly NodeOptions _nodeOptions;

    public NodeInfoQueryService(IServiceProvider services, IOptions<NodeOptions> nodeOptions)
    {
        _services = services;
        _nodeOptions = nodeOptions.Value;
    }

    public async Task<NodeInfoResponse> QueryAsync(CancellationToken ct)
    {
        // resolve per-call scope to access repositories
        using var scope = _services.CreateScope();
        var uow = scope.ServiceProvider.GetService<IUnitOfWork>();

        var bestHashHex = string.Empty;
        long bestHeight = 0;
        DateTimeOffset? bestTime = null;

        if (uow is not null)
        {
            try
            {
                var state = await uow.BlockchainStateDbRepository.GetStateAsync();
                if (state is not null)
                {
                    bestHeight = state.LastProcessedHeight;
                    bestHashHex = state.LastProcessedBlockHash.ToString();
                    bestTime = state.LastProcessedAt;
                }
            }
            catch
            {
                // ignore, return defaults
            }
        }

        return new NodeInfoResponse
        {
            Network = _nodeOptions.BitcoinNetwork,
            BestBlockHash = bestHashHex,
            BestBlockHeight = bestHeight,
            BestBlockTime = bestTime,
            Implementation = "NLightning",
            Version = typeof(NodeInfoQueryService).Assembly.GetName().Version?.ToString()
        };
    }
}