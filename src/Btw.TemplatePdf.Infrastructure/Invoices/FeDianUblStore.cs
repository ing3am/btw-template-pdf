using Btw.TemplatePdf.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Btw.TemplatePdf.Infrastructure.Invoices;

/// <summary>
/// Resolves UBL XML by CUFE via FE GetDocumentFromDian, with optional in-memory demo fallback.
/// </summary>
public sealed class FeDianUblStore : IUblStore
{
    private readonly FeDianDocumentClient _client;
    private readonly InMemoryUblStore _stub;
    private readonly FeDianOptions _options;
    private readonly ILogger<FeDianUblStore> _logger;

    public FeDianUblStore(
        FeDianDocumentClient client,
        InMemoryUblStore stub,
        IOptions<FeDianOptions> options,
        ILogger<FeDianUblStore> logger)
    {
        _client = client;
        _stub = stub;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetUblXmlAsync(
        string nit,
        string cufe,
        CancellationToken cancellationToken = default)
    {
        if (_client.IsConfigured)
        {
            try
            {
                var ubl = await _client
                    .GetUblXmlAsync(cufe, "UBL", cancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(ubl))
                    return ubl;

                _logger.LogWarning(
                    "No UBL from GetDocumentFromDian for CUFE {Cufe} (NIT {Nit})",
                    cufe,
                    nit);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error calling GetDocumentFromDian for CUFE {Cufe}",
                    cufe);
                if (!_options.AllowStubFallback)
                    throw;
            }
        }
        else
        {
            _logger.LogWarning("FeDian:BaseUrl not set; using stub UBL store if allowed.");
        }

        if (!_options.AllowStubFallback)
            return null;

        return await _stub.GetUblXmlAsync(nit, cufe, cancellationToken).ConfigureAwait(false);
    }
}
