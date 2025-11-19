using Microsoft.Extensions.DependencyInjection;

namespace NLightning.Infrastructure.Bitcoin;

using Builders;
using Builders.Interfaces;
using Crypto.Functions;
using Domain.Protocol.Interfaces;
using Infrastructure.Crypto.Interfaces;
using Protocol.Factories;
using Services;
using Wallet;
using Wallet.Interfaces;

/// <summary>
/// Extension methods for setting up Bitcoin infrastructure services in an IServiceCollection.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Bitcoin infrastructure services to the specified IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddBitcoinInfrastructure(this IServiceCollection services)
    {
        // Register Singletons
        services.AddSingleton<IBitcoinChainService, BitcoinChainService>();
        services.AddSingleton<IBlockchainMonitor, BlockchainMonitorService>();
        services.AddSingleton<ICommitmentKeyDerivationService, CommitmentKeyDerivationService>();
        services.AddSingleton<ICommitmentTransactionBuilder, CommitmentTransactionBuilder>();
        services.AddSingleton<IEcdh, Ecdh>();
        services.AddSingleton<IFundingOutputBuilder, FundingOutputBuilder>();
        services.AddSingleton<IFundingTransactionBuilder, FundingTransactionBuilder>();
        services.AddSingleton<IKeyDerivationService, KeyDerivationService>();
        services.AddSingleton<ITlvConverterFactory, TlvConverterFactory>();

        // Register Scoped Services
        services.AddScoped<IBitcoinWalletService, BitcoinWalletService>();

        return services;
    }
}