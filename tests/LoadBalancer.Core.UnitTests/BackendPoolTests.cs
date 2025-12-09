using Microsoft.Extensions.Logging.Abstractions;
using LoadBalancer.Core;

namespace LoadBalancer.Core.UnitTests;

[Trait("Category", "Unit")]
public class BackendPoolTests
{
    [Fact]
    public void GetHealthyBackends_ReturnsOnlyHealthyBackends()
    {
        // Arrange
        var options = CreateOptions(new List<Backend>
        {
            new() { Name = "Healthy-1", Address = "127.0.0.1", Port = 8081 },
            new() { Name = "Healthy-2", Address = "127.0.0.1", Port = 8082 }
        });
        var pool = new BackendPool(options, NullLogger<BackendPool>.Instance);

        // Mark one as unhealthy
        var backends = pool.GetHealthyBackends();
        backends[0].Health.MarkUnhealthy();

        // Act
        var healthyBackends = pool.GetHealthyBackends();

        // Assert
        Assert.Single(healthyBackends);
        Assert.Equal("Healthy-2", healthyBackends[0].Name);
    }

    [Fact]
    public void GetHealthyBackends_ReturnsEmpty_WhenAllUnhealthy()
    {
        // Arrange
        var options = CreateOptions(new List<Backend>
        {
            new() { Name = "Backend-1", Address = "127.0.0.1", Port = 8081 }
        });
        var pool = new BackendPool(options, NullLogger<BackendPool>.Instance);

        // Mark as unhealthy
        pool.GetHealthyBackends()[0].Health.MarkUnhealthy();

        // Act
        var healthyBackends = pool.GetHealthyBackends();

        // Assert
        Assert.Empty(healthyBackends);
    }

    [Fact]
    public void UpdateBackends_ReplacesExistingBackends()
    {
        // Arrange
        var options = CreateOptions(new List<Backend>
        {
            new() { Name = "Original", Address = "127.0.0.1", Port = 8081 }
        });
        var pool = new BackendPool(options, NullLogger<BackendPool>.Instance);

        // Act
        pool.UpdateBackends(new List<Backend>
        {
            new() { Name = "New-1", Address = "127.0.0.1", Port = 9001 },
            new() { Name = "New-2", Address = "127.0.0.1", Port = 9002 }
        });

        // Assert
        var backends = pool.GetHealthyBackends();
        Assert.Equal(2, backends.Count);
        Assert.Equal("New-1", backends[0].Name);
        Assert.Equal("New-2", backends[1].Name);
    }

    private static TestOptionsMonitor<LoadBalancerOptions> CreateOptions(List<Backend> backends)
    {
        return new TestOptionsMonitor<LoadBalancerOptions>(new LoadBalancerOptions
        {
            Backends = backends
        });
    }
}
