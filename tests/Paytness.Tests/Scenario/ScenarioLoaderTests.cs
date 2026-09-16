using Paytness.Scenario;
using Paytness.Tests.Support;

namespace Paytness.Tests.Scenario;

public sealed class ScenarioLoaderTests
{
    [Fact]
    public async Task ContractPathCannotEscapeScenarioRoot()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string root = Directory.CreateTempSubdirectory("paytness-").FullName;
        try
        {
            string scenario = Path.Combine(root, "scenario.yaml");
            await File.WriteAllTextAsync(scenario, "version: 1\nid: escape\ncontract: ../outside.yaml\nprovider:\n  responseModes: [deliver]\n", token);
            await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(scenario, token));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DuplicateYamlKeysAreRejected()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string root = Directory.CreateTempSubdirectory("paytness-").FullName;
        try
        {
            string scenario = Path.Combine(root, "scenario.yaml");
            await File.WriteAllTextAsync(scenario, "version: 1\nid: first\nid: second\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\n", token);
            await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(scenario, token));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WebhookDeliveryCannotReferenceUnknownEvent()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string root = Directory.CreateTempSubdirectory("paytness-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "contracts"));
            File.Copy(TestPaths.Scenario("contracts/basic.yaml"), Path.Combine(root, "contracts", "basic.yaml"));
            string scenario = Path.Combine(root, "scenario.yaml");
            await File.WriteAllTextAsync(scenario, """
version: 1
id: bad-webhook-ref
contract: contracts/basic.yaml
provider:
  responseModes: [deliver]
webhooks:
  path: /test/webhook
  events:
    - id: evt-1
      type: payment.succeeded
  deliveries:
    - eventId: evt-missing
""", token);

            ScenarioValidationException exception = await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(scenario, token));
            Assert.Contains("unknown event", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WebhookEventIdsMustBeUnique()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string root = Directory.CreateTempSubdirectory("paytness-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "contracts"));
            File.Copy(TestPaths.Scenario("contracts/basic.yaml"), Path.Combine(root, "contracts", "basic.yaml"));
            string scenario = Path.Combine(root, "scenario.yaml");
            await File.WriteAllTextAsync(scenario, """
version: 1
id: duplicate-webhook-event
contract: contracts/basic.yaml
provider:
  responseModes: [deliver]
webhooks:
  path: /test/webhook
  events:
    - id: evt-1
      type: payment.succeeded
    - id: evt-1
      type: payment.succeeded
  deliveries:
    - eventId: evt-1
""", token);

            ScenarioValidationException exception = await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(scenario, token));
            Assert.Contains("unique", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
