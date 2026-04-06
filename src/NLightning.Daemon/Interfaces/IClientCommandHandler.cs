namespace NLightning.Daemon.Interfaces;

using NLightning.Domain.Client.Enums;

public interface IClientCommandHandler<TRequest, TResponse>
{
    /// <summary>
    /// Gets the client command associated with the handler.
    /// </summary>
    /// <remarks>
    /// This property returns a value from the <c>ClientCommand</c> enumeration,
    /// representing the specific command handled by the implementing class.
    /// </remarks>
    ClientCommand Command { get; }

    /// <summary>
    /// Handles the execution of a client command asynchronously.
    /// </summary>
    /// <param name="request">The request object containing the necessary data to handle the command.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the response of the command execution.</returns>
    Task<TResponse> HandleAsync(TRequest request, CancellationToken ct);
}