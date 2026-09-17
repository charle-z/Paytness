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
    [Fact]
    public async Task ScenarioVersionMustBeExplicitAndEqualToOne()
    {
        string root = Directory.CreateTempSubdirectory("paytness-contract-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "contract.yaml"), "version: 1\n", CancellationToken.None);
            string scenario = Path.Combine(root, "scenario.yaml");
            await File.WriteAllTextAsync(scenario, "id: missing-version\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\n", CancellationToken.None);
            await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(scenario, CancellationToken.None));
            await File.WriteAllTextAsync(scenario, "version: 2\nid: unknown-version\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\n", CancellationToken.None);
            await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(scenario, CancellationToken.None));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ProviderContractVersionMustBeExplicitAndEqualToOne()
    {
        string root = Directory.CreateTempSubdirectory("paytness-contract-").FullName;
        try
        {
            string scenario = Path.Combine(root, "scenario.yaml");
            await File.WriteAllTextAsync(scenario, "version: 1\nid: provider-version\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\n", CancellationToken.None);
            string contract = Path.Combine(root, "contract.yaml");
            await File.WriteAllTextAsync(contract, "{}\n", CancellationToken.None);
            await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(scenario, CancellationToken.None));
            await File.WriteAllTextAsync(contract, "version: 2\n", CancellationToken.None);
            await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(scenario, CancellationToken.None));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task UnknownScenarioMembersAreRejectedForYamlAndJson()
    {
        string root = Directory.CreateTempSubdirectory("paytness-contract-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "contract.yaml"), "version: 1\n", CancellationToken.None);
            string yaml = Path.Combine(root, "scenario.yaml");
            await File.WriteAllTextAsync(yaml, "version: 1\nid: yaml-extra\ncontract: contract.yaml\nunexpected: true\nprovider:\n  responseModes: [deliver]\n", CancellationToken.None);
            await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(yaml, CancellationToken.None));

            await File.WriteAllTextAsync(Path.Combine(root, "contract.json"), "{\"version\":1}", CancellationToken.None);
            string json = Path.Combine(root, "scenario.json");
            await File.WriteAllTextAsync(json, "{\"version\":1,\"id\":\"json-extra\",\"contract\":\"contract.json\",\"unexpected\":true,\"provider\":{\"responseModes\":[\"deliver\"]}}", CancellationToken.None);
            await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(json, CancellationToken.None));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task InvalidAssertionsAreRejectedDuringValidation()
    {
        string root = Directory.CreateTempSubdirectory("paytness-contract-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "contract.yaml"), "version: 1\n", CancellationToken.None);
            string[] invalidScenarios =
            [
                "version: 1\nid: bad-kind\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\nassertions:\n  - id: bad\n    kind: arbitrary\n    comparator: eq\n    expected: 1\n",
                "version: 1\nid: bad-type\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\nassertions:\n  - id: bad\n    kind: provider_effect_count\n    comparator: eq\n    expected: \"1\"\n",
                "version: 1\nid: bad-ref\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\nassertions:\n  - id: bad\n    kind: observation\n    observation: missing\n    pointer: /value\n    comparator: eq\n    expected: ok\n",
                "version: 1\nid: bad-pointer\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\nobservations:\n  - id: state\n    path: /state\nassertions:\n  - id: bad\n    kind: observation\n    observation: state\n    pointer: /bad~2token\n    comparator: eq\n    expected: ok\n",
            ];
            string scenario = Path.Combine(root, "scenario.yaml");
            foreach (string content in invalidScenarios)
            {
                await File.WriteAllTextAsync(scenario, content, CancellationToken.None);
                await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(scenario, CancellationToken.None));
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task YamlAndJsonProduceEquivalentSemanticValues()
    {
        string root = Directory.CreateTempSubdirectory("paytness-contract-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "contract-yaml.yaml"), "version: 1\n", CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(root, "contract-json.json"), "{\"version\":1}", CancellationToken.None);
            string yaml = Path.Combine(root, "scenario.yaml");
            await File.WriteAllTextAsync(yaml, "version: 1\nid: equivalent\nseed: 42\ntimeoutSeconds: 15\ncontract: contract-yaml.yaml\nprovider:\n  responseModes: [deliver]\nobservations:\n  - id: state\n    path: /state\n    timeoutMs: 1000\n    intervalMs: 25\nassertions:\n  - id: paid\n    kind: observation\n    observation: state\n    pointer: /paymentStatus\n    comparator: eq\n    expected: Paid\n", CancellationToken.None);
            string json = Path.Combine(root, "scenario.json");
            await File.WriteAllTextAsync(json, "{\"version\":1,\"id\":\"equivalent\",\"seed\":42,\"timeoutSeconds\":15,\"contract\":\"contract-json.json\",\"provider\":{\"responseModes\":[\"deliver\"]},\"observations\":[{\"id\":\"state\",\"path\":\"/state\",\"timeoutMs\":1000,\"intervalMs\":25}],\"assertions\":[{\"id\":\"paid\",\"kind\":\"observation\",\"observation\":\"state\",\"pointer\":\"/paymentStatus\",\"comparator\":\"eq\",\"expected\":\"Paid\"}]}", CancellationToken.None);

            LoadedScenario fromYaml = await ScenarioLoader.LoadAsync(yaml, CancellationToken.None);
            LoadedScenario fromJson = await ScenarioLoader.LoadAsync(json, CancellationToken.None);
            Assert.Equal(fromYaml.Scenario.Id, fromJson.Scenario.Id);
            Assert.Equal(fromYaml.Scenario.Seed, fromJson.Scenario.Seed);
            Assert.Equal(fromYaml.Scenario.TimeoutSeconds, fromJson.Scenario.TimeoutSeconds);
            Assert.Equal(fromYaml.Scenario.Provider.ResponseModes, fromJson.Scenario.Provider.ResponseModes);
            Assert.Equal(fromYaml.Scenario.Observations[0], fromJson.Scenario.Observations[0]);
            Assert.Equal(fromYaml.Scenario.Assertions[0].Rfc6901, fromJson.Scenario.Assertions[0].Rfc6901);
            Assert.Equal(fromYaml.Scenario.Assertions[0].Expected?.GetString(), fromJson.Scenario.Assertions[0].Expected?.GetString());
            Assert.Equal(fromYaml.Contract.CreatePayment.Path, fromJson.Contract.CreatePayment.Path);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task InvalidScenarioSemanticsAreRejectedBeforeRun()
    {
        string root = Directory.CreateTempSubdirectory("paytness-contract-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "contract.yaml"), "version: 1\n", CancellationToken.None);
            string[] invalidScenarios =
            [
                "version: 1\nid: missing-contract\nprovider:\n  responseModes: [deliver]\n",
                "version: 1\nid: bad-method\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\nactions:\n  - method: TRACE\n    path: /test\n",
                "version: 1\nid: duplicate-observation\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\nobservations:\n  - id: state\n    path: /one\n  - id: state\n    path: /two\n",
                "version: 1\nid: bad-observation-bounds\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\nobservations:\n  - id: state\n    path: /state\n    intervalMs: 0\n",
            ];
            string scenario = Path.Combine(root, "scenario.yaml");
            foreach (string content in invalidScenarios)
            {
                await File.WriteAllTextAsync(scenario, content, CancellationToken.None);
                await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(scenario, CancellationToken.None));
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task InvalidProviderContractShapeIsRejected()
    {
        string root = Directory.CreateTempSubdirectory("paytness-contract-").FullName;
        try
        {
            string scenario = Path.Combine(root, "scenario.yaml");
            await File.WriteAllTextAsync(scenario, "version: 1\nid: bad-contract\ncontract: contract.yaml\nprovider:\n  responseModes: [deliver]\n", CancellationToken.None);
            string contract = Path.Combine(root, "contract.yaml");

            await File.WriteAllTextAsync(contract, "version: 1\nunexpected: true\n", CancellationToken.None);
            await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(scenario, CancellationToken.None));

            await File.WriteAllTextAsync(contract, "version: 1\ncreatePayment:\n  method: POST\n  path: /provider/v1/payments\n  idempotencyHeader: Idempotency-Key\n  extract:\n    logicalPayment: merchantReference\n", CancellationToken.None);
            await Assert.ThrowsAsync<ScenarioValidationException>(() => ScenarioLoader.LoadAsync(scenario, CancellationToken.None));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

}
