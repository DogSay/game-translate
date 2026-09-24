using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GameTranslate.Core;

public static class TranslationMemoryDocuments
{
    public static Dictionary<string, string> Parse(string json)
    {
        using var document = JsonDocument.Parse((json ?? string.Empty).TrimStart('\uFEFF'));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Translation memory is not a JSON object.");

        var entries = root;
        if (root.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Number)
        {
            if (!version.TryGetInt32(out var number) || number != 1)
                throw new InvalidDataException($"Unsupported translation-memory version: {version.GetRawText()}.");
            if (!root.TryGetProperty("entries", out entries) || entries.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Translation-memory v1 snapshot requires an entries object.");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in entries.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"Translation-memory value for {property.Name} must be a string.");
            if (!result.TryAdd(property.Name, property.Value.GetString()!))
                throw new InvalidDataException($"Translation memory contains a duplicate key: {property.Name}.");
        }
        return result;
    }
}

public sealed class TranslationMemoryApi : ITranslationApi
{
    private readonly string _path;
    private readonly ITranslationApi _upstream;
    private readonly Dictionary<string, string> _memory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TranslationMemoryApi(string path, ITranslationApi upstream)
    {
        _path = Path.GetFullPath(path);
        _upstream = upstream;
        _memory = File.Exists(_path)
            ? TranslationMemoryDocuments.Parse(File.ReadAllText(_path))
            : new(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<string>> ConvertBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var missing = texts.Where(text => !_memory.ContainsKey(text)).Distinct(StringComparer.Ordinal).ToArray();
            if (missing.Length > 0)
            {
                var converted = await _upstream.ConvertBatchAsync(missing, cancellationToken);
                if (converted.Count != missing.Length || converted.Any(value => value is null))
                    throw new InvalidDataException("Translation provider response cannot be stored in memory.");
                for (var index = 0; index < missing.Length; index++) _memory[missing[index]] = converted[index];
                Persist();
            }
            return texts.Select(text => _memory[text]).ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Persist()
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporary = _path + $".writing.{Guid.NewGuid():N}";
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var json = JsonSerializer.Serialize(_memory, options) + Environment.NewLine;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, _path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
    }
}
