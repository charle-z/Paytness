using Paytness.Reporting;

namespace Paytness.Tests.Reporting;

public sealed class SafeTextTests
{
    [Fact]
    public void ControlCharactersAreEscaped()
    {
        string sanitized = SafeText.Sanitize("line1\r\n\u001B[31mline2");
        Assert.DoesNotContain('\r', sanitized);
        Assert.DoesNotContain('\n', sanitized);
        Assert.DoesNotContain('\u001B', sanitized);
        Assert.Contains("\\r\\n", sanitized, StringComparison.Ordinal);
        Assert.Contains("\\u001B", sanitized, StringComparison.Ordinal);
    }
}
