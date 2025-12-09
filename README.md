# Layer 4 TCP Load Balancer

A .NET 10.0 Layer 4 (TCP) load balancer using raw sockets.

## Quick Start

### Prerequisites

- .NET 10.0 SDK

### Running the Load Balancer

1. **Clone and build**:

   ```bash
   dotnet build
   ```

2. **Configure backends** (edit `src/LoadBalancer.Host/appsettings.json`):

   ```json
   {
     "LoadBalancer": {
       "ListenAddress": "0.0.0.0",
       "ListenPort": 8000,
       "Backends": [
         { "Name": "backend-1", "Address": "127.0.0.1", "Port": 9001 },
         { "Name": "backend-2", "Address": "127.0.0.1", "Port": 9002 }
       ]
     }
   }
   ```

3. **Run the load balancer**:
   ```bash
   dotnet run --project src/LoadBalancer.Host
   ```

## Configuration

### appsettings.json

```json
{
  "LoadBalancer": {
    "ListenAddress": "0.0.0.0",
    "ListenPort": 8000,
    "Backends": [
      { "Name": "backend-1", "Address": "127.0.0.1", "Port": 9001 },
      { "Name": "backend-2", "Address": "127.0.0.1", "Port": 9002 }
    ],
    "Health": {
      "PassiveMonitoring": {
        "FailureThreshold": 3,
        "SuccessThreshold": 2
      }
    },
    "Connection": {
      "ConnectTimeoutMs": 5000
    }
  }
}
```

## Architecture

```
Client → SimpleTcpProxy → RoundRobinBalancer → Backend Server
              ↓
       SocketForwarder (bidirectional)
              ↓
       PassiveHealthMonitor (on failure)
```

## Health Checking

The load balancer uses passive health monitoring:

- **3 consecutive failures** → Backend marked unhealthy (removed from rotation)
- **2 consecutive successes** → Backend marked healthy (restored to rotation)
