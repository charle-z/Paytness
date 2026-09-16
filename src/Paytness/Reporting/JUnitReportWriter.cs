using System.Globalization;
using System.Xml;

namespace Paytness.Reporting;

public static class JUnitReportWriter
{
    public static async Task WriteAsync(string path, RunReport report, CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var settings = new XmlWriterSettings
        {
            Async = true,
            Indent = true,
            OmitXmlDeclaration = false,
        };
        await using var stream = File.Create(path);
        await using XmlWriter writer = XmlWriter.Create(stream, settings);

        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(null, "testsuite", null);
        await writer.WriteAttributeStringAsync(null, "name", null, report.ScenarioId);
        await writer.WriteAttributeStringAsync(null, "tests", null, report.Invariants.Count.ToString(CultureInfo.InvariantCulture));
        await writer.WriteAttributeStringAsync(null, "failures", null, report.Invariants.Count(static item => item.Status == InvariantStatus.Fail).ToString(CultureInfo.InvariantCulture));
        await writer.WriteAttributeStringAsync(null, "errors", null, report.Invariants.Count(static item => item.Status == InvariantStatus.Error).ToString(CultureInfo.InvariantCulture));

        foreach (InvariantResult invariant in report.Invariants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteStartElementAsync(null, "testcase", null);
            await writer.WriteAttributeStringAsync(null, "classname", null, "Paytness.Invariants");
            await writer.WriteAttributeStringAsync(null, "name", null, invariant.Id);

            if (invariant.Status is InvariantStatus.Fail or InvariantStatus.Error)
            {
                string elementName = invariant.Status == InvariantStatus.Fail ? "failure" : "error";
                await writer.WriteStartElementAsync(null, elementName, null);
                await writer.WriteAttributeStringAsync(null, "message", null, invariant.Message ?? $"Invariant {invariant.Status}.");
                string details = $"kind={invariant.Kind}; expected={Format(invariant.Expected)}; actual={Format(invariant.Actual)}; evidence={string.Join(',', invariant.EvidenceRefs)}";
                await writer.WriteStringAsync(details);
                await writer.WriteEndElementAsync();
            }

            await writer.WriteEndElementAsync();
        }

        await writer.WriteEndElementAsync();
        await writer.WriteEndDocumentAsync();
        await writer.FlushAsync();
    }

    private static string Format(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<none>";
}
