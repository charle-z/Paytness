using System.CommandLine;
using System.CommandLine.Invocation;
using System.Xml.Linq;
using Paytness.Cli;
using Paytness.Tests.Integration;
using Paytness.Tests.Support;

namespace Paytness.Tests.Cli;

public sealed class CliWebhookTests
{
    [Fact]
    public async Task RunCommandReadsWebhookSecretFromEnvironmentAndWritesJUnit()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        int providerPort = TestPaths.GetFreePort();
        string environmentName = $"PAYTNESS_TEST_SECRET_{Guid.NewGuid():N}";
        const string secret = "fixture-cli-hmac";
        string junitPath = Path.Combine(Path.GetTempPath(), $"paytness-{Guid.NewGuid():N}.xml");
        Environment.SetEnvironmentVariable(environmentName, secret);
        await using SyntheticSut sut = await SyntheticSut.StartAsync(
            new Uri($"http://127.0.0.1:{providerPort}"),
            false,
            secret,
            token);

        try
        {
            RootCommand root = CliApp.Build();
            ParseResult parse = root.Parse([
                "run",
                TestPaths.Scenario("duplicate-webhook.yaml"),
                "--sut", sut.Origin.ToString(),
                "--provider-listen", $"127.0.0.1:{providerPort}",
                "--webhook-secret-env", environmentName,
                "--junit", junitPath,
            ]);
            Assert.Empty(parse.Errors);

            int exitCode = await parse.InvokeAsync(new InvocationConfiguration(), token);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(junitPath));
            XDocument document = XDocument.Load(junitPath);
            Assert.Equal("duplicate-webhook", document.Root?.Attribute("name")?.Value);
            Assert.Equal("0", document.Root?.Attribute("failures")?.Value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentName, null);
            if (File.Exists(junitPath)) File.Delete(junitPath);
        }
    }

    [Fact]
    public async Task RunCommandRequiresSecretEnvironmentMappingForWebhookScenario()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        int providerPort = TestPaths.GetFreePort();
        await using SyntheticSut sut = await SyntheticSut.StartAsync(
            new Uri($"http://127.0.0.1:{providerPort}"),
            false,
            token);

        RootCommand root = CliApp.Build();
        ParseResult parse = root.Parse([
            "run",
            TestPaths.Scenario("duplicate-webhook.yaml"),
            "--sut", sut.Origin.ToString(),
            "--provider-listen", $"127.0.0.1:{providerPort}",
        ]);
        Assert.Empty(parse.Errors);

        int exitCode = await parse.InvokeAsync(new InvocationConfiguration(), token);

        Assert.Equal(2, exitCode);
    }
}
