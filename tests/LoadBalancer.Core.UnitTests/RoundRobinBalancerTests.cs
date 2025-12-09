using LoadBalancer.Core;

namespace LoadBalancer.Core.UnitTests;

[Trait("Category", "Unit")]
public class RoundRobinBalancerTests
{
    [Fact]
    public void SelectBackend_ReturnsNull_WhenNoHealthyBackends()
    {
        // Arrange
        var pool = new MockBackendPool(new List<Backend>());
        var balancer = new RoundRobinBalancer(pool);

        // Act
        var result = balancer.SelectBackend();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SelectBackend_ReturnsSingleBackend_WhenOnlyOneAvailable()
    {
        // Arrange
        var backend = new Backend { Name = "Backend-1", Address = "127.0.0.1", Port = 8080 };
        var pool = new MockBackendPool(new List<Backend> { backend });
        var balancer = new RoundRobinBalancer(pool);

        // Act
        var result1 = balancer.SelectBackend();
        var result2 = balancer.SelectBackend();
        var result3 = balancer.SelectBackend();

        // Assert
        Assert.Same(backend, result1);
        Assert.Same(backend, result2);
        Assert.Same(backend, result3);
    }

    [Fact]
    public void SelectBackend_CyclesThroughBackends_InRoundRobinOrder()
    {
        // Arrange
        var backend1 = new Backend { Name = "Backend-1", Address = "127.0.0.1", Port = 8081 };
        var backend2 = new Backend { Name = "Backend-2", Address = "127.0.0.1", Port = 8082 };
        var backend3 = new Backend { Name = "Backend-3", Address = "127.0.0.1", Port = 8083 };
        var pool = new MockBackendPool(new List<Backend> { backend1, backend2, backend3 });
        var balancer = new RoundRobinBalancer(pool);

        // Act - Get 6 selections (2 full cycles)
        var selections = new List<Backend?>();
        for (int i = 0; i < 6; i++)
        {
            selections.Add(balancer.SelectBackend());
        }

        // Assert - Should cycle: 1, 2, 3, 1, 2, 3
        Assert.Same(backend1, selections[0]);
        Assert.Same(backend2, selections[1]);
        Assert.Same(backend3, selections[2]);
        Assert.Same(backend1, selections[3]);
        Assert.Same(backend2, selections[4]);
        Assert.Same(backend3, selections[5]);
    }

    [Fact]
    public async Task SelectBackend_IsThreadSafe_UnderConcurrentAccess()
    {
        // Arrange
        var backend1 = new Backend { Name = "Backend-1", Address = "127.0.0.1", Port = 8081 };
        var backend2 = new Backend { Name = "Backend-2", Address = "127.0.0.1", Port = 8082 };
        var pool = new MockBackendPool(new List<Backend> { backend1, backend2 });
        var balancer = new RoundRobinBalancer(pool);

        // Act - Concurrent selections
        var results = new System.Collections.Concurrent.ConcurrentBag<Backend?>();
        var tasks = Enumerable.Range(0, 100).Select(_ =>
            Task.Run(() => results.Add(balancer.SelectBackend())));
        await Task.WhenAll(tasks);

        // Assert - All should be valid backends, roughly evenly distributed
        Assert.Equal(100, results.Count);
        Assert.All(results, r => Assert.NotNull(r));

        var backend1Count = results.Count(r => r == backend1);
        var backend2Count = results.Count(r => r == backend2);
        Assert.Equal(100, backend1Count + backend2Count);
        Assert.InRange(backend1Count, 40, 60); // Allow some variance
    }
}
