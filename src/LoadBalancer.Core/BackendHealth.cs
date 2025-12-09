namespace LoadBalancer.Core;

/// <summary>
/// Thread-safe health status for a backend server.
/// </summary>
public class BackendHealth
{
    private int _isHealthy = 1; // 1 = healthy, 0 = unhealthy

    /// <summary>
    /// Gets whether the backend is currently healthy.
    /// </summary>
    public bool IsHealthy => Interlocked.CompareExchange(ref _isHealthy, 0, 0) == 1;

    /// <summary>
    /// Marks the backend as healthy in a thread-safe manner.
    /// </summary>
    public void MarkHealthy()
    {
        Interlocked.Exchange(ref _isHealthy, 1);
    }

    /// <summary>
    /// Marks the backend as unhealthy in a thread-safe manner.
    /// </summary>
    public void MarkUnhealthy()
    {
        Interlocked.Exchange(ref _isHealthy, 0);
    }
}
