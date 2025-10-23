namespace NLightning.Daemon.Contracts.Helpers;

/// <summary>
/// Helper class for displaying command line usage information
/// </summary>
public static class CommandLineHelper
{
    /// <summary>
    /// Parse command line arguments to check for help request
    /// </summary>
    public static bool IsHelpRequested(string[] args)
    {
        return args.Any(arg =>
                            arg.Equals("--help", StringComparison.OrdinalIgnoreCase)
                         || arg.Equals("-h", StringComparison.OrdinalIgnoreCase));
    }

    public static string? GetCommand(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (IsOption(args[i]))
            {
                i++;
                continue;
            }

            return args[i].ToLowerInvariant();
        }

        return null;
    }

    public static string[] GetCommandArguments(string command, string[] args)
    {
        var cmdArgs = new List<string>();
        var cmdFound = false;

        for (var i = 0; i < args.Length; i++)
        {
            if (!cmdFound)
            {
                if (args[i].Equals(command, StringComparison.OrdinalIgnoreCase))
                    cmdFound = true;

                continue;
            }

            cmdArgs.Add(args[i]);
        }

        return cmdArgs.ToArray();
    }

    public static string GetNetwork(string[] args)
    {
        var network = "mainnet"; // Default

        // Check command line args
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--network", StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals("-n", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    network = args[i + 1];
                    break;
                }
            }

            if (!args[i].StartsWith("--network=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            network = args[i]["--network=".Length..];
            break;
        }

        // Check environment variable if not found in args
        var envNetwork = Environment.GetEnvironmentVariable("NLTG_NETWORK");
        if (!string.IsNullOrEmpty(envNetwork))
        {
            network = envNetwork;
        }

        return network;
    }

    private static bool IsOption(string arg) => arg.StartsWith("-n")
                                             || arg.StartsWith("--network")
                                             || arg.StartsWith("-c")
                                             || arg.StartsWith("--cookie");
}