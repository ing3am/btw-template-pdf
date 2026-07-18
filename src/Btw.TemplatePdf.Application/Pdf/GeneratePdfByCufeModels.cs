using Btw.TemplatePdf.Domain.Common;

namespace Btw.TemplatePdf.Application.Pdf;

public sealed record GeneratePdfByCufeRequest(
    string Nit,
    string Cufe,
    DocumentType DocumentType = DocumentType.Factura);

public sealed record GeneratePdfByCufeResponse(
    string Nit,
    string Cufe,
    DocumentType DocumentType,
    Guid TemplateId,
    int TemplateVersion,
    string ContentType,
    string FileName,
    string PdfBase64);

public sealed class PdfGenerationException : Exception
{
    public string Code { get; }

    public PdfGenerationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
