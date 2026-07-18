using System.Text.Json;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Templates;

namespace Btw.TemplatePdf.Infrastructure.Assets;

/// <summary>
/// Resolves studio assets embedded as data URLs in <see cref="TemplateAssetRef.StorageKey"/>.
/// </summary>
public sealed class EmbeddedDataUrlAssetStore : IAssetStore
{
    public Task<IReadOnlyDictionary<string, byte[]>> ResolveAsync(
        IEnumerable<TemplateAssetRef> assets,
        CancellationToken cancellationToken = default)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(asset.Id))
                continue;

            var payload = asset.StorageKey;
            if (string.IsNullOrWhiteSpace(payload))
                continue;

            if (TryDecodeDataUrl(payload, out var bytes) || TryDecodeRawBase64(payload, out bytes))
                map[asset.Id] = bytes;
        }

        return Task.FromResult<IReadOnlyDictionary<string, byte[]>>(map);
    }

    internal static IReadOnlyList<TemplateAssetRef> ParseAssetsJson(string? assetsJson)
    {
        if (string.IsNullOrWhiteSpace(assetsJson) || assetsJson.Trim() == "[]")
            return Array.Empty<TemplateAssetRef>();

        try
        {
            using var doc = JsonDocument.Parse(assetsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<TemplateAssetRef>();

            var list = new List<TemplateAssetRef>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var mime = el.TryGetProperty("mime", out var mimeProp)
                    ? mimeProp.GetString() ?? "application/octet-stream"
                    : "application/octet-stream";
                var name = el.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                var dataUrl = el.TryGetProperty("dataUrl", out var dataProp)
                    ? dataProp.GetString()
                    : null;

                list.Add(new TemplateAssetRef
                {
                    Id = id,
                    Mime = mime,
                    Name = name,
                    Role = "image",
                    StorageKey = dataUrl
                });
            }

            return list;
        }
        catch
        {
            return Array.Empty<TemplateAssetRef>();
        }
    }

    private static bool TryDecodeDataUrl(string payload, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        const string marker = ";base64,";
        var idx = payload.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return false;

        var b64 = payload[(idx + marker.Length)..];
        try
        {
            bytes = Convert.FromBase64String(b64);
            return bytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDecodeRawBase64(string payload, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        try
        {
            bytes = Convert.FromBase64String(payload.Trim());
            return bytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
