using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GameTranslate.Core;

public interface ITranslationApi
{
    Task<IReadOnlyList<string>> ConvertBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}

public sealed class ZhConvertClient : ITranslationApi
{
    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;

    public ZhConvertClient(HttpClient? httpClient = null, string endpoint = "https://api.zhconvert.org/convert")
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _endpoint = new Uri(endpoint);
    }

    public async Task<IReadOnlyList<string>> ConvertBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        if (texts.Count == 0) return [];
        var submitted = JsonSerializer.Serialize(texts, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["converter"] = "Taiwan",
                ["text"] = submitted,
            });
            using var response = await _httpClient.PostAsync(_endpoint, content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (!root.TryGetProperty("code", out var code) || code.GetInt32() != 0)
                    throw new InvalidDataException("繁化姬 API 回傳錯誤。" + ApiMessage(root));
                if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("text", out var convertedText))
                    throw new InvalidDataException("繁化姬 API 沒有回傳轉換文字。");
                var values = JsonSerializer.Deserialize<string[]>(convertedText.GetString() ?? string.Empty)
                    ?? throw new InvalidDataException("繁化姬批次回應不是有效陣列。");
                if (values.Length != texts.Count) throw new InvalidDataException("繁化姬批次回應數量不一致。");
                return values;
            }

            var transient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            if (!transient || attempt == 5)
                throw new HttpRequestException($"繁化姬 API HTTP {(int)response.StatusCode}。", null, response.StatusCode);
            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Min(30, 2 * Math.Pow(2, attempt - 1)));
            await Task.Delay(delay, cancellationToken);
        }
        throw new InvalidOperationException("繁化姬重試流程異常結束。");
    }

    private static string ApiMessage(JsonElement root) => root.TryGetProperty("msg", out var message) ? $" {message.GetString()}" : string.Empty;
}

public sealed record TranslationResult(string Text, int Rows, int Converted, int Preserved, int Copied);

public static class LocalizationTranslator
{
    public static async Task<TranslationResult> ConvertAsync(
        string csv,
        ITranslationApi api,
        int batchSize,
        CancellationToken cancellationToken,
        Action<int, int>? progress = null)
    {
        var document = LocalizationCsv.Parse(csv);
        var sourceIndex = document.HeaderIndex("source");
        var translationIndex = document.HeaderIndex("translation");
        if (sourceIndex < 0 || translationIndex < 0)
            throw new InvalidDataException("Localization CSV requires source and Translation columns.");

        var pending = new List<(List<string> Row, ProtectedLayout Layout)>();
        var rowCount = 0;
        var preserved = 0;
        var copied = 0;
        foreach (var row in document.Rows.Skip(1))
        {
            if (row.All(string.IsNullOrEmpty)) continue;
            rowCount++;
            while (row.Count <= translationIndex) row.Add(string.Empty);
            if (!string.IsNullOrEmpty(row[translationIndex]))
            {
                preserved++;
                continue;
            }
            var source = row.Count > sourceIndex ? row[sourceIndex] : string.Empty;
            var layout = ProtectedText.CreateLayout(source);
            if (layout.VisibleSegments.Count == 0)
            {
                row[translationIndex] = source;
                copied++;
            }
            else pending.Add((row, layout));
        }

        var convertedRows = 0;
        var size = Math.Max(1, batchSize);
        for (var start = 0; start < pending.Count; start += size)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = pending.Skip(start).Take(size).ToArray();
            var segments = batch.SelectMany(item => item.Layout.VisibleSegments).ToArray();
            var converted = await api.ConvertBatchAsync(segments, cancellationToken);
            if (converted.Count != segments.Length)
                throw new InvalidDataException("Translation provider returned a different segment count.");
            if (converted.Any(value => string.IsNullOrWhiteSpace(value) || value.Contains('\uFFFD')))
                throw new InvalidDataException("Translation provider returned blank or replacement-character text.");
            var cursor = 0;
            foreach (var item in batch)
            {
                var values = converted.Skip(cursor).Take(item.Layout.VisibleSegments.Count).ToArray();
                cursor += values.Length;
                var result = item.Layout.Reassemble(values);
                if (!ProtectedText.Tokens(item.Row[sourceIndex]).SequenceEqual(ProtectedText.Tokens(result)))
                    throw new InvalidDataException("Protected placeholders changed during conversion.");
                item.Row[translationIndex] = result;
                convertedRows++;
            }
            progress?.Invoke(convertedRows, pending.Count);
        }
        return new(document.Serialize(), rowCount, convertedRows, preserved, copied);
    }
}
