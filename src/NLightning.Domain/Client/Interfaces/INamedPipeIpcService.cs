namespace NLightning.Domain.Client.Interfaces;

/// <summary>
/// Interface for the named pipe ipc service
/// </summary>
public interface INamedPipeIpcService
{
    /// <summary>
    /// Starts the ipc server asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the ipc server asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task StopAsync();
}