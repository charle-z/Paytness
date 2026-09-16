using System.Globalization;
using System.Text;

namespace Paytness.Reporting;

public static class SafeText
{
    public const int DefaultMaxCharacters = 4_096;

    public static string Sanitize(object? value, int maxCharacters = DefaultMaxCharacters)
    {
        string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<none>";
        var output = new StringBuilder(Math.Min(text.Length, maxCharacters));
        for (int index = 0; index < text.Length && output.Length < maxCharacters; index++)
        {
            char character = text[index];
            switch (character)
            {
                case '\r': output.Append("\\r"); break;
                case '\n': output.Append("\\n"); break;
                case '\t': output.Append("\\t"); break;
                default:
                    if (char.IsControl(character) || (char.IsSurrogate(character) && !IsValidSurrogateAt(text, index)))
                    {
                        output.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        output.Append(character);
                    }
                    break;
            }
        }

        if (text.Length > maxCharacters) output.Append("…[truncated]");
        return output.ToString();
    }

    private static bool IsValidSurrogateAt(string value, int index)
    {
        char current = value[index];
        if (char.IsHighSurrogate(current))
            return index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]);
        if (char.IsLowSurrogate(current))
            return index > 0 && char.IsHighSurrogate(value[index - 1]);
        return true;
    }
}
