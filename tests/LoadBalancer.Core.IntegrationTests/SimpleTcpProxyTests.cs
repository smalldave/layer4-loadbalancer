using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using LoadBalancer.Core;

namespace LoadBalancer.Core.IntegrationTests;

/// <summary>
/// Tests for SimpleTcpProxy with proper half-close handling.
/// These tests use raw sockets instead of Kestrel to verify TCP behavior.
/// </summary>
[Trait("Category", "Integration")]
public class SimpleTcpProxyTests : IAsyncLifetime
{
    private const int ProxyPort = 18200;
    private const int SlowBackendPort = 19200;

    private SlowResponseBackendServer? _slowBackend;
    private SimpleTcpProxy? _proxy;
    private IBackendPool? _backendPool;

    public async Task InitializeAsync()
    {
        // Start slow backend: 5 parts with 50ms delay each
        _slowBackend = new SlowResponseBackendServer(
            "SlowBackend",
            SlowBackendPort,
            delayBetweenParts: TimeSpan.FromMilliseconds(50),
            responseParts: 5);

        await _slowBackend.StartAsync();
        await Task.Delay(100);

        // Create backend pool and services
        var options = new LoadBalancerOptions
        {
            Backends = new List<Backend>
            {
                new() { Name = "SlowBackend", Address = "127.0.0.1", Port = SlowBackendPort }
            }
        };
        var optionsMonitor = new TestOptionsMonitor<LoadBalancerOptions>(options);

        _backendPool = new BackendPool(optionsMonitor, NullLogger<BackendPool>.Instance);
        var healthMonitor = new PassiveHealthMonitor(Options.Create(options), NullLogger<PassiveHealthMonitor>.Instance);
        var loadBalancer = new RoundRobinBalancer(_backendPool);

        // Start SimpleTcpProxy
        _proxy = new SimpleTcpProxy(
            "127.0.0.1",
            ProxyPort,
            loadBalancer,
            healthMonitor,
            connectTimeout: TimeSpan.FromSeconds(5));

        await _proxy.StartAsync();
        await Task.Delay(100);
    }

    public async Task DisposeAsync()
    {
        if (_proxy != null)
            await _proxy.DisposeAsync();

        if (_slowBackend != null)
            await _slowBackend.DisposeAsync();
    }

    /// <summary>
    /// Tests that SimpleTcpProxy properly handles TCP half-close.
    /// When client closes write side, proxy should forward FIN to backend
    /// and continue receiving backend's response.
    /// </summary>
    [Fact]
    public async Task SimpleTcpProxy_ShouldReceiveFullResponse_WhenClientClosesWriteSideEarly()
    {
        // Arrange
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(IPAddress.Loopback, ProxyPort);

        // Act
        // 1. Send request
        var request = "REQUEST\n";
        var requestBytes = Encoding.UTF8.GetBytes(request);
        await socket.SendAsync(requestBytes, SocketFlags.None);

        // 2. Immediately shutdown write side (half-close)
        // This sends FIN to proxy, which should forward it to backend
        socket.Shutdown(SocketShutdown.Send);

        // 3. Read all response data
        var responseBuilder = new StringBuilder();
        var buffer = new byte[4096];

        while (true)
        {
            var bytesRead = await socket.ReceiveAsync(buffer, SocketFlags.None);
            if (bytesRead == 0)
                break;

            responseBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
        }

        var response = responseBuilder.ToString();

        // Assert - Should receive ALL parts despite early half-close
        Assert.Contains("[SlowBackend] Part 1 of 5", response);
        Assert.Contains("[SlowBackend] Part 2 of 5", response);
        Assert.Contains("[SlowBackend] Part 3 of 5", response);
        Assert.Contains("[SlowBackend] Part 4 of 5", response);
        Assert.Contains("[SlowBackend] Part 5 of 5", response);
        Assert.Contains("[SlowBackend] COMPLETE", response);
    }

    /// <summary>
    /// Tests basic proxy functionality without half-close.
    /// </summary>
    [Fact]
    public async Task SimpleTcpProxy_ShouldProxyTrafficToBackend()
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
        Assert.Contains("SlowBackend", response);
        Assert.Contains("Part 1", response);
    }
}
