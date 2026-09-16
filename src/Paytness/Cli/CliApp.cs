using System.CommandLine;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Paytness.Execution;
using Paytness.Reporting;
using Paytness.Scenario;
using Paytness.Security;
using Paytness.Webhooks;

namespace Paytness.Cli;

public static class CliApp
{
    private static readonly JsonSerializerOptions ReportJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static RootCommand Build()
    {
        var root = new RootCommand("Adversarial payment-integration test runner.");
        root.Subcommands.Add(BuildValidateCommand());
        root.Subcommands.Add(BuildRunCommand());
        return root;
    }

    private static Command BuildValidateCommand()
    {
        var scenarioArgument = new Argument<string>("scenario")
        {
            Description = "Path to a YAML or JSON ScenarioSpec v1.",
        };
        var command = new Command("validate", "Validate ScenarioSpec and ProviderContract without opening network connections.");
        command.Arguments.Add(scenarioArgument);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string scenarioPath = parseResult.GetValue(scenarioArgument) ?? string.Empty;
            try
            {
                LoadedScenario loaded = await ScenarioLoader.LoadAsync(scenarioPath, cancellationToken);
                Console.WriteLine($"VALID {loaded.Scenario.Id} sha256:{loaded.ScenarioHash}");
                return 0;
            }
            catch (ScenarioValidationException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 2;
            }
        });
        return command;
    }

    private static Command BuildRunCommand()
    {
        var scenarioArgument = new Argument<string>("scenario") { Description = "Path to a YAML or JSON ScenarioSpec v1." };
        var sutOption = new Option<string?>("--sut") { Description = "SUT origin, for example http://127.0.0.1:5000." };
        var providerListenOption = new Option<string>("--provider-listen")
        {
            Description = "Provider listen address.",
            DefaultValueFactory = _ => "127.0.0.1:8787",
        };
        var seedOption = new Option<int?>("--seed") { Description = "Override the scenario seed." };
        var jsonOption = new Option<string?>("--json") { Description = "Write the JSON v1 report to this path." };
        var junitOption = new Option<string?>("--junit") { Description = "Write a JUnit report to this path." };
        var webhookSecretEnvOption = new Option<string?>("--webhook-secret-env") { Description = "Environment variable that contains the generic HMAC webhook secret." };
        var allowTargetOption = new Option<string[]>("--allow-target") { Description = "Explicit allowed SUT host:port. Repeat as needed." };
        var allowPublicOption = new Option<bool>("--allow-public-targets") { Description = "Permit explicitly allowed public targets." };
        var allowNonLoopbackListenOption = new Option<bool>("--allow-non-loopback-listen") { Description = "Permit provider listen outside loopback." };

        var command = new Command("run", "Run one adversarial scenario.");
        command.Arguments.Add(scenarioArgument);
        command.Options.Add(sutOption);
        command.Options.Add(providerListenOption);
        command.Options.Add(seedOption);
        command.Options.Add(jsonOption);
        command.Options.Add(junitOption);
        command.Options.Add(webhookSecretEnvOption);
        command.Options.Add(allowTargetOption);
        command.Options.Add(allowPublicOption);
        command.Options.Add(allowNonLoopbackListenOption);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string scenarioPath = parseResult.GetValue(scenarioArgument) ?? string.Empty;
            string? sutText = parseResult.GetValue(sutOption);
            string providerListen = parseResult.GetValue(providerListenOption) ?? "127.0.0.1:8787";
            int? seed = parseResult.GetValue(seedOption);
            string? jsonPath = parseResult.GetValue(jsonOption);
            string? junitPath = parseResult.GetValue(junitOption);
            string? webhookSecretEnv = parseResult.GetValue(webhookSecretEnvOption);
            string[] allowedTargets = parseResult.GetValue(allowTargetOption) ?? [];
            bool allowPublicTargets = parseResult.GetValue(allowPublicOption);
            bool allowNonLoopbackListen = parseResult.GetValue(allowNonLoopbackListenOption);

            try
            {
                if (string.IsNullOrWhiteSpace(sutText) || !Uri.TryCreate(sutText, UriKind.Absolute, out Uri? sutOrigin))
                {
                    Console.Error.WriteLine("--sut must be a valid absolute HTTP(S) origin.");
                    return 2;
                }

                IPEndPoint providerEndpoint = ParseProviderEndpoint(providerListen, allowNonLoopbackListen);
                LoadedScenario loaded = await ScenarioLoader.LoadAsync(scenarioPath, cancellationToken);
                if (seed is int seedValue)
                {
                    loaded = loaded with { Scenario = loaded.Scenario with { Seed = seedValue } };
                }

                string? webhookSecret = ResolveWebhookSecret(loaded.Scenario, webhookSecretEnv);

                RunReport report = await ScenarioRunner.RunAsync(
                    loaded,
                    new RunOptions(sutOrigin, providerEndpoint, allowedTargets, allowPublicTargets, webhookSecret),
                    cancellationToken);

                WriteConsole(report);
                if (!string.IsNullOrWhiteSpace(jsonPath))
                {
                    string json = JsonSerializer.Serialize(report, ReportJson);
                    await File.WriteAllTextAsync(jsonPath, json, cancellationToken);
                }
                if (!string.IsNullOrWhiteSpace(junitPath))
                    await JUnitReportWriter.WriteAsync(junitPath, report, cancellationToken);

                return report.Result switch
                {
                    InvariantStatus.Pass => 0,
                    InvariantStatus.Fail => 1,
                    _ => 3,
                };
            }
            catch (ScenarioValidationException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 2;
            }
            catch (FormatException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 2;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Run cancelled.");
                return 130;
            }
            catch (Exception exception) when (exception is TargetPolicyException or WebhookDispatchException or HttpRequestException or TimeoutException or IOException)
            {
                Console.Error.WriteLine(exception.Message);
                return 3;
            }
        });

        return command;
    }
    private static string? ResolveWebhookSecret(ScenarioSpec scenario, string? environmentVariable)
    {
        if (scenario.Webhooks.Deliveries.Count == 0) return null;
        if (string.IsNullOrWhiteSpace(environmentVariable))
            throw new FormatException("Webhook deliveries require --webhook-secret-env ENVVAR.");
        string? secret = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrEmpty(secret))
            throw new FormatException("Configured webhook secret environment variable is not set or is empty.");
        return secret;
    }

    private static IPEndPoint ParseProviderEndpoint(string value, bool allowNonLoopback)
    {
        int separator = value.LastIndexOf(':');
        if (separator <= 0 || !int.TryParse(value[(separator + 1)..], out int port) || port is < 1 or > 65535)
            throw new FormatException("--provider-listen must be an IP:port with port between 1 and 65535.");

        string host = value[..separator];
        if (!IPAddress.TryParse(host, out IPAddress? address))
            throw new FormatException("--provider-listen currently requires an IP address, not a hostname.");
        if (!IPAddress.IsLoopback(address) && !allowNonLoopback)
            throw new FormatException("Non-loopback provider listen requires --allow-non-loopback-listen.");

        return new IPEndPoint(address, port);
    }

    private static void WriteConsole(RunReport report)
    {
        Console.WriteLine(report.Result.ToString().ToUpperInvariant());
        Console.WriteLine($"Scenario: {report.ScenarioId}");
        Console.WriteLine($"Seed: {report.Seed}");
        Console.WriteLine($"Scenario SHA-256: {report.ScenarioHash}");
        Console.WriteLine($"Provider requests/attempts/effects: {report.Provider.RequestCount}/{report.Provider.AttemptCount}/{report.Provider.EconomicEffectCount}");
        Console.WriteLine($"Webhook deliveries/acked/rejected: {report.Webhooks.DeliveryCount}/{report.Webhooks.AckedCount}/{report.Webhooks.RejectedCount}");
        foreach (InvariantResult invariant in report.Invariants)
        {
            Console.WriteLine($"[{invariant.Status}] {invariant.Id}: expected={invariant.Expected ?? "<none>"} actual={invariant.Actual ?? "<none>"}");
        }
    }
}
