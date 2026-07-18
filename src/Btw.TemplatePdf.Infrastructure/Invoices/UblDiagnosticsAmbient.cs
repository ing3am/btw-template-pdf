namespace Btw.TemplatePdf.Infrastructure.Invoices;

/// <summary>
/// Correlates UBL fetch diagnostic file with the subsequent mapping write
/// within the same async request.
/// </summary>
internal static class UblDiagnosticsAmbient
{
    private static readonly AsyncLocal<string?> ReportPath = new();

    public static string? CurrentReportPath
    {
        get => ReportPath.Value;
        set => ReportPath.Value = value;
    }
}
