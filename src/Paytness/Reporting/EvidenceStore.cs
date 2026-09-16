using System.Text.Json;

namespace Paytness.Reporting;

public sealed record EvidenceTruncation(bool Truncated, long RetainedBytes, int RetainedItems, int DroppedItems);

public sealed class EvidenceStore
{
    public const long MaxBytes = 64L * 1024 * 1024;
    public const int MaxRetainedStringBytes = 64 * 1024;
    private const int MaxConservativeStringCharacters = MaxRetainedStringBytes / 4;

    private readonly List<EvidenceItem> _items = [];
    private readonly long _maxBytes;
    private long _retainedBytes;
    private int _droppedItems;

    public EvidenceStore() : this(MaxBytes) { }

    internal EvidenceStore(long maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);
        _maxBytes = Math.Min(maxBytes, MaxBytes);
    }

    public IReadOnlyList<EvidenceItem> Items => _items;

    public EvidenceTruncation Truncation => new(
        _droppedItems > 0,
        _retainedBytes,
        _items.Count,
        _droppedItems);

    public void Add(EvidenceItem item)
    {
        EvidenceItem sanitized = Sanitize(item);
        int bytes = JsonSerializer.SerializeToUtf8Bytes(sanitized).Length;
        if (_retainedBytes + bytes > _maxBytes)
        {
            _droppedItems++;
            return;
        }

        _items.Add(sanitized);
        _retainedBytes += bytes;
    }

    public void AddRange(IEnumerable<EvidenceItem> items)
    {
        foreach (EvidenceItem item in items) Add(item);
    }

    private static EvidenceItem Sanitize(EvidenceItem item)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach ((string key, object? value) in item.Metadata)
        {
            metadata[key] = IsSensitiveKey(key) ? "[REDACTED]" : SanitizeValue(value);
        }

        return item with
        {
            Category = Truncate(item.Category),
            EntityId = Truncate(item.EntityId),
            Summary = Truncate(item.Summary),
            Metadata = metadata,
        };
    }

    private static object? SanitizeValue(object? value) => value is string text ? Truncate(text) : value;

    private static string Truncate(string value) => value.Length <= MaxConservativeStringCharacters
        ? value
        : value[..MaxConservativeStringCharacters] + "…[truncated]";

    private static bool IsSensitiveKey(string key)
    {
        string normalized = key.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return normalized.Contains("secret", StringComparison.Ordinal)
            || normalized.Contains("authorization", StringComparison.Ordinal)
            || normalized.Contains("cookie", StringComparison.Ordinal)
            || normalized.Contains("token", StringComparison.Ordinal)
            || normalized.Contains("password", StringComparison.Ordinal)
            || normalized.Contains("apikey", StringComparison.Ordinal);
    }
}
