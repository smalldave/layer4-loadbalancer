using LoadBalancer.Core;

namespace LoadBalancer.Core.UnitTests;

/// <summary>
/// Simple mock implementation of IBackendPool for unit tests.
/// </summary>
internal class MockBackendPool(IReadOnlyList<Backend> backends) : IBackendPool
{
    public IReadOnlyList<Backend> GetHealthyBackends() => backends;

    public void UpdateBackends(IEnumerable<Backend> backends)
    {
        throw new NotImplementedException();
    }
}
