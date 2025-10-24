using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLightning.Domain.Exceptions;
using NLightning.Infrastructure.Node.ValueObjects;
using NLightning.Infrastructure.Protocol.Models;

namespace NLightning.Infrastructure.Transport.Services;

using Domain.Node.Options;
using Events;
using Interfaces;

public class TcpService : ITcpService
{
    private readonly ILogger<TcpService> _logger;
    private readonly NodeOptions _nodeOptions;
    private readonly List<TcpListener> _listeners = [];

    private CancellationTokenSource? _cts;
    private Task? _listeningTask;

    /// <inheritdoc />
    public event EventHandler<NewPeerConnectedEventArgs>? OnNewPeerConnected;

    public TcpService(ILogger<TcpService> logger, IOptions<NodeOptions> nodeOptions)
    {
        _logger = logger;
        _nodeOptions = nodeOptions.Value;
    }

    /// <inheritdoc />
    public Task StartListeningAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        foreach (var address in _nodeOptions.ListenAddresses)
        {
            var parts = address.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
            {
                _logger.LogWarning("Invalid listen address: {Address}", address);
                continue;
            }

            var ipAddress = IPAddress.Parse(parts[0]);
            var listener = new TcpListener(ipAddress, port);
            listener.Start();
            _listeners.Add(listener);

            _logger.LogInformation("Listening for connections on {Address}:{Port}", ipAddress, port);
        }

        _listeningTask = ListenForConnectionsAsync(_cts.Token);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopListeningAsync()
    {
        if (_cts is null)
            throw new InvalidOperationException("Service is not running");

        await _cts.CancelAsync();

        foreach (var listener in _listeners)
        {
            try
            {
                listener.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping listener");
            }
        }

        _listeners.Clear();

        if (_listeningTask is not null)
        {
            try
            {
                await _listeningTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
        }
    }

    /// <inheritdoc />
    /// <exception cref="ConnectionException">Thrown when the connection to the peer fails.</exception>
    public async Task<ConnectedPeer> ConnectToPeerAsync(PeerAddress peerAddress)
    {
        var tcpClient = new TcpClient();
        try
        {
            await tcpClient.ConnectAsync(peerAddress.Host, peerAddress.Port,
                                         new CancellationTokenSource(_nodeOptions.NetworkTimeout).Token);

            return new ConnectedPeer(peerAddress.PubKey, peerAddress.Host.ToString(), (uint)peerAddress.Port,
                                     tcpClient);
        }
        catch (OperationCanceledException)
        {
            throw new ConnectionException($"Timeout connecting to peer {peerAddress.Host}:{peerAddress.Port}");
        }
        catch (Exception e)
        {
            throw new ConnectionException($"Failed to connect to peer {peerAddress.Host}:{peerAddress.Port}", e);
        }
    }

    private async Task ListenForConnectionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var listener in _listeners)
                {
                    if (!listener.Pending())
                        continue;

                    var tcpClient = await listener.AcceptTcpClientAsync(cancellationToken);
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            _logger.LogInformation("New peer connection from {RemoteEndPoint}",
                                                   tcpClient.Client.RemoteEndPoint);

                            if (tcpClient.Client.RemoteEndPoint is not IPEndPoint ipEndPoint)
                            {
                                _logger.LogError("Failed to get remote endpoint for {RemoteEndPoint}",
                                                 tcpClient.Client.RemoteEndPoint);
                                return;
                            }

                            // Raise the event for a new peer connection
                            OnNewPeerConnected?.Invoke(
                                this,
                                new NewPeerConnectedEventArgs(ipEndPoint.Address.ToString(), (uint)ipEndPoint.Port,
                                                              tcpClient));
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Error accepting peer connection for {RemoteEndPoint}",
                                             tcpClient.Client.RemoteEndPoint);
                        }
                    }, cancellationToken);
                }

                await Task.Delay(100, cancellationToken); // Avoid busy-waiting
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stopping listener service");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unhandled exception in listener service");
        }
    }
}