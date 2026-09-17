using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using Paytness.Cli;
using Paytness.Tests.Integration;
using Paytness.Tests.Support;

namespace Paytness.Tests.Cli;

public sealed class CliAppTests
{
    [Fact]
    public async Task ValidateCommandReturnsZeroForValidScenario()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        RootCommand root = CliApp.Build();
        ParseResult parse = root.Parse(["validate", TestPaths.Scenario("healthy.yaml")]);
        Assert.Empty(parse.Errors);
        int exitCode = await parse.InvokeAsync(new InvocationConfiguration(), token);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunCommandProducesPassAndJsonReport()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        int providerPort = TestPaths.GetFreePort();
        await using SyntheticSut sut = await SyntheticSut.StartAsync(new Uri($"http://127.0.0.1:{providerPort}"), false, token);
        string reportPath = Path.Combine(Path.GetTempPath(), $"paytness-{Guid.NewGuid():N}.json");
        try
        {
            RootCommand root = CliApp.Build();
            ParseResult parse = root.Parse([
                "run",
                TestPaths.Scenario("healthy.yaml"),
                "--sut", sut.Origin.ToString(),
                "--provider-listen", $"127.0.0.1:{providerPort}",
                "--json", reportPath,
            ]);
            Assert.Empty(parse.Errors);
            int exitCode = await parse.InvokeAsync(new InvocationConfiguration(), token);
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(reportPath));
            using JsonDocument report = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath, token));
            Assert.Equal(1, report.RootElement.GetProperty("schemaVersion").GetInt32());
            string[] propertyNames = report.RootElement.EnumerateObject().Select(static property => property.Name).Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(["evidence", "invariants", "provider", "result", "scenarioHash", "scenarioId", "schemaVersion", "seed", "webhooks"], propertyNames);
            Assert.Equal("pass", report.RootElement.GetProperty("result").GetString());
            Assert.Equal(1, report.RootElement.GetProperty("provider").GetProperty("economicEffectCount").GetInt32());
            JsonElement firstInvariant = report.RootElement.GetProperty("invariants")[0];
            Assert.True(firstInvariant.TryGetProperty("evidenceRefs", out JsonElement evidenceRefs));
            Assert.Equal(JsonValueKind.Array, evidenceRefs.ValueKind);
        }
        finally
        {
            if (File.Exists(reportPath)) File.Delete(reportPath);
        }
    }
    [Fact]
    public async Task RootInvocationReturns130WhenCancellationIsAlreadyRequested()
    {
        RootCommand root = CliApp.Build();
        ParseResult parse = root.Parse(["validate", TestPaths.Scenario("healthy.yaml")]);
        Assert.Empty(parse.Errors);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        int exitCode = await CliProcess.InvokeAsync(parse, cancellation.Token);

        Assert.Equal(130, exitCode);
    }

    [Fact]
    public async Task ValidateCommandReturnsTwoForSemanticAssertionError()
    {
        string rootPath = Directory.CreateTempSubdirectory("paytness-cli-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(rootPath, "contract.yaml"), "version: 1\n", CancellationToken.None);
            string scenarioPath = Path.Combine(rootPath, "scenario.yaml");
            await File.WriteAllTextAsync(scenarioPath, "version: 1\nid: invalid-assertion\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\nassertions:\n  - id: bad\n    kind: arbitrary\n    comparator: eq\n    expected: 1\n", CancellationToken.None);
            RootCommand root = CliApp.Build();
            ParseResult parse = root.Parse(["validate", scenarioPath]);
            Assert.Empty(parse.Errors);

            int exitCode = await CliProcess.InvokeAsync(parse, CancellationToken.None);

            Assert.Equal(2, exitCode);
        }
        finally { Directory.Delete(rootPath, recursive: true); }
    }

}
