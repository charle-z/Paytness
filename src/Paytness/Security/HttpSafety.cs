using System.Text.Json;

namespace Paytness.Security;

public sealed class HttpLimitException(string message) : IOException(message);

public static class HttpSafety
{
    public const int MaxResponseBytes = 256 * 1024;
    public const int MaxHeaders = 64;
    public const int MaxHeaderBytes = 16 * 1024;

    public static void ValidateResponseHeaders(HttpResponseMessage response)
    {
        int count = 0;
        int bytes = 0;
        foreach ((string name, IEnumerable<string> values) in response.Headers.Concat(response.Content.Headers))
        {
            count++;
            bytes += name.Length;
            foreach (string value in values) bytes += value.Length;
            if (count > MaxHeaders) throw new HttpLimitException($"SUT response exceeds {MaxHeaders} headers.");
            if (bytes > MaxHeaderBytes) throw new HttpLimitException($"SUT response headers exceed {MaxHeaderBytes} bytes.");
        }
    }

    public static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long length && length > MaxResponseBytes)
            throw new HttpLimitException($"SUT response exceeds {MaxResponseBytes} bytes.");

        await using Stream source = await content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream(capacity: Math.Min(MaxResponseBytes, 16 * 1024));
        byte[] buffer = new byte[8 * 1024];
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (destination.Length + read > MaxResponseBytes)
                throw new HttpLimitException($"SUT response exceeds {MaxResponseBytes} bytes.");
            destination.Write(buffer, 0, read);
        }
        return destination.ToArray();
    }

    public static async Task<JsonElement> ReadBoundedJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ValidateResponseHeaders(response);
        byte[] body = await ReadBoundedAsync(response.Content, cancellationToken);
        using JsonDocument document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 32 });
        return document.RootElement.Clone();
    }
}
