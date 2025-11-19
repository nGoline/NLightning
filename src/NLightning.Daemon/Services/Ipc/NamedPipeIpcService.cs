using System.IO.Pipes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NLightning.Daemon.Services.Ipc;

using Contracts.Utilities;
using Daemon.Ipc.Interfaces;
using Domain.Client.Constants;
using Domain.Client.Interfaces;
using Domain.Node.Options;
using Factories;
using Transport.Ipc;

/// <summary>
/// Hosted service that listens to on a named pipe and processes IPC requests using injected components.
/// </summary>
internal sealed class NamedPipeIpcService : INamedPipeIpcService
{
    private readonly ILogger<NamedPipeIpcService> _logger;
    private readonly IIpcAuthenticator _authenticator;
    private readonly IIpcFraming _framing;
    private readonly IIpcRequestRouter _router;
    private readonly string _pipeName;
    private readonly string _cookiePath;

    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public NamedPipeIpcService(IIpcAuthenticator authenticator, string configPath, IIpcFraming framing,
                               ILogger<NamedPipeIpcService> logger, IOptions<NodeOptions> _,
                               IIpcRequestRouter router)
    {
        _logger = logger;
        _authenticator = authenticator;
        _framing = framing;
        _router = router;

        _pipeName = NodeUtils.GetNamedPipeFilePath(configPath);
        _cookiePath = NodeUtils.GetCookieFilePath(configPath);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        EnsureCookieExists();

        _listenerTask = ListenToIpcClientAsync(cancellationToken);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is null)
            throw new InvalidOperationException("Service is not running");

        await _cts.CancelAsync();

        if (_listenerTask is not null)
        {
            try
            {
                await _listenerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
        }
    }

    private async Task ListenToIpcClientAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 10,
                                                           PipeTransmissionMode.Byte,
                                                           PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(cancellationToken);

                    _ = Task.Run(() => HandleClientAsync(server, cancellationToken), cancellationToken);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "IPC server accept loop error");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("IPC server loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in IPC server loop");
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream stream, CancellationToken ct)
    {
        try
        {
            var request = await _framing.ReadAsync(stream, ct);

            if (!await _authenticator.ValidateAsync(request.AuthToken, ct))
            {
                var err = IpcErrorFactory.CreateErrorEnvelope(request, ErrorCodes.AuthenticationFailure,
                                                              "Authentication failed.");
                await _framing.WriteAsync(stream, err, ct);
                return;
            }

            var response = await _router.RouteAsync(request, ct);
            await _framing.WriteAsync(stream, response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IPC client handling failed");
            try
            {
                // Try to write a generic error if we still can read an envelope
                var env = new IpcEnvelope { Version = 1, CorrelationId = Guid.NewGuid(), Kind = IpcEnvelopeKind.Error };
                var err = IpcErrorFactory.CreateErrorEnvelope(env, ErrorCodes.ServerError, ex.Message);
                await _framing.WriteAsync(stream, err, ct);
            }
            catch
            {
                // ignore
            }
        }
        finally
        {
            try { await stream.DisposeAsync(); }
            catch
            {
                /* ignore */
            }
        }
    }

    private void EnsureCookieExists()
    {
        try
        {
            var dir = Path.GetDirectoryName(_cookiePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(_cookiePath))
                return;

            var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            File.WriteAllText(_cookiePath, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure IPC cookie exists at {Path}", _cookiePath);
            throw;
        }
    }
}