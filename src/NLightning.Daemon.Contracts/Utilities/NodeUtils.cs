namespace NLightning.Daemon.Contracts.Utilities;

using Constants;

public static class NodeUtils
{
    /// <summary>
    /// Gets the path for the Named-Pipe file
    /// </summary>
    public static string GetNamedPipeFilePath(string network)
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var networkDir = Path.Combine(homeDir, NodeConstants.DaemonFolder, network);
        Directory.CreateDirectory(networkDir); // Ensure directory exists
        return Path.Combine(networkDir, NodeConstants.NamedPipeFile);
    }

    /// <summary>
    /// Gets the path for the Named-Pipe file
    /// </summary>
    public static string GetCookieFilePath(string network)
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var networkDir = Path.Combine(homeDir, NodeConstants.DaemonFolder, network);
        Directory.CreateDirectory(networkDir); // Ensure directory exists
        return Path.Combine(networkDir, NodeConstants.CookieFile);
    }
}