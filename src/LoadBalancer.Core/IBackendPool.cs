
namespace LoadBalancer.Core;

/// <summary>
/// Interface for managing the pool of backend servers.
/// </summary>
public interface IBackendPool
{
    /// <summary>
    /// Gets the list of currently healthy backends.
    /// </summary>
    /// <returns>A read-only list of healthy backends.</returns>
    IReadOnlyList<Backend> GetHealthyBackends();

    /// <summary>
    /// Updates the backend pool with a new list of backends.
    /// </summary>
    /// <param name="backends">The new list of backends.</param>
    void UpdateBackends(IEnumerable<Backend> backends);
}
