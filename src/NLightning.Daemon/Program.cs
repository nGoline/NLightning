using MessagePack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using NLightning.Daemon.Contracts.Helpers;
using NLightning.Daemon.Contracts.Utilities;
using NLightning.Daemon.Extensions;
using NLightning.Daemon.Utilities;
using NLightning.Domain.Node.Options;
using NLightning.Domain.Protocol.ValueObjects;
using NLightning.Infrastructure.Bitcoin.Managers;
using NLightning.Infrastructure.Bitcoin.Options;
using NLightning.Infrastructure.Bitcoin.Wallet;
using NLightning.Transport.Ipc.MessagePack;
using Serilog;

try
{
    // Bootstrap logger for startup messages
    Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateBootstrapLogger();

    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    {
        var exception = (Exception)e.ExceptionObject;
        Log.Logger.Error("An unhandled exception occurred: {exception}", exception);
    };

    // Get network for the PID file path
    var network = CommandLineHelper.GetNetwork(args);
    var pidFilePath = DaemonUtils.GetPidFilePath(network);

    // Check for the stop command
    if (DaemonUtils.IsStopRequested(args))
    {
        var stopped = DaemonUtils.StopDaemon(pidFilePath, Log.Logger);
        return stopped ? 0 : 1;
    }

    // Check for status command
    if (DaemonUtils.IsStatusRequested(args))
    {
        ReportDaemonStatus(pidFilePath);
        return 0;
    }

    // Check if help is requested
    if (CommandLineHelper.IsHelpRequested(args))
    {
        DaemonUtils.ShowUsage();
        return 0;
    }

    // Read the configuration file to check for daemon setting
    var initialConfig = NodeConfigurationExtensions.ReadInitialConfiguration(args);

    string? password = null;

    // Try to get password from args or prompt
    if (args.Contains("--password"))
    {
        var idx = Array.IndexOf(args, "--password");
        if (idx >= 0 && idx + 1 < args.Length)
            password = args[idx + 1];
    }

    if (string.IsNullOrWhiteSpace(password))
    {
        password = ConsoleUtils.ReadPassword("Enter password for key encryption: ");
    }

    if (string.IsNullOrWhiteSpace(password))
    {
        Log.Error("Password cannot be empty.");
        return 1;
    }

    SecureKeyManager keyManager;
    var keyFilePath = SecureKeyManager.GetKeyFilePath(network);
    if (!File.Exists(keyFilePath))
    {
        // Get current Block Height for key birth
        try
        {
            // Create logger for the wallet service using Serilog
            var loggerFactory = LoggerFactory.Create(b => b.AddSerilog(Log.Logger, dispose: false));
            var walletLogger = loggerFactory.CreateLogger<BitcoinChainService>();

            // Bind options from initialConfig
            var bitcoinOptions = initialConfig.GetSection("Bitcoin").Get<BitcoinOptions>()
                              ?? throw new InvalidOperationException(
                                     "Bitcoin configuration section is missing or invalid.");
            var nodeOptions = initialConfig.GetSection("Node").Get<NodeOptions>()
                           ?? throw new InvalidOperationException("Node configuration section is missing or invalid.");

            // Instantiate the service
            var bitcoinWalletService = new BitcoinChainService(
                Options.Create(bitcoinOptions),
                walletLogger,
                Options.Create(nodeOptions)
            );

            var heightOfBirth = await bitcoinWalletService.GetCurrentBlockHeightAsync();

            // Creates new key
            var key = new Key();
            keyManager = new SecureKeyManager(key.ToBytes(), new BitcoinNetwork(network), keyFilePath, heightOfBirth);
            keyManager.SaveToFile(password);
            Console.WriteLine($"New key created and saved to {keyFilePath}");
        }
        catch (Exception e)
        {
            Log.Logger.Error(e, "An error occurred while creating new key.");
            return 1;
        }
    }
    else
    {
        // Load the existing key
        keyManager = SecureKeyManager.FromFilePath(keyFilePath, new BitcoinNetwork(network), password);
        Console.WriteLine($"Loaded key from {keyFilePath}");
    }

    // Start as a daemon if requested
    if (DaemonUtils.StartDaemonIfRequested(args, initialConfig, pidFilePath, Log.Logger))
    {
        // The parent process exits immediately after starting the daemon
        return 0;
    }

    // Register the default formatter for MessagePackSerializer
    MessagePackSerializer.DefaultOptions = NLightningMessagePackOptions.Options;

    Log.Information("Starting NLTG...");

    // Create and run host
    var host = Host.CreateDefaultBuilder(args)
                   .ConfigureNltg(initialConfig)
                   .ConfigureNltgServices(keyManager)
                   .Build();

    // Run migrations if configured
    await host.MigrateDatabaseIfConfiguredAsync();

    // Run the host
    await host.RunAsync();

    return 0;
}
catch (Exception e)
{
    Log.Fatal(e, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

static void ReportDaemonStatus(string pidFilePath)
{
    try
    {
        if (!File.Exists(pidFilePath))
        {
            Console.WriteLine("NLTG daemon is not running");
            return;
        }

        var pidText = File.ReadAllText(pidFilePath).Trim();
        if (!int.TryParse(pidText, out var pid))
        {
            Console.WriteLine("Invalid PID in file, daemon may not be running");
            return;
        }

        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);
            var runTime = DateTime.Now - process.StartTime;

            Console.WriteLine("NLTG daemon is running");
            Console.WriteLine($"PID: {pid}");
            Console.WriteLine($"Started: {process.StartTime}");
            Console.WriteLine($"Uptime: {runTime.Days}d {runTime.Hours}h {runTime.Minutes}m");
            Console.WriteLine($"Memory: {process.WorkingSet64 / (1024 * 1024)} MB");
        }
        catch (ArgumentException)
        {
            Console.WriteLine("NLTG daemon is not running (stale PID file)");
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"Error checking daemon status: {e.Message}");
    }
}