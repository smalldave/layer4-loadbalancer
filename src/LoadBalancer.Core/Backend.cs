namespace LoadBalancer.Core;

/// <summary>
/// Represents a backend server that can handle proxied connections.
/// </summary>
public class Backend
{
    /// <summary>
    /// Gets or initializes the backend name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or initializes the backend server address.
    /// </summary>
    public required string Address { get; init; }

    /// <summary>
    /// Gets or initializes the backend server port.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Gets or initializes the backend weight for weighted load balancing.
    /// Default is 1.
    /// </summary>
    public int Weight { get; init; } = 1;

    /// <summary>
    /// Gets the health status of this backend.
    /// </summary>
    public BackendHealth Health { get; } = new();

    public override string ToString() => $"{Name} ({Address}:{Port})";
}
