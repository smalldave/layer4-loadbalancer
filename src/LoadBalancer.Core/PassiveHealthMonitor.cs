using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoadBalancer.Core;

/// <summary>
/// Passive health monitor that tracks connection failures and successes.
/// </summary>
public class PassiveHealthMonitor : IHealthMonitor
{
    private readonly ConcurrentDictionary<Backend, ErrorWindow> _errorWindows = new();
    private readonly int _failureThreshold;
    private readonly int _successThreshold;
    private readonly ILogger<PassiveHealthMonitor> _logger;

    public PassiveHealthMonitor(
        IOptions<LoadBalancerOptions> options,
        ILogger<PassiveHealthMonitor> logger)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var passiveOptions = options.Value.Health.PassiveMonitoring;
        _failureThreshold = passiveOptions.FailureThreshold;
        _successThreshold = passiveOptions.SuccessThreshold;

        _logger.LogInformation(
            "Passive health monitor initialized: FailureThreshold={FailureThreshold}, SuccessThreshold={SuccessThreshold}",
            _failureThreshold,
            _successThreshold);
    }

    /// <summary>
    /// Records a connection failure for the given backend.
    /// </summary>
    public void RecordFailure(Backend backend)
    {
        if (backend == null)
        {
            throw new ArgumentNullException(nameof(backend));
        }

        var window = _errorWindows.GetOrAdd(backend, _ => new ErrorWindow());
        window.RecordError();

        var consecutiveFailures = window.ConsecutiveFailures;

        if (consecutiveFailures >= _failureThreshold && backend.Health.IsHealthy)
        {
            backend.Health.MarkUnhealthy();
            _logger.LogWarning(
                "Backend {Backend} marked UNHEALTHY after {Failures} consecutive failures",
                backend,
                consecutiveFailures);
        }
        else
        {
            _logger.LogDebug(
                "Backend {Backend} failure recorded ({Failures}/{Threshold})",
                backend,
                consecutiveFailures,
                _failureThreshold);
        }
    }

    /// <summary>
    /// Records a successful connection for the given backend.
    /// </summary>
    public void RecordSuccess(Backend backend)
    {
        if (backend == null)
        {
            throw new ArgumentNullException(nameof(backend));
        }

        var window = _errorWindows.GetOrAdd(backend, _ => new ErrorWindow());
        window.RecordSuccess();

        var consecutiveSuccesses = window.ConsecutiveSuccesses;

        if (consecutiveSuccesses >= _successThreshold && !backend.Health.IsHealthy)
        {
            backend.Health.MarkHealthy();
            _logger.LogInformation(
                "Backend {Backend} marked HEALTHY after {Successes} consecutive successes",
                backend,
                consecutiveSuccesses);
        }
        else if (!backend.Health.IsHealthy)
        {
            _logger.LogDebug(
                "Backend {Backend} success recorded ({Successes}/{Threshold})",
                backend,
                consecutiveSuccesses,
                _successThreshold);
        }
    }
}

/// <summary>
/// Thread-safe error tracking window for a backend.
/// </summary>
internal class ErrorWindow
{
    // Counts of consecutive failures/successes; guarded by _sync
    private int _consecutiveFailures;
    private int _consecutiveSuccesses;
    private readonly object _sync = new object();

    /// <summary>
    /// Number of consecutive failures recorded.
    /// </summary>
    public int ConsecutiveFailures
    {
        get
        {
            lock (_sync)
            {
                return _consecutiveFailures;
            }
        }
    }

    /// <summary>
    /// Number of consecutive successes recorded.
    /// </summary>
    public int ConsecutiveSuccesses
    {
        get
        {
            lock (_sync)
            {
                return _consecutiveSuccesses;
            }
        }
    }

    /// <summary>
    /// Records an error: increments failures and resets successes to zero.
    /// </summary>
    public void RecordError()
    {
        lock (_sync)
        {
            _consecutiveFailures++;
            _consecutiveSuccesses = 0;
        }
    }

    /// <summary>
    /// Records a success: increments successes and resets failures to zero.
    /// </summary>
    public void RecordSuccess()
    {
        lock (_sync)
        {
            _consecutiveSuccesses++;
            _consecutiveFailures = 0;
        }
    }
}
