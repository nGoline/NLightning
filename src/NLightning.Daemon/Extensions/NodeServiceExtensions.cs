using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NLightning.Daemon.Extensions;

using Application;
using Contracts.Utilities;
using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.Transactions.Factories;
using Domain.Bitcoin.Transactions.Interfaces;
using Domain.Channels.Factories;
using Domain.Channels.Interfaces;
using Domain.Crypto.Hashes;
using Domain.Node.Options;
using Domain.Protocol.Interfaces;
using Domain.Protocol.ValueObjects;
using Handlers;
using Infrastructure;
using Infrastructure.Bitcoin;
using Infrastructure.Bitcoin.Builders;
using Infrastructure.Bitcoin.Managers;
using Infrastructure.Bitcoin.Options;
using Infrastructure.Bitcoin.Services;
using Infrastructure.Bitcoin.Signers;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Serialization;
using Interfaces;
using Services;
using Services.Ipc;

public static class NodeServiceExtensions
{
    /// <summary>
    /// Registers all NLTG application services for dependency injection
    /// </summary>
    public static IHostBuilder ConfigureNltgServices(this IHostBuilder hostBuilder, SecureKeyManager secureKeyManager)
    {
        return hostBuilder.ConfigureServices((hostContext, services) =>
        {
            // Get configuration
            var configuration = hostContext.Configuration;

            // Register configuration as a service
            services.AddSingleton(configuration);

            // Register the main daemon service
            services.AddHostedService<NltgDaemonService>();

            // Register IPC server
            services.AddSingleton<IIpcFraming, LengthPrefixedIpcFraming>();
            services.AddSingleton<IIpcRequestRouter, IpcRequestRouter>();
            services.AddSingleton<INodeInfoQueryService, NodeInfoQueryService>();
            services.AddSingleton<IIpcCommandHandler, NodeInfoIpcHandler>();
            services.AddSingleton<IIpcCommandHandler, ConnectIpcHandler>();
            services.AddSingleton<IIpcAuthenticator>(sp =>
            {
                var nodeOptions = sp.GetRequiredService<IOptions<NodeOptions>>().Value;
                var cookiePath = NodeUtils.GetCookieFilePath(nodeOptions.BitcoinNetwork);
                var logger = sp.GetRequiredService<ILogger<CookieFileAuthenticator>>();
                return new CookieFileAuthenticator(cookiePath, logger);
            });
            services.AddHostedService<NamedPipeIpcHostedService>();

            // Add HttpClient for FeeService with configuration
            services.AddHttpClient<IFeeService, FeeService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            // Singleton services (one instance throughout the application)
            services.AddSingleton<ISecureKeyManager>(secureKeyManager);
            services.AddSingleton<IChannelFactory>(sp =>
            {
                var feeService = sp.GetRequiredService<IFeeService>();
                var lightningSigner = sp.GetRequiredService<ILightningSigner>();
                var nodeOptions = sp.GetRequiredService<IOptions<NodeOptions>>().Value;
                var sha256 = sp.GetRequiredService<ISha256>();
                return new ChannelFactory(feeService, lightningSigner, nodeOptions, sha256);
            });
            services.AddSingleton<ICommitmentTransactionModelFactory, CommitmentTransactionModelFactory>();

            // Add the Signer
            services.AddSingleton<ILightningSigner>(serviceProvider =>
            {
                var fundingOutputBuilder = serviceProvider.GetRequiredService<IFundingOutputBuilder>();
                var keyDerivationService = serviceProvider.GetRequiredService<IKeyDerivationService>();
                var logger = serviceProvider.GetRequiredService<ILogger<LocalLightningSigner>>();
                var nodeOptions = serviceProvider.GetRequiredService<IOptions<NodeOptions>>().Value;

                // Create the signer with the correct network
                return new LocalLightningSigner(fundingOutputBuilder, keyDerivationService, logger, nodeOptions,
                                                secureKeyManager);
            });

            // Add the Application services
            services.AddApplicationServices();

            // Add the Infrastructure services
            services.AddBitcoinInfrastructure();
            services.AddInfrastructureServices();
            services.AddPersistenceInfrastructureServices(configuration);
            services.AddRepositoriesInfrastructureServices();
            services.AddSerializationInfrastructureServices();

            // Scoped services (one instance per scope)

            // Transient services (new instance each time)

            // Register options with values from configuration
            services.AddOptions<BitcoinOptions>().BindConfiguration("Bitcoin").ValidateOnStart();
            services.AddOptions<FeeEstimationOptions>().BindConfiguration("FeeEstimation").ValidateOnStart();
            services.AddOptions<NodeOptions>()
                    .BindConfiguration("Node")
                    .PostConfigure(options =>
                     {
                         var configuredAddresses = configuration.GetSection("Node:ListenAddresses").Get<string[]?>();
                         if (configuredAddresses is { Length: > 0 })
                         {
                             options.ListenAddresses = configuredAddresses.ToList();
                         }

                         var networkString = configuration.GetValue<string>("Node:Network");
                         if (!string.IsNullOrWhiteSpace(networkString))
                         {
                             options.BitcoinNetwork = new BitcoinNetwork(networkString);
                         }

                         options.Features.ChainHashes = [options.BitcoinNetwork.ChainHash];
                     })
                    .ValidateOnStart();
        });
    }
}