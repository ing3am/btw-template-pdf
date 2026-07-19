namespace Btw.TemplatePdf.Infrastructure.Invoices;

public sealed class FeDianOptions
{
    public const string SectionName = "FeDian";

    public const string QrCatalogProd =
        "https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey=";

    public const string QrCatalogHab =
        "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=";

    /// <summary>Base URL of the FE service (same as ARService URL_FE).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>UAT or PRD — matches GetDocumentFromDian path segment and DIAN QR catalog host.</summary>
    public string Environment { get; set; } = "UAT";

    /// <summary>Optional auth key sent as User/Password headers to auth/Authentication.</summary>
    public string? AuthKey { get; set; }

    /// <summary>When true and DIAN/FE is unavailable, fall back to the in-memory demo UBL.</summary>
    public bool AllowStubFallback { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// PRD → catalogo-vpfe; anything else (UAT/HAB) → catalogo-vpfe-hab.
    /// </summary>
    public string BuildQrSearchUrl(string documentKey)
    {
        var key = (documentKey ?? string.Empty).Trim();
        var isPrd = string.Equals(Environment?.Trim(), "PRD", StringComparison.OrdinalIgnoreCase);
        var prefix = isPrd ? QrCatalogProd : QrCatalogHab;
        return prefix + key;
    }
}
