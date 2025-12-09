namespace LoadBalancer.Core;

/// <summary>
/// Configuration options for the load balancer.
/// </summary>
public class LoadBalancerOptions
{
    /// <summary>
    /// Gets or sets the address to listen on.
    /// Default is "0.0.0.0" (all interfaces).
    /// </summary>
    public string ListenAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// Gets or sets the port to listen on.
    /// Default is 8000.
    /// </summary>
    public int ListenPort { get; set; } = 8000;

    /// <summary>
    /// Gets or sets the list of backend servers.
    /// </summary>
    public List<Backend> Backends { get; set; } = new();

    /// <summary>
    /// Gets or sets health check options.
    /// </summary>
    public HealthOptions Health { get; set; } = new();

    /// <summary>
    /// Gets or sets connection options.
    /// </summary>
    public ConnectionOptions Connection { get; set; } = new();
}

/// <summary>
/// Health check configuration options.
/// </summary>
public class HealthOptions
{
    /// <summary>
    /// Gets or sets passive monitoring options.
    /// </summary>
    public PassiveMonitoringOptions PassiveMonitoring { get; set; } = new();
}

/// <summary>
/// Passive health monitoring configuration.
/// </summary>
public class PassiveMonitoringOptions
{
    /// <summary>
    /// Gets or sets whether passive monitoring is enabled.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of consecutive failures before marking a backend unhealthy.
    /// Default is 3.
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// Gets or sets the number of consecutive successes before marking a backend healthy.
    /// Default is 2.
    /// </summary>
    public int SuccessThreshold { get; set; } = 2;

    /// <summary>
    /// Gets or sets the time window in seconds for tracking errors.
    /// Default is 60 seconds.
    /// </summary>
    public int TimeWindowSeconds { get; set; } = 60;
}

/// <summary>
/// Connection configuration options.
/// </summary>
public class ConnectionOptions
{
    /// <summary>
    /// Gets or sets the connection timeout in milliseconds.
    /// Default is 5000ms (5 seconds).
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the idle timeout in milliseconds.
    /// Default is 300000ms (5 minutes).
    /// </summary>
    public int IdleTimeoutMs { get; set; } = 300000;

    /// <summary>
    /// Gets or sets the maximum number of concurrent connections.
    /// Default is 10000.
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = 10000;
}
