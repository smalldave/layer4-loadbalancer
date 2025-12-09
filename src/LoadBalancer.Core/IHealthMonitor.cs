
namespace LoadBalancer.Core;

/// <summary>
/// Interface for monitoring backend health.
/// </summary>
public interface IHealthMonitor
{
    /// <summary>
    /// Records a successful connection to a backend.
    /// </summary>
    /// <param name="backend">The backend that succeeded.</param>
    void RecordSuccess(Backend backend);

    /// <summary>
    /// Records a failed connection to a backend.
    /// </summary>
    /// <param name="backend">The backend that failed.</param>
    void RecordFailure(Backend backend);
}
