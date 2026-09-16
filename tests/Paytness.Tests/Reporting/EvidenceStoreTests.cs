using Paytness.Reporting;

namespace Paytness.Tests.Reporting;

public sealed class EvidenceStoreTests
{
    [Fact]
    public void SensitiveMetadataIsRedactedBeforeRetention()
    {
        var store = new EvidenceStore();
        store.Add(new EvidenceItem(1, "test", "item-1", "safe", new Dictionary<string, object?>
        {
            ["webhookSecret"] = "must-not-survive",
            ["logicalPayment"] = "order-1",
        }));

        EvidenceItem item = Assert.Single(store.Items);
        Assert.Equal("[REDACTED]", item.Metadata["webhookSecret"]);
        Assert.Equal("order-1", item.Metadata["logicalPayment"]);
    }

    [Fact]
    public void RetainedStringsAreBounded()
    {
        var store = new EvidenceStore();
        string large = new('x', EvidenceStore.MaxRetainedStringBytes * 2);
        store.Add(new EvidenceItem(1, "test", "item-1", large, new Dictionary<string, object?> { ["value"] = large }));

        EvidenceItem item = Assert.Single(store.Items);
        Assert.True(item.Summary.Length < large.Length);
        Assert.True(Assert.IsType<string>(item.Metadata["value"]).Length < large.Length);
    }

    [Fact]
    public void StoreReportsTruncationWhenByteBudgetIsExceeded()
    {
        var store = new EvidenceStore(1_024);
        string payload = new string('x', 900);
        store.Add(new EvidenceItem(1, "test", "one", payload, new Dictionary<string, object?>()));
        store.Add(new EvidenceItem(2, "test", "two", payload, new Dictionary<string, object?>()));

        Assert.True(store.Truncation.Truncated);
        Assert.True(store.Truncation.RetainedBytes <= 1_024);
        Assert.True(store.Truncation.DroppedItems >= 1);
    }
}
