using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace LoadBalancer.Core;

/// <summary>
/// Simple TCP proxy using raw sockets instead of Kestrel.
/// Provides full control over TCP behavior including proper half-close handling.
/// </summary>
public class SimpleTcpProxy : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly ILoadBalancer _loadBalancer;
    private readonly IHealthMonitor _healthMonitor;
    private readonly ILogger<SimpleTcpProxy>? _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly TimeSpan _connectTimeout;
    private Task? _acceptTask;

    public SimpleTcpProxy(
        string listenAddress,
        int listenPort,
        ILoadBalancer loadBalancer,
        IHealthMonitor healthMonitor,
        TimeSpan? connectTimeout = null,
        ILogger<SimpleTcpProxy>? logger = null)
    {
        _listener = new TcpListener(IPAddress.Parse(listenAddress), listenPort);
        _loadBalancer = loadBalancer ?? throw new ArgumentNullException(nameof(loadBalancer));
        _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(5);
        _logger = logger;
    }

    /// <summary>
    /// Starts the proxy and begins accepting connections.
    /// </summary>
    public Task StartAsync()
    {
        _listener.Start();
        _acceptTask = AcceptConnectionsAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the proxy and closes all connections.
    /// </summary>
    public async Task StopAsync()
    {
        await _cts.CancelAsync();
        _listener.Stop();

        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleConnectionAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error accepting connection");
                if (cancellationToken.IsCancellationRequested)
                    break;
            }
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            var clientSocket = client.Client;

            // Select a backend
            var backend = _loadBalancer.SelectBackend();
            if (backend == null)
            {
                _logger?.LogWarning("No healthy backends available");
                return;
            }

            _logger?.LogDebug("Routing connection to backend {Backend}", backend);

            Socket? backendSocket = null;
            try
            {
                // Connect to backend with timeout
                backendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                using var connectCts = new CancellationTokenSource(_connectTimeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    connectCts.Token, cancellationToken);

                await backendSocket.ConnectAsync(backend.Address, backend.Port, combinedCts.Token);

                _logger?.LogDebug("Connected to backend {Backend}", backend);

                // Forward traffic bidirectionally
                await SocketForwarder.ForwardAsync(clientSocket, backendSocket, cancellationToken);

                _healthMonitor.RecordSuccess(backend);
                _logger?.LogDebug("Connection to backend {Backend} completed successfully", backend);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected during shutdown
            }
            catch (SocketException ex)
            {
                _logger?.LogError(ex, "Socket error connecting to backend {Backend}", backend);
                _healthMonitor.RecordFailure(backend);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling connection to backend {Backend}", backend);
                _healthMonitor.RecordFailure(backend);
            }
            finally
            {
                backendSocket?.Dispose();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
    }
}
