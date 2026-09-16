using Paytness.Security;

namespace Paytness.Tests.Security;

public sealed class HttpSafetyTests
{
    [Fact]
    public async Task OversizedContentLengthIsRejected()
    {
        using var content = new ByteArrayContent(new byte[HttpSafety.MaxResponseBytes + 1]);
        await Assert.ThrowsAsync<HttpLimitException>(() => HttpSafety.ReadBoundedAsync(content, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TooManyResponseHeadersAreRejected()
    {
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        for (int index = 0; index <= HttpSafety.MaxHeaders; index++)
            response.Headers.TryAddWithoutValidation($"X-Test-{index}", "x");
        Assert.Throws<HttpLimitException>(() => HttpSafety.ValidateResponseHeaders(response));
    }
}
