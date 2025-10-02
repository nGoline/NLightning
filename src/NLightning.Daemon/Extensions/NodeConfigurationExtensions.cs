using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace NLightning.Daemon.Extensions;

using Helpers;

public static class NodeConfigurationExtensions
{
    /// <summary>
    /// Configures the host builder with NLTG configuration and Serilog
    /// </summary>
    public static IHostBuilder ConfigureNltg(this IHostBuilder hostBuilder, IConfiguration configuration)
    {
        // Configure the host builder
        return hostBuilder
              .ConfigureAppConfiguration(builder =>
               {
                   builder.AddConfiguration(configuration);
               })
              .UseSerilog((_, _, loggerConfig) =>
               {
                   // Read from the current configuration
                   loggerConfig
                      .ReadFrom.Configuration(configuration)
                      .Enrich.With<ClassNameEnricher>();
               });
    }

    /// <summary>
    /// Configures the host builder with NLTG configuration and Serilog
    /// </summary>
    public static IHostBuilder ConfigureNltg(this IHostBuilder hostBuilder, string[] args)
    {
        var config = ReadInitialConfiguration(args);

        // Configure the host builder
        return hostBuilder
              .ConfigureAppConfiguration(builder =>
               {
                   builder.AddConfiguration(config);
               })
              .UseSerilog((_, _, loggerConfig) =>
               {
                   // Read from the current configuration
                   loggerConfig
                      .ReadFrom.Configuration(config)
                      .Enrich.With<ClassNameEnricher>();
               });
    }

    public static IConfiguration ReadInitialConfiguration(string[] args)
    {
        // Get network from the command line or environment variable first
        var initialConfig = new ConfigurationBuilder()
                           .AddCommandLine(args)
                           .AddEnvironmentVariables("NLTG_")
                           .Build();
        var network = initialConfig["network"] ?? initialConfig["n"] ?? "mainnet";

        // Check for a custom config path first
        var configPath = initialConfig["config"] ?? initialConfig["c"];
        var usingCustomConfig = !string.IsNullOrEmpty(configPath);

        if (usingCustomConfig)
        {
            configPath = Path.GetFullPath(configPath!);
            if (!File.Exists(configPath))
            {
                Log.Warning("Custom configuration file not found at {ConfigPath}", configPath);
                usingCustomConfig = false;
            }
        }

        // If no custom path, use default ~/.nltg/{network}/appsettings.json
        if (!usingCustomConfig)
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configDir = Path.Combine(homeDir, ".nltg", network);
            configPath = Path.Combine(configDir, "appsettings.json");

            // Ensure directory exists
            Directory.CreateDirectory(configDir);

            // Create default config if none exists
            if (!File.Exists(configPath))
            {
                File.WriteAllText(configPath, CreateDefaultConfigJson());
            }
        }

        // Log startup info using bootstrap logger
        Log.Information("Starting NLTG with configuration from {ConfigPath} (Network: {Network})", configPath, network);

        // Build configuration with proper precedence
        var config = new ConfigurationBuilder();
        config.Sources.Clear();

        return config
              .AddJsonFile(configPath!, optional: false, reloadOnChange: false)
              .AddEnvironmentVariables("NLTG_")
              .AddCommandLine(args)
              .Build();
    }

    /// <summary>
    /// Creates default configuration JSON
    /// </summary>
    private static string CreateDefaultConfigJson()
    {
        return """
               {
                 "Serilog": {
                   "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
                   "MinimumLevel": {
                     "Default": "Error",
                     "Override": {
                       "NLightning": "Information",
                       "System": "Warning"
                     }
                   },
                   "WriteTo": [
                     {
                       "Name": "Console",
                       "Args": {
                         "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{ClassName}] {Message:lj}{NewLine}{Exception}"
                       }
                     },
                     {
                       "Name": "File",
                       "Args": {
                         "path": "logs/log-.txt",
                         "rollingInterval": "Month",
                         "retainedFileCountLimit": 12,
                         "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{ClassName}] {Message:lj}{NewLine}{Exception}"
                       }
                     }
                   ],
                   "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId", "ClassName" ]
                 },
                 "Node": {
                   "Network": "regtest",
                   "Daemon": false,
                   "DnsSeedServers": [
                     "nlseed.nlightn.ing",
                     "nodes.lightning.directory",
                     "lseed.bitcoinstats.com"
                   ],
                   "ListenAddresses": [
                     "0.0.0.0:9735"
                   ],
                   "Features": {
                     "StaticRemoteKey": "Compulsory"
                   }
                 },
                 "FeeEstimation": {
                   "Url": "https://mempool.space/api/v1/fees/recommended",
                   "Method": "GET",
                   "ContentType": "application/json",
                   "PreferredFeeRate": "fastestFee",
                   "CacheExpiration": "5m",
                   "RateMultiplier": 1000,
                   "CacheFile": "fee_estimation_cache.bin"
                 },
                 "Database": {
                   "Provider": "Sqlite",
                   "ConnectionString": "Data Source=nltg.db;Cache=Shared",
                   "RunMigrations": false
                 },
                 "Bitcoin": {
                   "RpcEndpoint": "http://localhost:8332",
                   "RpcUser": "bitcoinrpc",
                   "RpcPassword": "your_rpc_password",
                   "ZmqHost": "bitcoinzmq",
                   "ZmqBlockPort": 8334,
                   "ZmqTxPort": 8335
                 }
               }
               """;
    }
}