using Btw.TemplatePdf.Application.Pdf;
using Btw.TemplatePdf.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Btw.TemplatePdf.Api.Controllers;

[ApiController]
[Route("api/v1/pdf")]
public sealed class PdfController : ControllerBase
{
    private readonly GeneratePdfByCufeUseCase _useCase;

    public PdfController(GeneratePdfByCufeUseCase useCase)
    {
        _useCase = useCase;
    }

    public sealed record ByCufeBody(string Nit, string Cufe, string? DocumentType);

    [HttpPost("by-cufe")]
    [ProducesResponseType(typeof(GeneratePdfByCufeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ByCufe(
        [FromBody] ByCufeBody body,
        CancellationToken cancellationToken)
    {
        try
        {
            var documentType = ParseDocumentType(body.DocumentType);
            var result = await _useCase.ExecuteAsync(
                new GeneratePdfByCufeRequest(body.Nit, body.Cufe, documentType),
                cancellationToken);
            return Ok(result);
        }
        catch (PdfGenerationException ex)
        {
            var status = ex.Code switch
            {
                "validation_error" => StatusCodes.Status400BadRequest,
                "template_not_found" or "invoice_not_found" => StatusCodes.Status404NotFound,
                "mapping_error" => StatusCodes.Status422UnprocessableEntity,
                _ => StatusCodes.Status500InternalServerError
            };

            return StatusCode(status, new
            {
                code = ex.Code,
                message = ex.Message,
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    private static DocumentType ParseDocumentType(string? value) =>
        (value ?? "factura").Trim().ToLowerInvariant() switch
        {
            "nota_credito" => DocumentType.NotaCredito,
            "nota_debito" => DocumentType.NotaDebito,
            "otro" => DocumentType.Otro,
            _ => DocumentType.Factura
        };
}
