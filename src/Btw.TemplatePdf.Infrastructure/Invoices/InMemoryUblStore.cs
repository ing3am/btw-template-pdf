using Btw.TemplatePdf.Domain.Abstractions;

namespace Btw.TemplatePdf.Infrastructure.Invoices;

public sealed class InMemoryUblStore : IUblStore
{
    public const string DemoCufe =
        "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";

    public Task<string?> GetUblXmlAsync(
        string nit,
        string cufe,
        CancellationToken cancellationToken = default)
    {
        if (nit != "900000000")
            return Task.FromResult<string?>(null);

        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Invoice>
              <ID>DEMO-1</ID>
              <UUID>{cufe}</UUID>
              <IssueDate>2026-07-18</IssueDate>
              <IssueTime>12:00:00-05:00</IssueTime>
              <Nit>{nit}</Nit>
            </Invoice>
            """;
        return Task.FromResult<string?>(xml);
    }
}
