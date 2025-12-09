using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using LoadBalancer.Core;

namespace LoadBalancer.Core.IntegrationTests;

/// <summary>
/// Integration tests for SimpleTcpProxy with multiple backends.
/// Tests round-robin distribution, failover, and concurrent connections.
/// </summary>
[Trait("Category", "Integration")]
public class LoadBalancerIntegrationTests : IAsyncLifetime
{
    private const int ProxyPort = 18300;
    private const int Backend1Port = 19301;
    private const int Backend2Port = 19302;

    private TestBackendServer? _backend1;
    private TestBackendServer? _backend2;
    private SimpleTcpProxy? _proxy;
    private IBackendPool? _backendPool;
    private PassiveHealthMonitor? _healthMonitor;

    public async Task InitializeAsync()
    {
        // Start test backend servers
        _backend1 = new TestBackendServer("Backend-1", Backend1Port);
        _backend2 = new TestBackendServer("Backend-2", Backend2Port);

        await _backend1.StartAsync();
        await _backend2.StartAsync();
        await Task.Delay(100);

        // Create backend pool and services
        var options = new LoadBalancerOptions
        {
            Backends = new List<Backend>
            {
                new() { Name = "Backend-1", Address = "127.0.0.1", Port = Backend1Port },
                new() { Name = "Backend-2", Address = "127.0.0.1", Port = Backend2Port }
            },
            Connection = new ConnectionOptions { ConnectTimeoutMs = 1000 }
        };
        var optionsMonitor = new TestOptionsMonitor<LoadBalancerOptions>(options);

        _backendPool = new BackendPool(optionsMonitor, NullLogger<BackendPool>.Instance);
        _healthMonitor = new PassiveHealthMonitor(Options.Create(options), NullLogger<PassiveHealthMonitor>.Instance);
        var loadBalancer = new RoundRobinBalancer(_backendPool);

        // Start SimpleTcpProxy
        _proxy = new SimpleTcpProxy(
            "127.0.0.1",
            ProxyPort,
            loadBalancer,
            _healthMonitor,
            connectTimeout: TimeSpan.FromSeconds(1));

        await _proxy.StartAsync();
        await Task.Delay(100);
    }

    public async Task DisposeAsync()
    {
        if (_proxy != null)
            await _proxy.DisposeAsync();

        if (_backend1 != null)
            await _backend1.DisposeAsync();

        if (_backend2 != null)
            await _backend2.DisposeAsync();
    }

    [Fact]
    public async Task ShouldProxyTrafficToBackend()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, ProxyPort);
        var stream = client.GetStream();

        // Act
        var message = "Hello World\n";
        var messageBytes = Encoding.UTF8.GetBytes(message);
        await stream.WriteAsync(messageBytes);
        await stream.FlushAsync();

        var buffer = new byte[4096];
        var bytesRead = await stream.ReadAsync(buffer);
        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        // Assert
        Assert.Contains("Backend-", response);
        Assert.Contains("Hello World", response);
    }

    [Fact]
    public async Task ShouldDistributeTrafficRoundRobin()
    {
        // Arrange
        var responses = new List<string>();

        // Act - Make 6 connections
        for (int i = 0; i < 6; i++)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, ProxyPort);
            var stream = client.GetStream();

            var message = $"Request {i}\n";
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(messageBytes);
            await stream.FlushAsync();

            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer);
            var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            responses.Add(response);

            client.Close();
            await Task.Delay(50); // Small delay between connections
        }

        // Assert - Should alternate between backends
        var backend1Count = responses.Count(r => r.Contains("Backend-1"));
        var backend2Count = responses.Count(r => r.Contains("Backend-2"));

        Assert.Equal(3, backend1Count);
        Assert.Equal(3, backend2Count);
    }

    [Fact]
    public async Task ShouldHandleBidirectionalCommunication()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, ProxyPort);
        var stream = client.GetStream();

        // Act - Send multiple messages on same connection
        var messages = new[] { "First\n", "Second\n", "Third\n" };
        var responses = new List<string>();

        foreach (var message in messages)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(messageBytes);
            await stream.FlushAsync();

            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer);
            var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            responses.Add(response);
        }

        // Assert
        Assert.Equal(3, responses.Count);
        Assert.All(responses, r => Assert.Contains("Backend-", r));
    }

    [Fact]
    public async Task ShouldHandleBackendFailure()
    {
        // Arrange - Stop backend 1
        await _backend1!.StopAsync();
        await Task.Delay(100);

        var responses = new List<string>();

        // Act - Make multiple connections
        for (int i = 0; i < 4; i++)
        {
            try
            {
                using var client = new TcpClient();
                client.ReceiveTimeout = 2000;
                client.SendTimeout = 2000;
                await client.ConnectAsync(IPAddress.Loopback, ProxyPort);
                var stream = client.GetStream();

                var message = $"Request {i}\n";
                var messageBytes = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(messageBytes);
                await stream.FlushAsync();

                var buffer = new byte[4096];
                var bytesRead = await stream.ReadAsync(buffer);
                if (bytesRead > 0)
                {
                    var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    responses.Add(response);
                }

                client.Close();
            }
            catch
            {
                // Some connections may fail while health monitor detects the issue
            }

            await Task.Delay(100);
        }

        // Assert - After failures are detected, traffic should go to Backend-2
        var backend2Responses = responses.Where(r => r.Contains("Backend-2")).ToList();
        Assert.NotEmpty(backend2Responses);

        // Restart backend 1 for cleanup
        _backend1 = new TestBackendServer("Backend-1", Backend1Port);
        await _backend1.StartAsync();
    }

    [Fact]
    public async Task ShouldHandleConcurrentConnections()
    {
        // Arrange
        var tasks = new List<Task<string>>();

        // Act - Create 20 concurrent connections
        for (int i = 0; i < 20; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, ProxyPort);
                var stream = client.GetStream();

                var message = $"Concurrent {index}\n";
                var messageBytes = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(messageBytes);
                await stream.FlushAsync();

                var buffer = new byte[4096];
                var bytesRead = await stream.ReadAsync(buffer);
                return Encoding.UTF8.GetString(buffer, 0, bytesRead);
            }));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(20, responses.Length);
        Assert.All(responses, r => Assert.Contains("Backend-", r));

        var backend1Count = responses.Count(r => r.Contains("Backend-1"));
        var backend2Count = responses.Count(r => r.Contains("Backend-2"));

        // Should be roughly evenly distributed (allowing some variance due to timing)
        Assert.InRange(backend1Count, 5, 15);
        Assert.InRange(backend2Count, 5, 15);
    }
}
