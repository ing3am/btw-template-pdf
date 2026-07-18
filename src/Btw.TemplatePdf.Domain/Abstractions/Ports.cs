using Btw.TemplatePdf.Domain.Common;
using Btw.TemplatePdf.Domain.Invoices;
using Btw.TemplatePdf.Domain.Templates;

namespace Btw.TemplatePdf.Domain.Abstractions;

public interface ITemplateStore
{
    Task<TemplateDefinition?> GetPublishedAsync(
        string nit,
        DocumentType documentType,
        CancellationToken cancellationToken = default);
}

public interface IUblStore
{
    /// <summary>Returns raw UBL XML for the invoice, or null if missing.</summary>
    Task<string?> GetUblXmlAsync(
        string nit,
        string cufe,
        CancellationToken cancellationToken = default);
}

public interface IUblToViewModelMapper
{
    InvoiceViewModel Map(string nit, string cufe, string ublXml);
}

public interface IAssetStore
{
    Task<IReadOnlyDictionary<string, byte[]>> ResolveAsync(
        IEnumerable<TemplateAssetRef> assets,
        CancellationToken cancellationToken = default);
}

public interface IPdfRenderer
{
    Task<byte[]> RenderAsync(
        TemplateDefinition template,
        InvoiceViewModel invoice,
        IReadOnlyDictionary<string, byte[]> assets,
        CancellationToken cancellationToken = default);
}
