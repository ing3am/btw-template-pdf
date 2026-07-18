namespace Btw.TemplatePdf.Infrastructure.Invoices;

public sealed class FeDianOptions
{
    public const string SectionName = "FeDian";

    /// <summary>Base URL of the FE service (same as ARService URL_FE).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>UAT or PRD — matches GetDocumentFromDian path segment.</summary>
    public string Environment { get; set; } = "UAT";

    /// <summary>Optional auth key sent as User/Password headers to auth/Authentication.</summary>
    public string? AuthKey { get; set; }

    /// <summary>When true and DIAN/FE is unavailable, fall back to the in-memory demo UBL.</summary>
    public bool AllowStubFallback { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 60;
}
