using System.Net;
using System.Net.Sockets;

namespace Paytness.Security;

public sealed class TargetPolicyException(string message) : Exception(message);

public static class TargetPolicy
{
    public static void Validate(Uri origin, IReadOnlyCollection<string> allowedTargets, bool allowPublicTargets)
    {
        ValidateOriginShape(origin);

        int port = EffectivePort(origin);
        string target = $"{origin.Host}:{port}";
        if (origin.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return;

        if (IPAddress.TryParse(origin.Host, out IPAddress? address))
        {
            ValidateResolvedAddresses(origin, [address], allowedTargets, allowPublicTargets);
            return;
        }

        if (!ContainsTarget(allowedTargets, target))
            throw new TargetPolicyException($"Target '{target}' requires explicit --allow-target.");
    }

    public static void ValidateResolvedAddresses(
        Uri origin,
        IReadOnlyCollection<IPAddress> addresses,
        IReadOnlyCollection<string> allowedTargets,
        bool allowPublicTargets)
    {
        ValidateOriginShape(origin);
        if (addresses.Count == 0) throw new TargetPolicyException("SUT hostname resolved to no addresses.");

        int port = EffectivePort(origin);
        string target = $"{origin.Host}:{port}";
        bool localhost = origin.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        bool literalLoopback = IPAddress.TryParse(origin.Host, out IPAddress? literal) && IPAddress.IsLoopback(literal);
        if (!localhost && !literalLoopback && !ContainsTarget(allowedTargets, target))
            throw new TargetPolicyException($"Target '{target}' requires explicit --allow-target.");

        foreach (IPAddress address in addresses)
        {
            if (IsHardDenied(address)) throw new TargetPolicyException("SUT address is in a hard-denied network range.");
            if (localhost && !IPAddress.IsLoopback(address))
                throw new TargetPolicyException("localhost resolved to a non-loopback address.");
            if (!IPAddress.IsLoopback(address) && !IsPrivate(address) && !allowPublicTargets)
                throw new TargetPolicyException("Public SUT targets require --allow-public-targets.");
        }
    }

    internal static int EffectivePort(Uri origin) => origin.IsDefaultPort
        ? origin.Scheme == Uri.UriSchemeHttps ? 443 : 80
        : origin.Port;

    private static void ValidateOriginShape(Uri origin)
    {
        if (!origin.IsAbsoluteUri || (origin.Scheme != Uri.UriSchemeHttp && origin.Scheme != Uri.UriSchemeHttps))
            throw new TargetPolicyException("SUT origin must use http or https.");
        if (!string.IsNullOrEmpty(origin.UserInfo)) throw new TargetPolicyException("SUT origin must not contain user information.");
        if (origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
            throw new TargetPolicyException("--sut must be an origin without path, query, or fragment.");
    }

    private static bool ContainsTarget(IReadOnlyCollection<string> targets, string expected) =>
        targets.Any(target => string.Equals(target, expected, StringComparison.OrdinalIgnoreCase));

    private static bool IsPrivate(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        byte[] ipv6 = address.GetAddressBytes();
        return (ipv6[0] & 0xFE) == 0xFC;
    }

    private static bool IsHardDenied(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return true;
        if (address.IsIPv6Multicast) return true;
        byte[] bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            if (bytes[0] is >= 224 and <= 239) return true;
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            if (bytes[0] == 0) return true;
        }
        else if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
        {
            return true;
        }

        return false;
    }
}
