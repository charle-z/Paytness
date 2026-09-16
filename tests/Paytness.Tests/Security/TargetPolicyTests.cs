using System.Net;
using Paytness.Security;

namespace Paytness.Tests.Security;

public sealed class TargetPolicyTests
{
    [Fact]
    public void LoopbackIsAllowedWithoutExplicitTarget()
    {
        TargetPolicy.Validate(new Uri("http://127.0.0.1:5000"), [], false);
    }

    [Fact]
    public void PrivateAddressRequiresExactAllowTarget()
    {
        Assert.Throws<TargetPolicyException>(() => TargetPolicy.Validate(new Uri("http://192.168.1.20:8080"), [], false));
        TargetPolicy.Validate(new Uri("http://192.168.1.20:8080"), ["192.168.1.20:8080"], false);
    }

    [Fact]
    public void PublicAddressRequiresExplicitPublicOptIn()
    {
        Uri origin = new("https://8.8.8.8");
        Assert.Throws<TargetPolicyException>(() => TargetPolicy.Validate(origin, ["8.8.8.8:443"], false));
        TargetPolicy.Validate(origin, ["8.8.8.8:443"], true);
    }

    [Fact]
    public void HostnameRequiresExactAllowTarget()
    {
        Uri origin = new("https://example.test");
        Assert.Throws<TargetPolicyException>(() => TargetPolicy.Validate(origin, [], true));
        TargetPolicy.Validate(origin, ["example.test:443"], true);
    }

    [Fact]
    public void ResolvedHostnameRejectsHardDeniedAddress()
    {
        Uri origin = new("https://example.test");
        Assert.Throws<TargetPolicyException>(() => TargetPolicy.ValidateResolvedAddresses(
            origin,
            [IPAddress.Parse("10.0.0.4"), IPAddress.Any],
            ["example.test:443"],
            true));
    }

    [Fact]
    public void ResolvedPublicHostnameRequiresPublicOptIn()
    {
        Uri origin = new("https://example.test");
        IPAddress publicAddress = IPAddress.Parse("203.0.113.10");
        Assert.Throws<TargetPolicyException>(() => TargetPolicy.ValidateResolvedAddresses(
            origin, [publicAddress], ["example.test:443"], false));
        TargetPolicy.ValidateResolvedAddresses(origin, [publicAddress], ["example.test:443"], true);
    }

    [Fact]
    public void LocalhostMustResolveOnlyToLoopback()
    {
        Uri origin = new("http://localhost:5000");
        TargetPolicy.ValidateResolvedAddresses(origin, [IPAddress.Loopback, IPAddress.IPv6Loopback], [], false);
        Assert.Throws<TargetPolicyException>(() => TargetPolicy.ValidateResolvedAddresses(
            origin, [IPAddress.Loopback, IPAddress.Parse("10.0.0.5")], [], false));
    }
}
