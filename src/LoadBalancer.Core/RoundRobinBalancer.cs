
namespace LoadBalancer.Core;

/// <summary>
/// Thread-safe Round Robin load balancer implementation.
/// Uses Interlocked.Increment for lock-free operation.
/// </summary>
public class RoundRobinBalancer : ILoadBalancer
{
    private readonly IBackendPool _backendPool;
    private int _current = -1;

    public RoundRobinBalancer(IBackendPool backendPool)
    {
        _backendPool = backendPool ?? throw new ArgumentNullException(nameof(backendPool));
    }

    /// <summary>
    /// Selects the next backend in round-robin order.
    /// Thread-safe and lock-free using Interlocked.Increment.
    /// </summary>
    public Backend? SelectBackend()
    {
        var backends = _backendPool.GetHealthyBackends();

        if (backends.Count == 0)
        {
            return null;
        }

        // Thread-safe increment with atomic operation
        var next = Interlocked.Increment(ref _current);
        // Mask off sign bit to avoid negative modulo results when _current overflows
        var index = (next & 0x7FFFFFFF) % backends.Count;
        return backends[index];
    }
}
