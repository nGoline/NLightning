using System.Buffers;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace NLightning.Infrastructure.Transport.Services;

using Crypto.Interfaces;
using Domain.Crypto.ValueObjects;
using Domain.Exceptions;
using Domain.Protocol.Interfaces;
using Domain.Serialization.Interfaces;
using Domain.Transport;
using Exceptions;
using Interfaces;
using Protocol.Constants;

internal sealed class TransportService : ITransportService
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger _logger;
    private readonly IMessageSerializer _messageSerializer;
    private readonly TimeSpan _networkTimeout;
    private readonly SemaphoreSlim _networkWriteSemaphore = new(1, 1);
    private readonly TcpClient _tcpClient;
    private readonly TaskCompletionSource<bool> _tcs = new();

    private IHandshakeService? _handshakeService;
    private ITransport? _transport;
    private bool _disposed;

    // event that will be called when a message is received
    public event EventHandler<MemoryStream>? MessageReceived;
    public event EventHandler<Exception>? ExceptionRaised;

    public bool IsInitiator { get; }
    public bool IsConnected => _tcpClient.Connected;
    public CompactPubKey? RemoteStaticPublicKey { get; private set; }

    public TransportService(IEcdh ecdh, ILogger logger, IMessageSerializer messageSerializer, TimeSpan networkTimeout,
                            bool isInitiator, ReadOnlySpan<byte> s, ReadOnlySpan<byte> rs, TcpClient tcpClient)
        : this(logger, messageSerializer, networkTimeout, new HandshakeService(isInitiator, s, rs, null, ecdh),
               tcpClient)
    {
        _messageSerializer = messageSerializer;
        _networkTimeout = networkTimeout;
    }

    internal TransportService(ILogger logger, IMessageSerializer messageSerializer, TimeSpan networkTimeout,
                              IHandshakeService handshakeService, TcpClient tcpClient)
    {
        _handshakeService = handshakeService;
        _logger = logger;
        _messageSerializer = messageSerializer;
        _networkTimeout = networkTimeout;
        _tcpClient = tcpClient;

        IsInitiator = handshakeService.IsInitiator;
    }

    public async Task InitializeAsync()
    {
        if (_handshakeService == null)
            throw new NullReferenceException(nameof(_handshakeService));

        if (!_tcpClient.Connected)
            throw new InvalidOperationException("TcpClient is not connected");

        var writeBuffer = ArrayPool<byte>.Shared.Rent(66);
        var readBuffer = ArrayPool<byte>.Shared.Rent(66);
        var stream = _tcpClient.GetStream();

        CancellationTokenSource networkTimeoutCancellationTokenSource = new();

        var host = _tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";

        if (_handshakeService.IsInitiator)
        {
            try
            {
                _logger.LogTrace("We're initiator, writing Act One for {host}", host);

                // Write Act One
                var len = _handshakeService.PerformStep(ProtocolConstants.EmptyMessage, writeBuffer.AsSpan()[..50],
                                                        out _);
                await stream.WriteAsync(writeBuffer.AsMemory()[..len], networkTimeoutCancellationTokenSource.Token);
                await stream.FlushAsync(networkTimeoutCancellationTokenSource.Token);

                // Read exactly 50 bytes
                _logger.LogTrace("Reading Act Two from {host}", host);
                networkTimeoutCancellationTokenSource = new CancellationTokenSource(_networkTimeout);
                await stream.ReadExactlyAsync(readBuffer.AsMemory()[..50], networkTimeoutCancellationTokenSource.Token);
                networkTimeoutCancellationTokenSource.Dispose();

                // Read Act Two and Write Act Three
                _logger.LogTrace("Writing Act Three for {host}", host);
                len = _handshakeService.PerformStep(readBuffer.AsSpan()[..50], writeBuffer.AsSpan()[..66],
                                                    out _transport);
                await stream.WriteAsync(writeBuffer.AsMemory()[..len], CancellationToken.None);
                await stream.FlushAsync(CancellationToken.None);
            }
            catch (Exception e)
            {
                throw new ConnectionTimeoutException($"Timeout while reading Handshake's Act 2 from host {host}", e);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(writeBuffer);
                ArrayPool<byte>.Shared.Return(readBuffer);
            }
        }
        else
        {
            var act = 1;
            _logger.LogTrace("We're responder, waiting for Act One from {host}", host);

            try
            {
                // Read exactly 50 bytes
                networkTimeoutCancellationTokenSource = new CancellationTokenSource(_networkTimeout);
                if (!IsSocketConnected())
                    throw new ConnectionException($"TcpClient disconnected before reading Act One from host {host}");

                await stream.ReadExactlyAsync(readBuffer.AsMemory()[..50], networkTimeoutCancellationTokenSource.Token);

                // Read Act One and Write Act Two
                _logger.LogTrace("Writing Act Two for {host}", host);
                var len = _handshakeService.PerformStep(readBuffer.AsSpan()[..50], writeBuffer.AsSpan()[..50], out _);

                if (!IsSocketConnected())
                    throw new ConnectionException($"TcpClient disconnected before writing Act Two to host {host}");

                await stream.WriteAsync(writeBuffer.AsMemory()[..len], CancellationToken.None);
                await stream.FlushAsync(CancellationToken.None);

                // Read exactly 66 bytes
                _logger.LogTrace("Reading Act Three from {host}", host);
                act = 3;
                networkTimeoutCancellationTokenSource = new CancellationTokenSource(_networkTimeout);
                if (!IsSocketConnected())
                    throw new ConnectionException($"TcpClient disconnected before reading Act Three from host {host}");
                await stream.ReadExactlyAsync(readBuffer.AsMemory()[..66], networkTimeoutCancellationTokenSource.Token);
                networkTimeoutCancellationTokenSource.Dispose();

                // Read Act Three
                _ = _handshakeService.PerformStep(readBuffer.AsSpan()[..66], writeBuffer.AsSpan()[..50],
                                                  out _transport);
            }
            catch (TaskCanceledException tce)
            {
                _tcs.SetResult(true);
                throw new ConnectionTimeoutException(
                    $"Timeout while reading Handshake's Act {act} from host {host}", tce);
            }
            catch (Exception e)
            {
                _tcs.SetResult(true);
                throw new ConnectionException($"Host {host} closed the connection", e);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(writeBuffer);
                ArrayPool<byte>.Shared.Return(readBuffer);
            }
        }

        // Handshake completed
        if (_transport is null)
            throw new InvalidOperationException($"Handshake not completed for host {host}");

        RemoteStaticPublicKey = _handshakeService.RemoteStaticPublicKey
                             ?? throw new InvalidOperationException($"RemoteStaticPublicKey is null for host {host}");

        // Listen to messages and raise event
        _logger.LogTrace("Handshake completed with host {host}, listening to messages from peer {peer}", host,
                         RemoteStaticPublicKey);
        _ = Task.Run(ReadResponseAsync, _cts.Token).ContinueWith(task =>
        {
            if (task.Exception?.InnerExceptions.Count > 0)
                ExceptionRaised?.Invoke(this, task.Exception.InnerExceptions[0]);
        }, _cts.Token);

        // Dispose of the handshake service
        _handshakeService.Dispose();
        _handshakeService = null;
    }

    public async Task WriteMessageAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        if (_tcpClient is null)
            throw new ConnectionException("TcpClient was null while trying to write a message",
                                          new NullReferenceException(nameof(_tcpClient)));

        if (!IsSocketConnected())
            throw new ConnectionException("TcpClient was not connected while trying to write a message");

        if (_transport is null)
            throw new ConnectionException("Handshake not completed while trying to write a message");

        // Serialize the message
        using var messageStream = new MemoryStream();
        await _messageSerializer.SerializeAsync(message, messageStream);

        // Encrypt the message
        var buffer = ArrayPool<byte>.Shared.Rent(ProtocolConstants.MaxMessageLength);
        var size = _transport.WriteMessage(messageStream.ToArray(),
                                           buffer.AsSpan()[..ProtocolConstants.MaxMessageLength]);

        // Write the message to stream
        await _networkWriteSemaphore.WaitAsync(cancellationToken);
        try
        {
            var stream = _tcpClient.GetStream();
            await stream.WriteAsync(buffer.AsMemory()[..size], cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        catch (Exception e)
        {
            throw new ConnectionException("Error writing message", e);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            _networkWriteSemaphore.Release();
        }
    }

    private bool IsSocketConnected()
    {
        try
        {
            if (_tcpClient.Client.Connected)
            {
                // This is how you can determine if a socket is still connected.
                return _tcpClient.Client.Connected &&
                       (!_tcpClient.Client.Poll(1, SelectMode.SelectRead) || _tcpClient.Client.Available != 0);
            }

            return false;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private async Task ReadResponseAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(ProtocolConstants.MaxMessageLength);
            var memoryBuffer = buffer.AsMemory();

            try
            {
                if (_transport == null)
                    throw new InvalidOperationException("Handshake not completed while trying to read a message");

                if (_tcpClient is null || !IsSocketConnected())
                    throw new InvalidOperationException("TcpClient is not connected while trying to read a message");

                // Read response
                var stream = _tcpClient.GetStream();
                var lenRead = await stream.ReadAsync(memoryBuffer[..ProtocolConstants.MessageHeaderSize], _cts.Token);
                if (_cts.IsCancellationRequested)
                    break;

                if (lenRead != ProtocolConstants.MessageHeaderSize)
                {
                    if (!IsSocketConnected() || lenRead == 0)
                        throw new ConnectionException(
                            "TcpClient is not connected while trying to read a message header");

                    throw new ConnectionException("Peer sent wrong length");
                }

                var messageLen =
                    _transport.ReadMessageLength(memoryBuffer[..ProtocolConstants.MessageHeaderSize].Span);
                if (_cts.IsCancellationRequested)
                    break;

                if (messageLen > ProtocolConstants.MaxMessageLength)
                    throw new ConnectionException("Peer sent message too long");

                if (!IsSocketConnected())
                    throw new ConnectionException("TcpClient is not connected while trying to read a message body");

                lenRead = await stream.ReadAsync(memoryBuffer[..messageLen], _cts.Token);
                if (_cts.IsCancellationRequested)
                    break;

                if (lenRead != messageLen)
                    throw new ConnectionException("Peer sent wrong body length");

                messageLen = _transport.ReadMessagePayload(memoryBuffer[..lenRead].Span, buffer);

                // Raise event
                var messageStream = new MemoryStream(buffer[..messageLen]);
                MessageReceived?.Invoke(this, messageStream);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (ConnectionException)
            {
                throw;
            }
            catch (Exception e)
            {
                if (!_cts.IsCancellationRequested)
                {
                    if (_tcpClient is null || !_tcpClient.Connected)
                        throw new ConnectionException("Peer closed the connection");

                    throw new ConnectionException("Error reading response", e);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, true);
            }
        }

        _tcs.TrySetResult(true);
    }

    #region Dispose Pattern

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Cancel and wait for an elegant shutdown
            _cts.Cancel();
            _tcs.Task.Wait(TimeSpan.FromSeconds(5));

            _handshakeService?.Dispose();
            _transport?.Dispose();
            _tcpClient.Dispose();
        }

        _disposed = true;
    }

    ~TransportService()
    {
        Dispose(false);
    }

    #endregion
}