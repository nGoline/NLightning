namespace NLightning.Daemon.Interfaces;

public interface IIpcAuthenticator
{
    Task<bool> ValidateAsync(string? token, CancellationToken ct = default);
}