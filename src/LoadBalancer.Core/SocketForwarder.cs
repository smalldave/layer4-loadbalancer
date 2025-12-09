using System.Net.Sockets;

namespace LoadBalancer.Core;

/// <summary>
/// Bidirectional socket forwarder with proper TCP half-close handling.
/// Unlike the pipe-based BidirectionalForwarder, this works directly with sockets
/// and explicitly propagates FIN packets between connections.
/// </summary>
public static class SocketForwarder
{
    /// <summary>
    /// Forwards data bidirectionally between two sockets with proper half-close handling.
    /// When one side closes their write (sends FIN), we propagate it to the other side.
    /// </summary>
    public static async Task ForwardAsync(
        Socket clientSocket,
        Socket backendSocket,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var clientToBackend = CopyAsync(clientSocket, backendSocket, "client→backend", cts.Token);
        var backendToClient = CopyAsync(backendSocket, clientSocket, "backend→client", cts.Token);

        try
        {
            // Wait for either direction to complete
            var completedTask = await Task.WhenAny(clientToBackend, backendToClient);

            if (completedTask.IsFaulted)
            {
                // One direction failed - cancel the other
                await cts.CancelAsync();
            }
            else if (completedTask == clientToBackend)
            {
                // Client closed write side - propagate FIN to backend
                try
                {
                    backendSocket.Shutdown(SocketShutdown.Send);
                }
                catch (SocketException)
                {
                    // Backend already closed
                }
            }
            else
            {
                // Backend closed write side - propagate FIN to client
                try
                {
                    clientSocket.Shutdown(SocketShutdown.Send);
                }
                catch (SocketException)
                {
                    // Client already closed
                }
            }

            // Wait for both directions to complete
            await Task.WhenAll(clientToBackend, backendToClient);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
    }

    /// <summary>
    /// Copies data from one socket to another until EOF or cancellation.
    /// </summary>
    private static async Task CopyAsync(
        Socket from,
        Socket to,
        string direction,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await from.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);

                if (bytesRead == 0)
                {
                    // EOF - other side closed their write (sent FIN)
                    break;
                }

                await to.SendAsync(buffer.AsMemory(0, bytesRead), SocketFlags.None, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (SocketException)
        {
            // Connection reset or other socket error
        }
    }
}
