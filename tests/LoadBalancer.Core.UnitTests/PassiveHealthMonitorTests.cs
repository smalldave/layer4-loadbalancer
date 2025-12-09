using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using LoadBalancer.Core;

namespace LoadBalancer.Core.UnitTests;

[Trait("Category", "Unit")]
public class PassiveHealthMonitorTests
{
    [Fact]
    public void RecordFailure_MarksUnhealthy_AfterThresholdReached()
    {
        // Arrange
        var options = CreateOptions(failureThreshold: 3, successThreshold: 2);
        var monitor = new PassiveHealthMonitor(options, NullLogger<PassiveHealthMonitor>.Instance);
        var backend = new Backend { Name = "Test", Address = "127.0.0.1", Port = 8080 };

        Assert.True(backend.Health.IsHealthy);

        // Act - Record failures up to threshold
        monitor.RecordFailure(backend);
        monitor.RecordFailure(backend);
        Assert.True(backend.Health.IsHealthy); // Still healthy

        monitor.RecordFailure(backend); // 3rd failure - threshold reached

        // Assert
        Assert.False(backend.Health.IsHealthy);
    }

    [Fact]
    public void RecordSuccess_MarksHealthy_AfterThresholdReached()
    {
        // Arrange
        var options = CreateOptions(failureThreshold: 3, successThreshold: 2);
        var monitor = new PassiveHealthMonitor(options, NullLogger<PassiveHealthMonitor>.Instance);
        var backend = new Backend { Name = "Test", Address = "127.0.0.1", Port = 8080 };

        // Start unhealthy
        backend.Health.MarkUnhealthy();
        Assert.False(backend.Health.IsHealthy);

        // Act - Record successes up to threshold
        monitor.RecordSuccess(backend);
        Assert.False(backend.Health.IsHealthy); // Still unhealthy

        monitor.RecordSuccess(backend); // 2nd success - threshold reached

        // Assert
        Assert.True(backend.Health.IsHealthy);
    }

    [Fact]
    public void RecordSuccess_ResetsFailureCount()
    {
        // Arrange
        var options = CreateOptions(failureThreshold: 3, successThreshold: 2);
        var monitor = new PassiveHealthMonitor(options, NullLogger<PassiveHealthMonitor>.Instance);
        var backend = new Backend { Name = "Test", Address = "127.0.0.1", Port = 8080 };

        // Record 2 failures (one away from threshold)
        monitor.RecordFailure(backend);
        monitor.RecordFailure(backend);

        // Act - Success should reset the counter
        monitor.RecordSuccess(backend);

        // Record 2 more failures (should not reach threshold since counter reset)
        monitor.RecordFailure(backend);
        monitor.RecordFailure(backend);

        // Assert - Should still be healthy
        Assert.True(backend.Health.IsHealthy);
    }

    [Fact]
    public void RecordFailure_ResetsSuccessCount()
    {
        // Arrange
        var options = CreateOptions(failureThreshold: 3, successThreshold: 3);
        var monitor = new PassiveHealthMonitor(options, NullLogger<PassiveHealthMonitor>.Instance);
        var backend = new Backend { Name = "Test", Address = "127.0.0.1", Port = 8080 };
        backend.Health.MarkUnhealthy();

        // Record 2 successes (one away from threshold)
        monitor.RecordSuccess(backend);
        monitor.RecordSuccess(backend);

        // Act - Failure should reset the counter
        monitor.RecordFailure(backend);

        // Record 2 more successes (should not reach threshold since counter reset)
        monitor.RecordSuccess(backend);
        monitor.RecordSuccess(backend);

        // Assert - Should still be unhealthy
        Assert.False(backend.Health.IsHealthy);
    }

    private static IOptions<LoadBalancerOptions> CreateOptions(int failureThreshold, int successThreshold)
    {
        return Options.Create(new LoadBalancerOptions
        {
            Health = new HealthOptions
            {
                PassiveMonitoring = new PassiveMonitoringOptions
                {
                    FailureThreshold = failureThreshold,
                    SuccessThreshold = successThreshold
                }
            }
        });
    }
}
