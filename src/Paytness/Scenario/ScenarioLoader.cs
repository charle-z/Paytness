using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Paytness.Scenario;

public sealed class ScenarioValidationException(string message) : Exception(message);

public static class ScenarioLoader
{
    public const long MaxScenarioBytes = 1024 * 1024;
    public const long MaxContractBytes = 512 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static async Task<LoadedScenario> LoadAsync(string scenarioPath, CancellationToken cancellationToken = default)
    {
        string fullScenarioPath = Path.GetFullPath(scenarioPath);
        ScenarioSpec scenario = await LoadDocumentAsync<ScenarioSpec>(fullScenarioPath, MaxScenarioBytes, cancellationToken);
        ValidateScenario(scenario);

        string scenarioRoot = Path.GetDirectoryName(fullScenarioPath) ?? Directory.GetCurrentDirectory();
        string contractPath = ResolveContainedPath(scenarioRoot, scenario.Contract);
        ProviderContract contract = await LoadDocumentAsync<ProviderContract>(contractPath, MaxContractBytes, cancellationToken);
        ValidateContract(contract);

        byte[] scenarioBytes = await File.ReadAllBytesAsync(fullScenarioPath, cancellationToken);
        string scenarioHash = Convert.ToHexStringLower(SHA256.HashData(scenarioBytes));
        return new LoadedScenario(scenario, contract, fullScenarioPath, scenarioHash);
    }

    internal static string ResolveContainedPath(string root, string relativePath) =>
        ContainedPathResolver.Resolve(root, relativePath);

    private static async Task<T> LoadDocumentAsync<T>(string path, long maxBytes, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new ScenarioValidationException($"File not found: {path}");
        if (info.Length > maxBytes) throw new ScenarioValidationException($"File exceeds the {maxBytes} byte limit: {path}");

        string text = await File.ReadAllTextAsync(path, cancellationToken);
        string extension = Path.GetExtension(path);

        try
        {
            if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                DocumentStructuralValidator.ValidateJson(text);
                return JsonSerializer.Deserialize<T>(text, JsonOptions) ?? throw new ScenarioValidationException($"Document is empty: {path}");
            }

            if (extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
            {
                DocumentStructuralValidator.ValidateYaml(text);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .WithDuplicateKeyChecking()
                    .WithAttemptingUnquotedStringTypeDeserialization()
                    .Build();
                object? yamlObject = deserializer.Deserialize<object?>(text);
                string json = JsonSerializer.Serialize(yamlObject, JsonOptions);
                return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new ScenarioValidationException($"Document is empty: {path}");
            }
        }
        catch (ScenarioValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or YamlDotNet.Core.YamlException)
        {
            throw new ScenarioValidationException($"Invalid document '{path}': {exception.Message}");
        }

        throw new ScenarioValidationException($"Unsupported document extension '{extension}'. Use .json, .yaml, or .yml.");
    }

    private static void ValidateScenario(ScenarioSpec scenario)
    {
        if (scenario.Version != 1) throw new ScenarioValidationException("ScenarioSpec version must be 1.");
        if (string.IsNullOrWhiteSpace(scenario.Id)) throw new ScenarioValidationException("Scenario id is required.");
        if (scenario.TimeoutSeconds is < 1 or > 300) throw new ScenarioValidationException("Scenario timeoutSeconds must be between 1 and 300.");
        if (scenario.Actions.Count > 1_000) throw new ScenarioValidationException("Scenario exceeds 1,000 actions.");
        if (scenario.Observations.Count > 500) throw new ScenarioValidationException("Scenario exceeds 500 observations.");
        if (scenario.Assertions.Count > 500) throw new ScenarioValidationException("Scenario exceeds 500 assertions.");
        if (scenario.Provider.ResponseModes.Count is 0 or > 1_000) throw new ScenarioValidationException("Provider responseModes must contain between 1 and 1,000 entries.");
        if (scenario.Provider.ResponseModes.Any(static mode => mode is not ("deliver" or "abort_after_commit")))
            throw new ScenarioValidationException("Provider responseModes supports only deliver and abort_after_commit in v0.1.");
        int actionInvocations = 0;
        foreach (SutActionSpec action in scenario.Actions)
        {
            if (!IsHttpPath(action.Path)) throw new ScenarioValidationException("SUT action paths must begin with '/'.");
            if (action.Concurrency is < 1 or > 128) throw new ScenarioValidationException("SUT action concurrency must be between 1 and 128.");
            actionInvocations += action.Concurrency;
        }
        if (actionInvocations > 1_000) throw new ScenarioValidationException("Scenario exceeds 1,000 SUT action invocations.");
        foreach (ObservationSpec observation in scenario.Observations)
            if (!IsHttpPath(observation.Path)) throw new ScenarioValidationException("Observation paths must begin with '/'.");

        ValidateWebhooks(scenario.Webhooks);
    }

    private static void ValidateWebhooks(WebhookSpec webhooks)
    {
        if (webhooks.Events.Count > 1_000) throw new ScenarioValidationException("Scenario exceeds 1,000 payment events.");
        if (webhooks.Deliveries.Count > 1_000) throw new ScenarioValidationException("Scenario exceeds 1,000 webhook deliveries.");
        if (webhooks.Events.Count == 0 && webhooks.Deliveries.Count == 0) return;
        if (string.IsNullOrWhiteSpace(webhooks.Path) || !IsHttpPath(webhooks.Path))
            throw new ScenarioValidationException("Webhook path must begin with '/' when webhook events or deliveries are declared.");
        if (webhooks.Events.Count == 0 || webhooks.Deliveries.Count == 0)
            throw new ScenarioValidationException("Webhook scenarios require at least one event and one explicit delivery.");

        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaymentEventSpec paymentEvent in webhooks.Events)
        {
            if (string.IsNullOrWhiteSpace(paymentEvent.Id) || !eventIds.Add(paymentEvent.Id))
                throw new ScenarioValidationException("Webhook event ids must be non-empty and unique.");
            if (paymentEvent.Type is not ("payment.processing" or "payment.succeeded" or "payment.failed"))
                throw new ScenarioValidationException($"Unsupported payment event type '{paymentEvent.Type}'.");
            if (paymentEvent.AttemptOrdinal is < 1 or > 1_000)
                throw new ScenarioValidationException("Webhook event attemptOrdinal must be between 1 and 1,000.");
        }

        foreach (WebhookDeliverySpec delivery in webhooks.Deliveries)
        {
            if (!eventIds.Contains(delivery.EventId))
                throw new ScenarioValidationException($"Webhook delivery references unknown event '{delivery.EventId}'.");
            if (delivery.DelayMs is < 0 or > 300_000)
                throw new ScenarioValidationException("Webhook delivery delayMs must be between 0 and 300000.");
        }
    }

    private static void ValidateContract(ProviderContract contract)
    {
        if (contract.Version != 1) throw new ScenarioValidationException("ProviderContract version must be 1.");
        if (!IsHttpPath(contract.CreatePayment.Path)) throw new ScenarioValidationException("Provider createPayment.path must begin with '/'.");
        if (contract.CreatePayment.Method is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE"))
            throw new ScenarioValidationException("Provider createPayment.method is not supported.");
        if (string.IsNullOrWhiteSpace(contract.CreatePayment.Extract.LogicalPayment))
            throw new ScenarioValidationException("Provider logicalPayment extraction pointer is required.");
        if (string.IsNullOrWhiteSpace(contract.Webhook.SignatureHeader))
            throw new ScenarioValidationException("Provider webhook signatureHeader is required.");
        string[] webhookProperties = [contract.Webhook.EventIdProperty, contract.Webhook.EventTypeProperty, contract.Webhook.EventOccurredAtProperty, contract.Webhook.AttemptIdProperty, contract.Webhook.LogicalPaymentProperty, contract.Webhook.StateProperty];
        if (webhookProperties.Any(string.IsNullOrWhiteSpace) || webhookProperties.Distinct(StringComparer.Ordinal).Count() != webhookProperties.Length)
            throw new ScenarioValidationException("Provider webhook property names must be non-empty and unique.");
    }

    private static bool IsHttpPath(string value) => value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal);
}
