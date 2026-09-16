using System.Net;

namespace Paytness.Execution;

public sealed record RunOptions(
    Uri SutOrigin,
    IPEndPoint ProviderEndpoint,
    IReadOnlyCollection<string> AllowedTargets,
    bool AllowPublicTargets,
    string? WebhookSecret = null);
