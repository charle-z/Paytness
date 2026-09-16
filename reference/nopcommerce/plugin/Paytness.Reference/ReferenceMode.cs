using System.Net;

namespace Paytness.Reference;

internal static class ReferenceMode
{
    public static bool Enabled => string.Equals(
        Environment.GetEnvironmentVariable("PAYTNESS_REFERENCE_MODE"),
        "1",
        StringComparison.Ordinal);

    public static bool AcceptStaleState => string.Equals(
        Environment.GetEnvironmentVariable("PAYTNESS_REFERENCE_MUTATION"),
        "accept-stale-state",
        StringComparison.Ordinal);

    public static bool UnstableIdempotency => string.Equals(
        Environment.GetEnvironmentVariable("PAYTNESS_REFERENCE_MUTATION"),
        "unstable-idempotency",
        StringComparison.Ordinal);

    public static string StoreOrigin => Environment.GetEnvironmentVariable("PAYTNESS_REFERENCE_STORE_ORIGIN")
        ?? "http://127.0.0.1:8080/";

    private static bool IsAllowedProviderHost(string host)
    {
        if (string.Equals(host, "paytness", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address);
    }

    public static Uri GetProviderOrigin()
    {
        string value = Environment.GetEnvironmentVariable("PAYTNESS_PROVIDER_ORIGIN") ?? "http://127.0.0.1:8787";
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? origin)
            || origin.Scheme != Uri.UriSchemeHttp
            || origin.AbsolutePath != "/"
            || origin.Port != 8787
            || !IsAllowedProviderHost(origin.Host))
            throw new InvalidOperationException("PAYTNESS_PROVIDER_ORIGIN must be HTTP on port 8787 and target loopback or the paytness service.");
        return origin;
    }
}
