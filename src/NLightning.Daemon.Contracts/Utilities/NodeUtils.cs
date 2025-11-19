namespace NLightning.Daemon.Contracts.Utilities;

using Constants;

public static class NodeUtils
{
    /// <summary>
    /// Gets the path for the Named-Pipe file
    /// </summary>
    public static string GetNamedPipeFilePath(string cookiePath)
    {
        return Path.Combine(cookiePath, NodeConstants.NamedPipeFile);
    }

    /// <summary>
    /// Gets the path for the Named-Pipe file
    /// </summary>
    public static string GetCookieFilePath(string cookiePath)
    {
        return Path.Combine(cookiePath, NodeConstants.CookieFile);
    }
}