using System.Net;
using System.Net.Sockets;

namespace Paytness.Security;

public static class PinnedSocketsHttpHandler
{
    public static SocketsHttpHandler Create(
        Uri origin,
        IReadOnlyCollection<string> allowedTargets,
        bool allowPublicTargets)
    {
        TargetPolicy.Validate(origin, allowedTargets, allowPublicTargets);
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectCallback = async (context, cancellationToken) =>
            {
                DnsEndPoint endpoint = context.DnsEndPoint;
                IPAddress[] resolved = IPAddress.TryParse(endpoint.Host, out IPAddress? literal)
                    ? [literal]
                    : await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);

                TargetPolicy.ValidateResolvedAddresses(origin, resolved, allowedTargets, allowPublicTargets);
                IPAddress selected = resolved
                    .OrderBy(static address => address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
                    .ThenBy(static address => address.ToString(), StringComparer.Ordinal)
                    .First();

                var socket = new Socket(selected.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(selected, endpoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
    }
}
