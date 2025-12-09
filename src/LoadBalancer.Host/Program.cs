using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LoadBalancer.Core;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Build service provider
var services = new ServiceCollection();

// Configure logging
services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Bind LoadBalancer configuration
services.Configure<LoadBalancerOptions>(configuration.GetSection("LoadBalancer"));

// Register core services
services.AddSingleton<IBackendPool, BackendPool>();
services.AddSingleton<IHealthMonitor, PassiveHealthMonitor>();
services.AddSingleton<ILoadBalancer, RoundRobinBalancer>();

// Suppress ASP0000: This is the application composition root, not library code
#pragma warning disable ASP0000
var serviceProvider = services.BuildServiceProvider();
#pragma warning restore ASP0000

// Get configuration and services
var lbOptions = serviceProvider.GetRequiredService<IOptions<LoadBalancerOptions>>().Value;
var loadBalancer = serviceProvider.GetRequiredService<ILoadBalancer>();
var healthMonitor = serviceProvider.GetRequiredService<IHealthMonitor>();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

if (lbOptions.Backends.Count == 0)
{
    throw new InvalidOperationException("No backends configured in LoadBalancer configuration");
}

// Create and start the TCP proxy
await using var proxy = new SimpleTcpProxy(
    lbOptions.ListenAddress,
    lbOptions.ListenPort,
    loadBalancer,
    healthMonitor,
    TimeSpan.FromMilliseconds(lbOptions.Connection.ConnectTimeoutMs),
    serviceProvider.GetRequiredService<ILogger<SimpleTcpProxy>>());

// Log startup information
logger.LogInformation("Layer 4 Load Balancer starting...");
logger.LogInformation("Listening on {Address}:{Port}", lbOptions.ListenAddress, lbOptions.ListenPort);
logger.LogInformation("Backend servers:");
foreach (var backend in lbOptions.Backends)
{
    logger.LogInformation("  - {Backend}", backend);
}

await proxy.StartAsync();

// Wait for shutdown signal
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    logger.LogInformation("Shutdown signal received");
};

logger.LogInformation("Press Ctrl+C to stop");

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // Expected on shutdown
}

logger.LogInformation("Shutting down...");
await proxy.StopAsync();
logger.LogInformation("Stopped");
