using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LoadBalancer.Core.IntegrationTests;

/// <summary>
/// Simple TCP echo server for testing the load balancer.
/// Echoes back messages with a server identifier.
/// </summary>
public class TestBackendServer : IAsyncDisposable
{
    private readonly int _port;
    private readonly string _name;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private Task? _serverTask;
    private int _connectionCount;

    public int Port => _port;
    public string Name => _name;
    public int ConnectionCount => _connectionCount;

    public TestBackendServer(string name, int port)
    {
        _name = name;
        _port = port;
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    /// <summary>
    /// Starts the test backend server.
    /// </summary>
    public Task StartAsync()
    {
        _listener.Start();
        _serverTask = RunServerAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the test backend server.
    /// </summary>
    public async Task StopAsync()
    {
        await _cts.CancelAsync();
        _listener.Stop();

        if (_serverTask != null)
        {
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Ignore errors during shutdown
                if (cancellationToken.IsCancellationRequested)
                    break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _connectionCount);

        try
        {
            using (client)
            {
                var stream = client.GetStream();
                var buffer = new byte[4096];

                while (!cancellationToken.IsCancellationRequested)
                {
                    var bytesRead = await stream.ReadAsync(buffer, cancellationToken);

                    if (bytesRead == 0)
                        break; // Client disconnected

                    // Echo back with server identifier
                    var received = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var response = $"[{_name}] {received}";
                    var responseBytes = Encoding.UTF8.GetBytes(response);

                    await stream.WriteAsync(responseBytes, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }
            }
        }
        catch (Exception)
        {
            // Ignore errors during shutdown or client disconnect
        }
        finally
        {
            Interlocked.Decrement(ref _connectionCount);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
    }
}
