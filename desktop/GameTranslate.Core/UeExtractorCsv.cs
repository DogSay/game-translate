using System.Text.RegularExpressions;

namespace GameTranslate.Core;

public sealed record CsvNormalization(string Text, int Repairs);
public sealed record UeLocalizationExtraction(string Csv, string? Hashes);

public static class UeExtractionFiles
{
    public static UeLocalizationExtraction FindLocalization(string directory, string name, string archiveBase)
    {
        ValidateName(name, archiveBase);
        var exactStem = Path.Combine(directory, $"{name}_{archiveBase}");
        var exactCsv = exactStem + ".csv";
        if (File.Exists(exactCsv))
            return new(exactCsv, Existing(exactStem + ".locreshashes"));

        var genericStem = Path.Combine(directory, name);
        var genericCsv = genericStem + ".csv";
        var competing = Directory.EnumerateFiles(directory, $"{name}_*.csv", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).Contains("_skipped_lines", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (File.Exists(genericCsv))
        {
            if (competing.Length > 0)
                throw new InvalidDataException($"Ambiguous UEExtractor output for {name}: generic and archive-specific CSV files both exist.");
            return new(genericCsv, Existing(genericStem + ".locreshashes"));
        }

        if (competing.Length > 0)
            throw new FileNotFoundException($"UEExtractor did not emit {name} for requested archive {archiveBase}; refusing a different archive output.", exactCsv);
        throw new FileNotFoundException($"UEExtractor did not emit localization CSV for {name}.", exactCsv);
    }

    public static string Find(string directory, string name, string archiveBase, string extension)
    {
        return FindOptional(directory, name, archiveBase, extension)
            ?? throw new FileNotFoundException($"無法唯一識別 {name} 嘅 {extension} 抽取結果。", Path.Combine(directory, $"{name}_{archiveBase}{extension}"));
    }

    public static string? FindOptional(string directory, string name, string archiveBase, string extension)
    {
        ValidateName(name, archiveBase);
        var exact = Path.Combine(directory, $"{name}_{archiveBase}{extension}");
        if (File.Exists(exact)) return exact;
        var generic = Path.Combine(directory, $"{name}{extension}");
        if (File.Exists(generic)) return generic;
        return null;
    }

    private static string? Existing(string path) => File.Exists(path) ? path : null;

    private static void ValidateName(string name, string archiveBase)
    {
        if (!Path.GetFileName(name).Equals(name, StringComparison.Ordinal) ||
            !Path.GetFileName(archiveBase).Equals(archiveBase, StringComparison.Ordinal))
            throw new InvalidDataException("Unsafe UEExtractor output name.");
    }
}

public static class UeExtractorCsv
{
    public static CsvNormalization Normalize(string text, IReadOnlySet<string> locresKeys)
    {
        var document = LocalizationCsv.Parse(text);
        var normalized = new List<List<string>>();
        var repairs = 0;
        for (var index = 0; index < document.Rows.Count; index++)
        {
            var row = document.Rows[index];
            var next = index + 1 < document.Rows.Count ? document.Rows[index + 1] : null;
            if (row.Count >= 1 && row.Skip(1).All(string.IsNullOrEmpty) && next is { Count: >= 2 })
            {
                var key = $"{row[0]}\n{next[0]}";
                if (locresKeys.Contains(key))
                {
                    normalized.Add([key, .. next.Skip(1)]);
                    repairs++;
                    index++;
                    continue;
                }
            }
            normalized.Add(row);
        }
        if (repairs == 0) return new(text, 0);
        return new(Serialize(normalized, document.EndOfLine), repairs);
    }

    private static string Serialize(IReadOnlyList<List<string>> rows, string eol)
    {
        static string Escape(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
        return rows.Count == 0 ? string.Empty : string.Join(eol, rows.Select(row => string.Join(',', row.Select(Escape)))) + eol;
    }
}

public static partial class UnrealVerification
{
    public static void ExactPaths(string output, IReadOnlyList<string> expected)
    {
        var actual = (output ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var wanted = expected.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
            throw new InvalidDataException("Packed virtual paths do not exactly match the staged localization paths.");
    }
}
