using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LoadBalancer.Core.IntegrationTests;

/// <summary>
/// Test backend server that sends a multi-part response with delays.
/// Used to test half-close scenarios where client finishes sending before backend finishes responding.
/// </summary>
public class SlowResponseBackendServer : IAsyncDisposable
{
    private readonly int _port;
    private readonly string _name;
    private readonly TimeSpan _delayBetweenParts;
    private readonly int _responseParts;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private Task? _serverTask;

    public int Port => _port;
    public string Name => _name;

    public SlowResponseBackendServer(string name, int port, TimeSpan delayBetweenParts, int responseParts)
    {
        _name = name;
        _port = port;
        _delayBetweenParts = delayBetweenParts;
        _responseParts = responseParts;
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    public Task StartAsync()
    {
        _listener.Start();
        _serverTask = RunServerAsync(_cts.Token);
        return Task.CompletedTask;
    }

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
                if (cancellationToken.IsCancellationRequested)
                    break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            {
                var stream = client.GetStream();
                var buffer = new byte[4096];

                // Read request (we don't care about content, just need to receive something)
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                    return;

                // Send multi-part response with delays
                for (int i = 1; i <= _responseParts; i++)
                {
                    var part = $"[{_name}] Part {i} of {_responseParts}\n";
                    var partBytes = Encoding.UTF8.GetBytes(part);
                    await stream.WriteAsync(partBytes, cancellationToken);
                    await stream.FlushAsync(cancellationToken);

                    if (i < _responseParts)
                    {
                        await Task.Delay(_delayBetweenParts, cancellationToken);
                    }
                }

                // Send completion marker
                var complete = $"[{_name}] COMPLETE\n";
                var completeBytes = Encoding.UTF8.GetBytes(complete);
                await stream.WriteAsync(completeBytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
        }
        catch (Exception)
        {
            // Ignore errors during shutdown or client disconnect
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
    }
}
