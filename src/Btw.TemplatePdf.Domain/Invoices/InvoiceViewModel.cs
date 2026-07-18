namespace Btw.TemplatePdf.Domain.Invoices;

/// <summary>
/// Runtime view-model aligned with studio DIAN paths (documento.*, items[], …).
/// Produced by adapting UBL; consumed by the PDF renderer.
/// </summary>
public sealed class InvoiceViewModel
{
    public required string Nit { get; init; }
    public required string Cufe { get; init; }
    public required IReadOnlyDictionary<string, object?> Data { get; init; }
}
