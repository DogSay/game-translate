using System.Text;
using System.Text.RegularExpressions;

namespace GameTranslate.Core;

public sealed class ProtectedLayout
{
    private readonly IReadOnlyList<object> _parts;
    public IReadOnlyList<string> VisibleSegments { get; }

    internal ProtectedLayout(IReadOnlyList<object> parts, IReadOnlyList<string> visibleSegments)
    {
        _parts = parts;
        VisibleSegments = visibleSegments;
    }

    public string Reassemble(IReadOnlyList<string> converted)
    {
        if (converted.Count != VisibleSegments.Count)
            throw new InvalidDataException("Converted segment count does not match protected text layout.");
        var result = new StringBuilder();
        foreach (var part in _parts)
            result.Append(part is int index ? converted[index] : (string)part);
        return result.ToString();
    }
}

public static partial class ProtectedText
{
    [GeneratedRegex(@"(<[^>]*>|\{[^{}\r\n]*\}|%(?:\d+\$)?[-+0 #]*\d*(?:\.\d+)?[A-Za-z]|\\(?:[nrt\\]|u[0-9A-Fa-f]{4})|\[/?[A-Za-z][^\]\r\n]*\])")]
    private static partial Regex ProtectedToken();

    [GeneratedRegex(@"\p{IsCJKUnifiedIdeographs}")]
    private static partial Regex HanCharacter();

    public static ProtectedLayout CreateLayout(string text)
    {
        text ??= string.Empty;
        var parts = new List<object>();
        var visible = new List<string>();
        var position = 0;
        foreach (Match match in ProtectedToken().Matches(text))
        {
            AddVisible(text[position..match.Index], parts, visible);
            parts.Add(match.Value);
            position = match.Index + match.Length;
        }
        AddVisible(text[position..], parts, visible);
        return new(parts, visible);
    }

    public static IReadOnlyList<string> Tokens(string text) => ProtectedToken().Matches(text ?? string.Empty).Select(match => match.Value).ToArray();

    private static void AddVisible(string value, List<object> parts, List<string> visible)
    {
        if (HanCharacter().IsMatch(value))
        {
            parts.Add(visible.Count);
            visible.Add(value);
        }
        else parts.Add(value);
    }
}
