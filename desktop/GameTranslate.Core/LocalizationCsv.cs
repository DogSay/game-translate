using System.Text;

namespace GameTranslate.Core;

public sealed class LocalizationCsv
{
    public List<List<string>> Rows { get; }
    public string EndOfLine { get; }

    private LocalizationCsv(List<List<string>> rows, string endOfLine)
    {
        Rows = rows;
        EndOfLine = endOfLine;
    }

    public static LocalizationCsv Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var eol = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else if (character == '"') quoted = false;
                else field.Append(character);
                continue;
            }

            if (character == '"' && field.Length == 0) quoted = true;
            else if (character == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = [];
            }
            else field.Append(character);
        }

        if (quoted) throw new InvalidDataException("Malformed CSV: unterminated quoted field.");
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return new(rows, eol);
    }

    public string Serialize()
    {
        if (Rows.Count == 0) return string.Empty;
        return string.Join(EndOfLine, Rows.Select(row => string.Join(',', row.Select(Escape)))) + EndOfLine;
    }

    public int HeaderIndex(string name)
    {
        if (Rows.Count == 0) return -1;
        return Rows[0].FindIndex(value => value.TrimStart('\uFEFF').Trim().Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string Escape(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
        ? $"\"{value.Replace("\"", "\"\"")}\""
        : value;
}
