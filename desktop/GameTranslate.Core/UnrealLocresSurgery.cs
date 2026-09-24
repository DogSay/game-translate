using System.Text;

namespace GameTranslate.Core;

public sealed record LocresSurgeryResult(byte[] Bytes, byte Version, int StringCount, int Replaced);

public static class UnrealLocresSurgery
{
    private static readonly byte[] Magic = [
        0x0E, 0x14, 0x74, 0x75, 0x67, 0x4A, 0x03, 0xFC,
        0x4A, 0x15, 0x90, 0x9D, 0xC3, 0x37, 0x7F, 0x1B,
    ];

    public static LocresSurgeryResult Patch(byte[] template, IReadOnlyDictionary<string, string> translations)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(translations);
        Require(template, 0, 25, "header");
        if (!template.AsSpan(0, Magic.Length).SequenceEqual(Magic)) throw new InvalidDataException("Invalid locres magic.");
        var version = template[16];
        if (version is < 1 or > 3) throw new InvalidDataException($"Unsupported locres version: {version}.");
        var arrayOffset64 = BitConverter.ToInt64(template, 17);
        if (arrayOffset64 < 25 || arrayOffset64 > int.MaxValue) throw new InvalidDataException("Invalid locres string array offset.");
        var arrayOffset = (int)arrayOffset64;
        Require(template, arrayOffset, 4, "string array count");
        var count = BitConverter.ToInt32(template, arrayOffset);
        if (count < 0) throw new InvalidDataException("Invalid locres string array count.");

        var offset = arrayOffset + 4;
        var items = new List<(string Value, int? ReferenceCount)>(count);
        for (var index = 0; index < count; index++)
        {
            var value = ReadFString(template, ref offset);
            int? referenceCount = null;
            if (version >= 2)
            {
                Require(template, offset, 4, "string reference count");
                referenceCount = BitConverter.ToInt32(template, offset);
                offset += 4;
            }
            items.Add((value, referenceCount));
        }
        if (offset != template.Length)
            throw new InvalidDataException($"Locres string array does not end at EOF ({offset} != {template.Length}).");

        using var output = new MemoryStream();
        output.Write(template, 0, arrayOffset + 4);
        var replaced = 0;
        foreach (var item in items)
        {
            var value = ReplacementFor(item.Value, translations);
            if (!value.Equals(item.Value, StringComparison.Ordinal)) replaced++;
            WriteFString(output, value);
            if (item.ReferenceCount is int referenceCount) output.Write(BitConverter.GetBytes(referenceCount));
        }
        return new(output.ToArray(), version, count, replaced);
    }

    private static string ReplacementFor(string value, IReadOnlyDictionary<string, string> translations)
    {
        if (translations.TryGetValue(value, out var direct)) return direct;
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (translations.TryGetValue(normalized, out var normalizedValue)) return normalizedValue;
        var newline = value.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var cfKey = value.Replace("\r\n", "<cf>", StringComparison.Ordinal).Replace("\n", "<cf>", StringComparison.Ordinal);
        return translations.TryGetValue(cfKey, out var cfValue)
            ? cfValue.Replace("<cf>", newline, StringComparison.Ordinal)
            : value;
    }

    private static string ReadFString(byte[] bytes, ref int offset)
    {
        Require(bytes, offset, 4, "FString length");
        var length = BitConverter.ToInt32(bytes, offset);
        offset += 4;
        if (length == 0) return string.Empty;
        if (length == int.MinValue) throw new InvalidDataException("Invalid locres FString length.");
        if (length < 0)
        {
            var units = -length;
            Require(bytes, offset, checked(units * 2), "UTF-16 FString");
            if (BitConverter.ToUInt16(bytes, offset + (units - 1) * 2) != 0) throw new InvalidDataException("Invalid locres UTF-16 terminator.");
            var value = Encoding.Unicode.GetString(bytes, offset, (units - 1) * 2);
            offset += units * 2;
            return value;
        }
        Require(bytes, offset, length, "ANSI FString");
        if (bytes[offset + length - 1] != 0) throw new InvalidDataException("Invalid locres ANSI terminator.");
        var ansi = Encoding.UTF8.GetString(bytes, offset, length - 1);
        offset += length;
        return ansi;
    }

    private static void WriteFString(Stream stream, string value)
    {
        if (value.Length == 0)
        {
            stream.Write(new byte[4]);
            return;
        }
        if (value.All(character => character <= 0x7F))
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(BitConverter.GetBytes(bytes.Length + 1));
            stream.Write(bytes);
            stream.WriteByte(0);
            return;
        }
        var unicode = Encoding.Unicode.GetBytes(value);
        stream.Write(BitConverter.GetBytes(-(unicode.Length / 2 + 1)));
        stream.Write(unicode);
        stream.WriteByte(0);
        stream.WriteByte(0);
    }

    private static void Require(byte[] bytes, int offset, int length, string label)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
            throw new InvalidDataException($"Invalid locres {label}.");
    }
}

public static class LocresTranslations
{
    public static IReadOnlyDictionary<string, string> FromCsv(string csvText,
        IReadOnlyDictionary<string, string>? memory = null)
    {
        var document = LocalizationCsv.Parse(csvText);
        var sourceIndex = document.HeaderIndex("source");
        var translationIndex = document.HeaderIndex("translation");
        if (sourceIndex < 0 || translationIndex < 0)
            throw new InvalidDataException("Localization CSV requires source and Translation columns.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in document.Rows.Skip(1))
        {
            var source = row.Count > sourceIndex ? row[sourceIndex] : string.Empty;
            var translation = row.Count > translationIndex ? row[translationIndex] : string.Empty;
            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(translation)
                && !source.Equals(translation, StringComparison.Ordinal) && !result.ContainsKey(source)) result[source] = translation;
            var normalizedSource = source.Replace("\r\n", "\n", StringComparison.Ordinal);
            if (!string.IsNullOrEmpty(normalizedSource) && !string.IsNullOrEmpty(translation)
                && !normalizedSource.Equals(source, StringComparison.Ordinal) && !result.ContainsKey(normalizedSource))
                result[normalizedSource] = translation;
        }
        if (memory is not null)
            foreach (var item in memory)
                result.TryAdd(item.Key, item.Value);
        return result;
    }
}

public static class LocresOverrides
{
    // Labels a faithful conversion cannot produce: with the compat patch replacing zh-Hans
    // content, the in-game language entry should read 繁體中文, not the converted 簡體中文.
    public static readonly IReadOnlyDictionary<string, string> Exact = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["简体中文"] = "繁體中文",
    };

    public static IReadOnlyDictionary<string, string> Apply(IReadOnlyDictionary<string, string> translations)
    {
        var result = new Dictionary<string, string>(translations, StringComparer.Ordinal);
        foreach (var item in Exact) result[item.Key] = item.Value;
        return result;
    }
}
