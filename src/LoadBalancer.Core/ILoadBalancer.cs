
namespace LoadBalancer.Core;

/// <summary>
/// Interface for load balancing algorithms.
/// </summary>
public interface ILoadBalancer
{
    /// <summary>
    /// Selects a backend server to handle the next connection.
    /// </summary>
    /// <returns>A backend server, or null if no healthy backends are available.</returns>
    Backend? SelectBackend();
}
