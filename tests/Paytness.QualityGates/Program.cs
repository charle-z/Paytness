using Paytness.Execution;
using Paytness.Reporting;
using Paytness.Scenario;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

Console.WriteLine("B1: parser boundaries");
string root = Directory.CreateTempSubdirectory("paytness-quality-").FullName;
try
{
    string contract = Path.Combine(root, "contract.json");
    await File.WriteAllTextAsync(contract, "{\"version\":1}");
    const string prefix = "{\"version\":1,\"id\":\"size-limit\",\"contract\":\"contract.json\",\"provider\":{\"responseModes\":[\"deliver\"]}}";
    int padding = checked((int)ScenarioLoader.MaxScenarioBytes - prefix.Length);
    string scenario = Path.Combine(root, "scenario.json");
    await File.WriteAllTextAsync(scenario, prefix + new string(' ', padding));
    Require(new FileInfo(scenario).Length == ScenarioLoader.MaxScenarioBytes, "exact 1 MiB fixture size mismatch");
    LoadedScenario loaded = await ScenarioLoader.LoadAsync(scenario);
    Require(loaded.Scenario.Id == "size-limit", "exact 1 MiB scenario was not accepted");
    await File.AppendAllTextAsync(scenario, " ");
    try { await ScenarioLoader.LoadAsync(scenario); throw new InvalidOperationException("1 MiB + 1 was accepted"); }
    catch (ScenarioValidationException) { }

    string exactNodes = "[" + string.Join(',', Enumerable.Repeat("0", DocumentStructuralValidator.MaxNodes - 1)) + "]";
    DocumentStructuralValidator.ValidateJson(exactNodes);
    string overNodes = "[" + string.Join(',', Enumerable.Repeat("0", DocumentStructuralValidator.MaxNodes)) + "]";
    try { DocumentStructuralValidator.ValidateJson(overNodes); throw new InvalidOperationException("10001 JSON nodes were accepted"); }
    catch (ScenarioValidationException) { }

    string exactDepth = new string('[', DocumentStructuralValidator.MaxDepth) + "0" + new string(']', DocumentStructuralValidator.MaxDepth);
    DocumentStructuralValidator.ValidateYaml(exactDepth);
    string overDepth = new string('[', DocumentStructuralValidator.MaxDepth + 1) + "0" + new string(']', DocumentStructuralValidator.MaxDepth + 1);
    try { DocumentStructuralValidator.ValidateYaml(overDepth); throw new InvalidOperationException("YAML depth 33 was accepted"); }
    catch (ScenarioValidationException) { }
}
finally
{
    Directory.Delete(root, recursive: true);
}
Console.WriteLine("B1 PASS");

Console.WriteLine("B2: 2000 scheduled actions");
ScheduledAction[] actions = Enumerable.Range(1, ScheduledActionScheduler.MaximumActions)
    .Reverse()
    .Select(index => new ScheduledAction($"a-{index:D4}", index, TimeSpan.Zero, _ => ValueTask.CompletedTask))
    .ToArray();
IReadOnlyList<string> completed = await ScheduledActionScheduler.ExecuteAsync(actions, TimeProvider.System);
for (int i = 0; i < ScheduledActionScheduler.MaximumActions; i++)
    Require(completed[i] == $"a-{i + 1:D4}", $"unstable scheduler order at {i}");
Console.WriteLine("B2 PASS");

Console.WriteLine("B5: EvidenceStore 64 MiB cap");
{
    var store = new EvidenceStore();
    string payload = new('x', EvidenceStore.MaxRetainedStringBytes * 2);
    int attempted = 0;
    while (!store.Truncation.Truncated && attempted < 10_000)
    {
        attempted++;
        store.Add(new EvidenceItem(
            attempted,
            "b5",
            $"item-{attempted:D5}",
            payload,
            new Dictionary<string, object?>()));
    }

    EvidenceTruncation truncation = store.Truncation;
    Require(truncation.Truncated, "EvidenceStore did not truncate under the default 64 MiB budget");
    Require(truncation.RetainedBytes <= EvidenceStore.MaxBytes, "EvidenceStore exceeded its 64 MiB cap");
    Require(
        truncation.RetainedBytes >= 60L * 1024 * 1024,
        "EvidenceStore truncated before meaningful pressure near the 64 MiB cap");
    Require(truncation.DroppedItems >= 1, "EvidenceStore truncation did not record a dropped item");
    Console.WriteLine($"B5 PASS retained={truncation.RetainedBytes / 1024.0 / 1024.0:F2} MiB items={truncation.RetainedItems} dropped={truncation.DroppedItems}");
}

Console.WriteLine("B6: active cancellation + shutdown");
await using (ProbeSut sut = await ProbeSut.StartAsync())
{
    const string webhookSecret = "probe-secret";
    LoadedScenario webhookLoaded = await ScenarioLoader.LoadAsync("scenarios/duplicate-webhook.yaml");
    webhookLoaded = webhookLoaded with
    {
        Scenario = webhookLoaded.Scenario with
        {
            Webhooks = webhookLoaded.Scenario.Webhooks with
            {
                Path = "/test/webhook-blocking",
                Deliveries = [webhookLoaded.Scenario.Webhooks.Deliveries[0]],
            },
            Assertions = Array.Empty<AssertionSpec>(),
        },
    };
    int webhookPort = FreePort();
    sut.ProviderOrigin = new Uri($"http://127.0.0.1:{webhookPort}");
    using var webhookCancel = new CancellationTokenSource();
    Task<Paytness.Reporting.RunReport> webhookRun = ScenarioRunner.RunAsync(
        webhookLoaded,
        new RunOptions(sut.Origin, new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, webhookPort), [], false, webhookSecret),
        webhookCancel.Token);
    await sut.BlockingWebhookStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
    var webhookWatch = System.Diagnostics.Stopwatch.StartNew();
    webhookCancel.Cancel();
    try { await webhookRun; throw new InvalidOperationException("cancelled webhook run returned normally"); }
    catch (OperationCanceledException) { }
    webhookWatch.Stop();
    Require(webhookWatch.Elapsed <= TimeSpan.FromSeconds(5), $"webhook cancellation took {webhookWatch.Elapsed}");
    Console.WriteLine($"B6 webhook PASS {webhookWatch.Elapsed.TotalMilliseconds:F1} ms");

    sut.Reset();
    LoadedScenario observationLoaded = await ScenarioLoader.LoadAsync("scenarios/healthy.yaml");
    observationLoaded = observationLoaded with
    {
        Scenario = observationLoaded.Scenario with
        {
            Observations =
            [
                new ObservationSpec { Id = "not-ready", Path = "/test/not-ready", TimeoutMs = 300_000, IntervalMs = 1_000 },
            ],
            Assertions = Array.Empty<AssertionSpec>(),
        },
    };
    int observationPort = FreePort();
    sut.ProviderOrigin = new Uri($"http://127.0.0.1:{observationPort}");
    using var observationCancel = new CancellationTokenSource();
    Task<Paytness.Reporting.RunReport> observationRun = ScenarioRunner.RunAsync(
        observationLoaded,
        new RunOptions(sut.Origin, new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, observationPort), [], false),
        observationCancel.Token);
    await sut.NotReadyObservationStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
    var observationWatch = System.Diagnostics.Stopwatch.StartNew();
    observationCancel.Cancel();
    try { await observationRun; throw new InvalidOperationException("cancelled observation run returned normally"); }
    catch (OperationCanceledException) { }
    observationWatch.Stop();
    Require(observationWatch.Elapsed <= TimeSpan.FromSeconds(5), $"observation cancellation took {observationWatch.Elapsed}");
    Console.WriteLine($"B6 observation PASS {observationWatch.Elapsed.TotalMilliseconds:F1} ms");
}

static int FreePort()
{
    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start();
    int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}
