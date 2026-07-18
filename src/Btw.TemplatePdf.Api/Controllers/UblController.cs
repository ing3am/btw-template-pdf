using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Infrastructure.Invoices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Btw.TemplatePdf.Api.Controllers;

/// <summary>
/// UBL lookup by CUFE — same FE path as ARService DianDocument/GetUbl
/// (<c>GetDocumentFromDian</c>).
/// </summary>
[ApiController]
[Route("api/v1/ubl")]
public sealed class UblController : ControllerBase
{
    private readonly IUblStore _ublStore;
    private readonly FeDianOptions _options;

    public UblController(IUblStore ublStore, IOptions<FeDianOptions> options)
    {
        _ublStore = ublStore;
        _options = options.Value;
    }

    public sealed record UblByCufeResponse(
        string Cufe,
        string? Nit,
        string Environment,
        string TypeDocument,
        string UblXml,
        bool FeConfigured);

    /// <summary>GET /api/v1/ubl/by-cufe?cufe=…&amp;nit=…&amp;typeDocument=UBL</summary>
    [HttpGet("by-cufe")]
    [ProducesResponseType(typeof(UblByCufeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ByCufe(
        [FromQuery] string cufe,
        [FromQuery] string? nit = null,
        [FromQuery] string typeDocument = "UBL",
        CancellationToken cancellationToken = default)
    {
        _ = typeDocument;
        var key = (cufe ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(key))
        {
            return BadRequest(new
            {
                code = "validation_error",
                message = "cufe is required.",
                traceId = HttpContext.TraceIdentifier
            });
        }

        string? ublXml;
        try
        {
            ublXml = await _ublStore
                .GetUblXmlAsync(nit ?? "900000000", key, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                code = "dian_upstream_error",
                message = ex.Message,
                traceId = HttpContext.TraceIdentifier
            });
        }

        if (string.IsNullOrWhiteSpace(ublXml))
        {
            return NotFound(new
            {
                code = "invoice_not_found",
                message = $"No UBL found for CUFE {key}.",
                traceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(new UblByCufeResponse(
            Cufe: key,
            Nit: nit,
            Environment: _options.Environment,
            TypeDocument: string.IsNullOrWhiteSpace(typeDocument)
                ? "UBL"
                : typeDocument.Trim().ToUpperInvariant(),
            UblXml: ublXml,
            FeConfigured: !string.IsNullOrWhiteSpace(_options.BaseUrl)));
    }
}
