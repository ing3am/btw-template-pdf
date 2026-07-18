using System.Text;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Invoices;
using Btw.TemplatePdf.Domain.Templates;

namespace Btw.TemplatePdf.Infrastructure.Pdf;

/// <summary>
/// Placeholder renderer. Replace with blocksJson + InvoiceViewModel engine (HTML/iText).
/// </summary>
public sealed class StubPdfRenderer : IPdfRenderer
{
    public Task<byte[]> RenderAsync(
        TemplateDefinition template,
        InvoiceViewModel invoice,
        IReadOnlyDictionary<string, byte[]> assets,
        CancellationToken cancellationToken = default)
    {
        var numero = TryGet(invoice.Data, "documento", "numero") ?? "SIN-NUMERO";
        var content =
            $"BTW Template PDF\nNIT {invoice.Nit}\nCUFE {invoice.Cufe}\nDoc {numero}\nTemplate {template.TemplateId} v{template.Version}\nAssets {assets.Count}";
        return Task.FromResult(BuildSimplePdf(content));
    }

    private static string? TryGet(
        IReadOnlyDictionary<string, object?> data,
        string root,
        string key)
    {
        if (!data.TryGetValue(root, out var node) ||
            node is not IDictionary<string, object?> map)
            return null;
        return map.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static byte[] BuildSimplePdf(string text)
    {
        var escaped = text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var lines = escaped.Split('\n');
        var content = new StringBuilder();
        content.Append("BT /F1 10 Tf 50 750 Td ");
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) content.Append("0 -14 Td ");
            content.Append('(').Append(lines[i]).Append(") Tj ");
        }

        var stream = content.ToString();
        var objects = new List<string>
        {
            "1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n",
            "2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj\n",
            "3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources<< /Font<< /F1 5 0 R >> >> >>endobj\n",
            $"4 0 obj<< /Length {Encoding.ASCII.GetByteCount(stream)} >>stream\n{stream}\nendstream\nendobj\n",
            "5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n"
        };

        var body = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(body.ToString()));
            body.Append(obj);
        }

        var xrefPos = Encoding.ASCII.GetByteCount(body.ToString());
        body.Append($"xref\n0 {objects.Count + 1}\n");
        body.Append("0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
            body.Append($"{offsets[i]:D10} 00000 n \n");
        body.Append(
            $"trailer<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF");

        return Encoding.ASCII.GetBytes(body.ToString());
    }
}
