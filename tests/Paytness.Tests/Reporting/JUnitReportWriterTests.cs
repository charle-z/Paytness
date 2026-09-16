using System.Xml.Linq;
using Paytness.Provider;
using Paytness.Reporting;
using Paytness.Webhooks;

namespace Paytness.Tests.Reporting;

public sealed class JUnitReportWriterTests
{
    [Fact]
    public async Task WriterMapsInvariantFailureWithoutHttpBodies()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string path = Path.Combine(Path.GetTempPath(), $"paytness-{Guid.NewGuid():N}.xml");
        var report = new RunReport(
            1,
            "fixture",
            1,
            "hash",
            InvariantStatus.Fail,
            new ProviderMetrics(1, 2, 2, 2, 2),
            WebhookMetrics.Empty,
            [
                new InvariantResult("one-effect", "provider_effect_count", InvariantStatus.Fail, 1, 2, ["request-0001", "request-0002"]),
                new InvariantResult("paid", "observation", InvariantStatus.Pass, "Paid", "Paid", []),
            ],
            []);

        try
        {
            await JUnitReportWriter.WriteAsync(path, report, token);
            XDocument document = XDocument.Load(path);
            XElement suite = Assert.IsType<XElement>(document.Root);
            Assert.Equal("2", suite.Attribute("tests")?.Value);
            Assert.Equal("1", suite.Attribute("failures")?.Value);
            Assert.Equal("0", suite.Attribute("errors")?.Value);
            XElement failure = Assert.Single(suite.Descendants("failure"));
            Assert.Contains("request-0001,request-0002", failure.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("http", failure.Value, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
