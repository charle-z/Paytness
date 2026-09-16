using System.Globalization;
using System.Text.Json;

namespace Paytness.Observation;

public static class JsonPointer
{
    public static bool TryResolve(JsonElement root, string reference, out JsonElement value)
    {
        value = root;
        if (reference.Length == 0) return true;
        if (!reference.StartsWith('/')) return false;

        foreach (string rawToken in reference.Split('/').Skip(1))
        {
            string token = rawToken.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (!value.TryGetProperty(token, out value)) return false;
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int index) && index >= 0 && index < value.GetArrayLength())
            {
                value = value[index];
                continue;
            }

            return false;
        }

        return true;
    }
}
