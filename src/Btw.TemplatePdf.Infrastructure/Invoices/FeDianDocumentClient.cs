using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Btw.TemplatePdf.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Btw.TemplatePdf.Infrastructure.Invoices;

/// <summary>
/// HTTP client mirroring ARService <c>FeDocumentClient.GetUblFromDianAsync</c>:
/// GET {URL_FE}clientDian/ClientWcfDian/GetDocumentFromDian/{cufe}/{env}/false?typeDocument=UBL
/// Prefer the studio-forwarded FE Bearer token when present.
/// </summary>
public sealed class FeDianDocumentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly FeDianOptions _options;
    private readonly IFeBearerTokenAccessor _bearer;
    private readonly ILogger<FeDianDocumentClient> _logger;

    public FeDianDocumentClient(
        HttpClient http,
        IOptions<FeDianOptions> options,
        IFeBearerTokenAccessor bearer,
        ILogger<FeDianDocumentClient> logger)
    {
        _http = http;
        _options = options.Value;
        _bearer = bearer;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<string?> GetUblXmlAsync(
        string documentKey,
        string typeDocument = "UBL",
        CancellationToken cancellationToken = default)
    {
        var key = (documentKey ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException("CUFE/CUDE is required.");

        if (!IsConfigured)
            throw new InvalidOperationException("FeDian:BaseUrl is not configured (URL_FE).");

        var baseUrl = NormalizeBaseUrl(_options.BaseUrl);
        var environment = string.IsNullOrWhiteSpace(_options.Environment)
            ? "UAT"
            : _options.Environment.Trim().ToUpperInvariant();
        var type = string.IsNullOrWhiteSpace(typeDocument)
            ? "UBL"
            : typeDocument.Trim().ToUpperInvariant();

        var requestUrl =
            $"{baseUrl}clientDian/ClientWcfDian/GetDocumentFromDian/{Uri.EscapeDataString(key)}/{environment}/false?typeDocument={Uri.EscapeDataString(type)}";

        _logger.LogInformation("Consulting UBL via GetDocumentFromDian: {Url}", requestUrl);

        using var response = await SendWithOptionalAuthAsync(baseUrl, requestUrl, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "GetDocumentFromDian HTTP {Status}: {Body}",
                (int)response.StatusCode,
                Truncate(body, 500));
            return null;
        }

        FePetitionResponse? feResponse;
        try
        {
            feResponse = JsonSerializer.Deserialize<FePetitionResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid FE JSON for GetDocumentFromDian");
            return null;
        }

        if (feResponse is null || !feResponse.Success)
        {
            _logger.LogWarning(
                "GetDocumentFromDian unsuccessful: {Message}",
                feResponse?.Message ?? "null response");
            return null;
        }

        var base64Result = ExtractResultBase64(feResponse.Result);
        if (string.IsNullOrWhiteSpace(base64Result))
        {
            _logger.LogWarning("GetDocumentFromDian returned empty result for key {Key}", key);
            return null;
        }

        try
        {
            var ublXml = DecodeUblPayload(base64Result);
            return string.IsNullOrWhiteSpace(ublXml) ? null : ublXml;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode UBL payload for key {Key}", key);
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendWithOptionalAuthAsync(
        string baseUrl,
        string requestUrl,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_bearer.Token))
        {
            using var withUserToken = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            withUserToken.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _bearer.Token.Trim());
            var userResponse = await _http.SendAsync(withUserToken, cancellationToken)
                .ConfigureAwait(false);
            if (userResponse.StatusCode != HttpStatusCode.Unauthorized)
                return userResponse;

            _logger.LogWarning("Forwarded FE Bearer was rejected; trying service AuthKey fallback.");
            userResponse.Dispose();
        }

        var response = await _http.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        var token = await AuthenticateAsync(baseUrl, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("FE auth failed; cannot retry GetDocumentFromDian");
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"success\":false,\"message\":\"FE authentication failed\"}")
            };
        }

        using var authorized = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        authorized.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _http.SendAsync(authorized, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> AuthenticateAsync(string baseUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}auth/Authentication")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.AuthKey))
        {
            request.Headers.TryAddWithoutValidation("User", _options.AuthKey);
            request.Headers.TryAddWithoutValidation("Password", _options.AuthKey);
        }

        using var authResponse = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var authBody = await authResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!authResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Auth FE failed. Status={Status}, Body={Body}",
                (int)authResponse.StatusCode,
                Truncate(authBody, 300));
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(authBody);
            if (doc.RootElement.TryGetProperty("token", out var tokenProp))
                return tokenProp.GetString();
            if (doc.RootElement.TryGetProperty("Token", out var tokenProp2))
                return tokenProp2.GetString();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse FE auth token");
        }

        return null;
    }

    internal static string DecodeUblPayload(string base64)
    {
        var bytes = Convert.FromBase64String(base64.Trim());
        if (IsZip(bytes))
        {
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.Entries.FirstOrDefault(e =>
                             e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                         ?? zip.Entries.FirstOrDefault();
            if (entry is null)
                throw new InvalidOperationException("ZIP from DIAN contains no entries.");

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static bool IsZip(byte[] bytes) =>
        bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B;

    private static string? ExtractResultBase64(JsonElement? result)
    {
        if (result is null || result.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return result.Value.ValueKind switch
        {
            JsonValueKind.String => result.Value.GetString(),
            JsonValueKind.Object when result.Value.TryGetProperty("ublXml", out var ubl) => ubl.GetString(),
            JsonValueKind.Object when result.Value.TryGetProperty("XmlBase64Bytes", out var xml) => xml.GetString(),
            _ => result.Value.ToString()
        };
    }

    private static string NormalizeBaseUrl(string urlFe) =>
        urlFe.EndsWith('/') ? urlFe : urlFe + "/";

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private sealed class FePetitionResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("result")]
        public JsonElement? Result { get; set; }
    }
}
