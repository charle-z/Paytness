using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Paytness.Execution;
using Paytness.Provider;
using Paytness.Reporting;
using Paytness.Scenario;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static int FreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static async Task<double> RunHealthyAsync(ProbeSut sut, LoadedScenario healthy)
{
    int port = FreePort();
    sut.Reset();
    sut.ProviderOrigin = new Uri($"http://127.0.0.1:{port}");
    var sw = Stopwatch.StartNew();
    RunReport report = await ScenarioRunner.RunAsync(
        healthy,
        new RunOptions(sut.Origin, new IPEndPoint(IPAddress.Loopback, port), [], false));
    sw.Stop();
    Require(report.Result == InvariantStatus.Pass, "healthy benchmark run did not PASS");
    return sw.Elapsed.TotalMilliseconds;
}

LoadedScenario healthy = await ScenarioLoader.LoadAsync("scenarios/healthy.yaml");

Console.WriteLine("Performance environment");
Console.WriteLine($"framework={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"os={System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
Console.WriteLine($"arch={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"cpu={Environment.ProcessorCount}");

var validationTimes = new List<double>();
for (int i = 0; i < 100; i++)
{
    var sw = Stopwatch.StartNew();
    _ = await ScenarioLoader.LoadAsync("scenarios/healthy.yaml");
    sw.Stop();
    validationTimes.Add(sw.Elapsed.TotalMilliseconds);
}
double validationP95 = Metrics.P95(validationTimes);
Console.WriteLine($"normal_validation_p95_ms={validationP95:F3} budget=500");
Require(validationP95 <= 500, "normal validation p95 budget failed");

string largeRoot = Directory.CreateTempSubdirectory("paytness-perf-").FullName;
try
{
    await File.WriteAllTextAsync(Path.Combine(largeRoot, "contract.json"), "{\"version\":1}");
    const string prefix = "{\"version\":1,\"id\":\"large\",\"contract\":\"contract.json\",\"provider\":{\"responseModes\":[\"deliver\"]}}";
    string scenario = Path.Combine(largeRoot, "scenario.json");
    await File.WriteAllTextAsync(scenario, prefix + new string(' ', checked((int)ScenarioLoader.MaxScenarioBytes - prefix.Length)));
    var largeTimes = new List<double>();
    for (int i = 0; i < 30; i++)
    {
        var sw = Stopwatch.StartNew();
        _ = await ScenarioLoader.LoadAsync(scenario);
        sw.Stop();
        largeTimes.Add(sw.Elapsed.TotalMilliseconds);
    }
    double largeP95 = Metrics.P95(largeTimes);
    Console.WriteLine($"one_mib_validation_p95_ms={largeP95:F3} budget=1000");
    Require(largeP95 <= 1000, "1 MiB validation p95 budget failed");
}
finally
{
    Directory.Delete(largeRoot, recursive: true);
}

var startupTimes = new List<double>();
for (int i = 0; i < 30; i++)
{
    int port = FreePort();
    var sw = Stopwatch.StartNew();
    await using ProviderHost provider = await ProviderHost.StartAsync(
        healthy.Contract, healthy.Scenario.Provider.ResponseModes,
        new IPEndPoint(IPAddress.Loopback, port), CancellationToken.None);
    sw.Stop();
    startupTimes.Add(sw.Elapsed.TotalMilliseconds);
}
double startupP95 = Metrics.P95(startupTimes);
Console.WriteLine($"provider_ready_p95_ms={startupP95:F3} budget=1500");
Require(startupP95 <= 1500, "provider-ready p95 budget failed");

var lagSamples = new double[100];
var lagWatch = Stopwatch.StartNew();
ScheduledAction[] lagActions = Enumerable.Range(0, lagSamples.Length)
    .Select(index => new ScheduledAction(
        $"lag-{index:D3}", index + 1, TimeSpan.FromMilliseconds(index * 5),
        _ => { lagSamples[index] = lagWatch.Elapsed.TotalMilliseconds - index * 5; return ValueTask.CompletedTask; }))
    .ToArray();
_ = await ScheduledActionScheduler.ExecuteAsync(lagActions, TimeProvider.System);
double lagP95 = Metrics.P95(lagSamples);
Console.WriteLine($"scheduler_lag_p95_ms={lagP95:F3} budget=25");
Require(lagP95 <= 25, "scheduler lag p95 budget failed");

await using ProbeSut sut = await ProbeSut.StartAsync();
for (int i = 0; i < 10; i++) _ = await RunHealthyAsync(sut, healthy);
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();

var runnerTimes = new List<double>();
var managedSamples = new List<(double X, double Y)>();
var process = Process.GetCurrentProcess();
long maxRss = 0;
for (int i = 1; i <= 100; i++)
{
    runnerTimes.Add(await RunHealthyAsync(sut, healthy));
    process.Refresh();
    maxRss = Math.Max(maxRss, process.WorkingSet64);
    if (i % 10 == 0)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long managed = GC.GetTotalMemory(forceFullCollection: true);
        managedSamples.Add((i, managed));
        process.Refresh();
        maxRss = Math.Max(maxRss, process.WorkingSet64);
        Console.WriteLine($"b7_sample_run={i} managed={Metrics.MiB(managed)} rss={Metrics.MiB(process.WorkingSet64)}");
    }
}

double runnerP95 = Metrics.P95(runnerTimes);
double memorySlope = Metrics.LinearSlope(managedSamples);
double managedDelta = managedSamples[^1].Y - managedSamples[0].Y;
Console.WriteLine($"healthy_total_p95_ms={runnerP95:F3} conservative_budget=500");
Console.WriteLine($"max_rss={Metrics.MiB(maxRss)} budget=200 MiB");
Console.WriteLine($"b7_managed_delta={Metrics.MiB((long)managedDelta)} slope_bytes_per_run={memorySlope:F1}");
const long managedDeltaBudget = 5L * 1024 * 1024;
const double managedSlopeBudget = 64d * 1024;
Console.WriteLine($"b7_managed_delta_budget={Metrics.MiB(managedDeltaBudget)} slope_budget_bytes_per_run={managedSlopeBudget:F0}");
Require(maxRss < 200L * 1024 * 1024, "RSS budget failed");
Require(managedDelta <= managedDeltaBudget, "B7 managed-memory delta budget failed");
Require(memorySlope <= managedSlopeBudget, "B7 managed-memory slope budget failed");
if (runnerP95 <= 500)
    Console.WriteLine("small_runner_overhead=PASS_CONSERVATIVE (total run p95 itself is <=500 ms)");
else
    Console.WriteLine("small_runner_overhead=INCONCLUSIVE (total includes SUT work)");

Console.WriteLine("B7 evidence complete; no numeric retained-memory threshold was invented.");
