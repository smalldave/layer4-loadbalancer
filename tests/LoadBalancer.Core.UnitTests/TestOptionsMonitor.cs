using Microsoft.Extensions.Options;

namespace LoadBalancer.Core.UnitTests;

/// <summary>
/// Simple IOptionsMonitor implementation for tests.
/// </summary>
internal class TestOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
{
    public T CurrentValue { get; } = currentValue;

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
