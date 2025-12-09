using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoadBalancer.Core;

/// <summary>
/// Manages the pool of backend servers with live configuration reload support.
/// </summary>
public class BackendPool : IBackendPool
{
    private volatile IReadOnlyList<Backend> _backends = Array.Empty<Backend>();
    private readonly ILogger<BackendPool> _logger;

    public BackendPool(
        IOptionsMonitor<LoadBalancerOptions> options,
        ILogger<BackendPool> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // Initialize with current configuration
        UpdateBackends(options.CurrentValue.Backends);

        // Register for configuration changes (live reload)
        options.OnChange(opts =>
        {
            _logger.LogInformation("Configuration changed, reloading backends");
            UpdateBackends(opts.Backends);
        });
    }

    /// <summary>
    /// Gets the list of currently healthy backends.
    /// </summary>
    public IReadOnlyList<Backend> GetHealthyBackends()
    {
        return _backends.Where(b => b.Health.IsHealthy).ToList();
    }

    /// <summary>
    /// Updates the backend pool with a new list of backends.
    /// Thread-safe using volatile field.
    /// </summary>
    public void UpdateBackends(IEnumerable<Backend> backends)
    {
        if (backends == null)
        {
            throw new ArgumentNullException(nameof(backends));
        }

        _backends = backends.ToList().AsReadOnly();

        _logger.LogInformation(
            "Updated backend pool: {Count} backend(s) configured",
            _backends.Count);

        foreach (var backend in _backends)
        {
            _logger.LogDebug("Backend: {Backend}", backend);
        }
    }
}
