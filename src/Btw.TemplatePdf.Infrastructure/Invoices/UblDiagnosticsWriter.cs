using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Btw.TemplatePdf.Infrastructure.Invoices;

/// <summary>
/// Writes UBL fetch/map diagnostics to a text file under <c>logs/ubl/</c>
/// so they can be copied from disk (not only console).
/// </summary>
public sealed class UblDiagnosticsWriter
{
    private static readonly string[] CatalogPaths =
    [
        "documento.tipo",
        "documento.numero",
        "documento.prefijo",
        "documento.autorizacion",
        "documento.rangoDesde",
        "documento.rangoHasta",
        "documento.vigenciaInicio",
        "documento.vigenciaFin",
        "documento.fechaGeneracion",
        "documento.horaGeneracion",
        "documento.fechaVencimiento",
        "documento.moneda",
        "documento.cufe",
        "documento.qrUrl",
        "emisor.razonSocial",
        "emisor.nit",
        "emisor.dv",
        "emisor.telefono",
        "emisor.direccion",
        "emisor.ciudad",
        "emisor.departamento",
        "emisor.email",
        "cliente.nombre",
        "cliente.nit",
        "cliente.telefono",
        "cliente.direccion",
        "cliente.ciudad",
        "cliente.departamento",
        "cliente.email",
        "cliente.pais",
        "factura.fecha",
        "factura.fechaVencimiento",
        "factura.formaPago",
        "factura.medioPago",
        "factura.nroPedido",
        "pago.forma",
        "pago.medio",
        "pago.fechaVencimiento",
        "observaciones",
        "totales.subtotal",
        "totales.iva",
        "totales.total",
        "totales.descuento",
        "totales.totalItems",
        "software.nombre",
        "software.fabricante",
        "software.fabricanteNit"
    ];

    private static readonly string[] InterestingUblLocalNames =
    [
        "ID", "UUID", "IssueDate", "IssueTime", "DueDate", "InvoiceTypeCode",
        "DocumentCurrencyCode", "RegistrationName", "CompanyID", "Telephone",
        "ElectronicMail", "Line", "CityName", "CountrySubentity", "Description",
        "InvoicedQuantity", "PriceAmount", "LineExtensionAmount", "TaxAmount",
        "PayableAmount", "TaxExclusiveAmount", "PaymentMeansCode", "PaymentDueDate",
        "InvoiceAuthorization", "StartDate", "EndDate", "From", "To",
        "SoftwareName", "ProviderID", "Note", "OrderReference"
    ];

    private readonly string _logDir;
    private readonly ILogger<UblDiagnosticsWriter> _logger;

    public UblDiagnosticsWriter(IHostEnvironment env, ILogger<UblDiagnosticsWriter> logger)
    {
        _logger = logger;
        _logDir = Path.Combine(env.ContentRootPath, "logs", "ubl");
        Directory.CreateDirectory(_logDir);
    }

    public string WriteFetchReport(
        string nit,
        string cufe,
        string ublXml,
        string source)
    {
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var shortCufe = cufe.Length <= 12 ? cufe : cufe[..12];
        var baseName = $"ubl-{stamp}-{shortCufe}";
        var reportPath = Path.Combine(_logDir, $"{baseName}.txt");
        var xmlPath = Path.Combine(_logDir, $"{baseName}.xml");
        var rawPath = Path.Combine(_logDir, $"{baseName}.raw.txt");

        var normalizedXml = TryNormalizeUblXml(ublXml, out var normalizeNote);
        if (normalizedXml is not null)
        {
            File.WriteAllText(xmlPath, normalizedXml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        else
        {
            // Do NOT write invalid content as .xml (browsers show "Start tag expected").
            File.WriteAllText(rawPath, ublXml ?? string.Empty, Encoding.UTF8);
            xmlPath = rawPath;
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== UBL FETCH DIAGNOSTIC ===");
        sb.AppendLine($"timestamp: {DateTimeOffset.Now:O}");
        sb.AppendLine($"source:    {source}");
        sb.AppendLine($"nit:       {nit}");
        sb.AppendLine($"cufe:      {cufe}");
        sb.AppendLine($"xmlLength: {(ublXml ?? string.Empty).Length}");
        sb.AppendLine($"xmlFile:   {xmlPath}");
        if (!string.IsNullOrWhiteSpace(normalizeNote))
            sb.AppendLine($"xmlNote:   {normalizeNote}");
        if (normalizedXml is null)
        {
            sb.AppendLine("xmlStatus: NOT written as .xml (invalid for browsers)");
            sb.AppendLine("tip:       Open the .raw.txt sibling, or the .txt report — not a fake .xml");
        }
        else
        {
            sb.AppendLine("xmlStatus: valid XML written");
        }
        sb.AppendLine();

        string rootName = "?";
        var present = new List<string>();
        var missing = new List<string>();
        var parseInput = normalizedXml ?? ublXml ?? string.Empty;
        try
        {
            var doc = XDocument.Parse(parseInput, LoadOptions.PreserveWhitespace);
            rootName = doc.Root?.Name.LocalName ?? "?";
            var localNames = new HashSet<string>(
                doc.Descendants().Select(e => e.Name.LocalName),
                StringComparer.OrdinalIgnoreCase);

            foreach (var name in InterestingUblLocalNames)
            {
                if (localNames.Contains(name))
                    present.Add(name);
                else
                    missing.Add(name);
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"XML PARSE ERROR: {ex.Message}");
            sb.AppendLine("Open the .raw.txt sibling if .xml was not written.");
        }

        sb.AppendLine($"root: {rootName}");
        sb.AppendLine();
        sb.AppendLine("--- Tags present in UBL ---");
        sb.AppendLine(present.Count == 0 ? "(none)" : string.Join(", ", present));
        sb.AppendLine();
        sb.AppendLine("--- Tags missing in UBL (of catalog checklist) ---");
        sb.AppendLine(missing.Count == 0 ? "(none)" : string.Join(", ", missing));
        sb.AppendLine();
        sb.AppendLine("--- How to read ---");
        sb.AppendLine("• Tag MISSING + path EMPTY  => field is not in the UBL");
        sb.AppendLine("• Tag PRESENT + path EMPTY  => UBL has it, mapper is wrong/incomplete");
        sb.AppendLine("• source=StubFallback       => not real DIAN UBL");
        sb.AppendLine();
        sb.AppendLine("(Mapping section is appended after DianUblToViewModelMapper runs.)");
        sb.AppendLine();

        File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
        _logger.LogInformation("UBL diagnostic file written: {Path}", reportPath);
        return reportPath;
    }

    public void AppendMappingReport(
        string reportPath,
        string nit,
        string cufe,
        IReadOnlyDictionary<string, object?> data)
    {
        if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath))
        {
            reportPath = WriteFetchReport(nit, cufe, "<!-- mapping-only -->", "MappingOnly");
        }

        using var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(data));
        var root = jsonDoc.RootElement;

        var filled = new List<string>();
        var empty = new List<string>();
        var samples = new StringBuilder();

        foreach (var path in CatalogPaths)
        {
            if (!TryGetPath(root, path, out var value) || IsEmpty(value))
            {
                empty.Add(path);
                continue;
            }

            filled.Add(path);
            samples.AppendLine($"  {path} = {Truncate(ValueToString(value), 120)}");
        }

        var itemCount = 0;
        if (TryGetPath(root, "items", out var items) && items.ValueKind == JsonValueKind.Array)
            itemCount = items.GetArrayLength();

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("=== UBL MAP DIAGNOSTIC ===");
        sb.AppendLine($"timestamp:  {DateTimeOffset.Now:O}");
        sb.AppendLine($"nit:        {nit}");
        sb.AppendLine($"cufe:       {cufe}");
        sb.AppendLine($"items:      {itemCount}");
        sb.AppendLine($"filled:     {filled.Count}");
        sb.AppendLine($"empty:      {empty.Count}");
        sb.AppendLine();
        sb.AppendLine("--- Filled catalog paths ---");
        sb.AppendLine(filled.Count == 0 ? "(none)" : string.Join(", ", filled));
        sb.AppendLine();
        sb.AppendLine("--- Empty catalog paths ---");
        sb.AppendLine(empty.Count == 0 ? "(none)" : string.Join(", ", empty));
        sb.AppendLine();
        sb.AppendLine("--- Samples (filled values) ---");
        sb.Append(samples.Length == 0 ? "(none)\r\n" : samples.ToString());

        File.AppendAllText(reportPath, sb.ToString(), Encoding.UTF8);
        _logger.LogInformation("UBL mapping diagnostic appended: {Path}", reportPath);
    }

    private static bool TryGetPath(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        foreach (var key in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out value))
            {
                value = default;
                return false;
            }
        }

        return true;
    }

    private static bool IsEmpty(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => true,
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Array => value.GetArrayLength() == 0,
            JsonValueKind.Object => !value.EnumerateObject().Any(),
            _ => false
        };

    private static string ValueToString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => $"[{value.GetArrayLength()} items]",
            _ => value.ToString()
        };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    /// <summary>
    /// Ensures we only write a browser-openable .xml when content is real XML
    /// (handles accidental Base64 payloads and BOM).
    /// </summary>
    private static string? TryNormalizeUblXml(string? raw, out string note)
    {
        note = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            note = "empty payload";
            return null;
        }

        var text = raw.Trim().TrimStart('\uFEFF');
        if (text.StartsWith('<'))
        {
            try
            {
                XDocument.Parse(text, LoadOptions.PreserveWhitespace);
                return text;
            }
            catch (Exception ex)
            {
                note = $"starts with '<' but invalid XML: {ex.Message}";
                return null;
            }
        }

        // Maybe FE left Base64 (not yet decoded).
        try
        {
            var bytes = Convert.FromBase64String(text);
            if (bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B)
            {
                note = "payload is a ZIP (Base64); open with a zip tool — not plain XML";
                return null;
            }

            var decoded = Encoding.UTF8.GetString(bytes).Trim().TrimStart('\uFEFF');
            if (decoded.StartsWith('<'))
            {
                XDocument.Parse(decoded, LoadOptions.PreserveWhitespace);
                note = "decoded from Base64 to XML";
                return decoded;
            }

            note = "Base64 decoded but result is not XML";
            return null;
        }
        catch
        {
            note = "payload is not XML and not valid Base64 — see .raw.txt";
            return null;
        }
    }
}
