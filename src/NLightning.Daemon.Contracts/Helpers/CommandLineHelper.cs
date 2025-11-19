namespace NLightning.Daemon.Contracts.Helpers;

/// <summary>
/// Helper class for displaying command line usage information
/// </summary>
public static class CommandLineHelper
{
    public const string DashH = "-h";
    public const string DashDashHelp = "--help";
    public const string DashN = "-n";
    public const string DashDashNetwork = "--network";
    public const string DashDashNetworkEquals = "--network=";
    public const string DashC = "-c";
    public const string DashDashCookie = "--cookie";
    public const string DashDashCookieEquals = "--cookie=";

    /// <summary>
    /// Parse command line arguments to check for help request
    /// </summary>
    public static bool IsHelpRequested(string[] args)
    {
        return args.Any(arg =>
                            arg.Equals(DashDashHelp, StringComparison.OrdinalIgnoreCase)
                         || arg.Equals(DashH, StringComparison.OrdinalIgnoreCase));
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

    public static string GetCookiePath(string[] args)
    {
        string? network = null;
        string? cookiePath = null;

        // Check command line args
        for (var i = 0; i < args.Length; i++)
        {
            // Check for network
            if (args[i].StartsWith(DashN) || args[i].StartsWith(DashDashNetwork, StringComparison.OrdinalIgnoreCase))
            {
                if ((args[i].Equals(DashDashNetwork, StringComparison.OrdinalIgnoreCase) || args[i].Equals(DashN))
                 && i + 1 < args.Length)
                {
                    network = args[i + 1];
                }
                else if (args[i].StartsWith(DashDashNetworkEquals, StringComparison.OrdinalIgnoreCase))
                {
                    network = args[i][DashDashNetworkEquals.Length..];
                }

                if (network is not null)
                    break;
            }
            else if (args[i].StartsWith(DashC) || // Check for cookie
                     args[i].StartsWith(DashDashCookie, StringComparison.OrdinalIgnoreCase))
            {
                if ((args[i].Equals(DashDashCookie, StringComparison.OrdinalIgnoreCase) || args[i].Equals(DashC))
                 && i + 1 < args.Length)
                {
                    cookiePath = args[i];
                }
                else if (args[i].StartsWith(DashDashCookieEquals, StringComparison.OrdinalIgnoreCase))
                {
                    cookiePath = args[i][DashDashCookieEquals.Length..];
                }

                if (cookiePath is not null)
                    break;
            }
        }

        // Check the environment if no args provided
        if (cookiePath is null && network is null)
        {
            var envNetwork = Environment.GetEnvironmentVariable("NLTG_NETWORK");
            if (!string.IsNullOrEmpty(envNetwork))
            {
                network = envNetwork;
            }
            else
            {
                var envCookie = Environment.GetEnvironmentVariable("NLTG_COOKIE");
                if (!string.IsNullOrEmpty(envCookie))
                {
                    cookiePath = envCookie;
                }
            }
        }

        // Go with default paths if no environments provided
        if (cookiePath is not null)
            return ExtractDirectoryFromCookiePath(cookiePath);

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        cookiePath = Path.Combine(homeDir, ".nltg", network ?? "mainnet");
        return Directory.Exists(cookiePath) ? cookiePath : throw new InvalidOperationException("Cookie not found");
    }

    private static string ExtractDirectoryFromCookiePath(string cookiePath)
    {
        cookiePath = Path.GetFullPath(cookiePath);
        if (cookiePath.EndsWith(".cookie", StringComparison.OrdinalIgnoreCase))
        {
            cookiePath = Path.GetDirectoryName(cookiePath) ??
                         throw new InvalidOperationException("Cookie not found");
        }
        else if (cookiePath.EndsWith(Path.DirectorySeparatorChar))
            cookiePath = cookiePath[..^1];

        return Directory.Exists(cookiePath) ? cookiePath : throw new InvalidOperationException("Cookie not found");
    }

    private static bool IsOption(string arg) => arg.StartsWith("-n")
                                             || arg.StartsWith("--network")
                                             || arg.StartsWith("-c")
                                             || arg.StartsWith("--cookie");
}