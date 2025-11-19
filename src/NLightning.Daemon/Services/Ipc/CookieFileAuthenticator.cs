using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Services.Ipc;

using Daemon.Ipc.Interfaces;

/// <summary>
/// Cookie-file-based authenticator (Bitcoin Core style). Uses constant-time comparison.
/// </summary>
public sealed class CookieFileAuthenticator : IIpcAuthenticator
{
    private readonly string _cookieFilePath;
    private readonly ILogger<CookieFileAuthenticator> _logger;

    public CookieFileAuthenticator(string cookieFilePath, ILogger<CookieFileAuthenticator> logger)
    {
        _cookieFilePath = cookieFilePath;
        _logger = logger;
    }

    public async Task<bool> ValidateAsync(string? token, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (!File.Exists(_cookieFilePath)) return false;
            var expected = (await File.ReadAllTextAsync(_cookieFilePath, ct)).Trim();
            return FixedTimeEquals(expected, token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auth validation failed");
            return false;
        }
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}