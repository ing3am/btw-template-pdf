using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Invoices;

namespace Btw.TemplatePdf.Infrastructure.Invoices;

/// <summary>
/// Stub adapter. Replace with a real DIAN UBL parser that fills catalog paths.
/// </summary>
public sealed class StubUblToViewModelMapper : IUblToViewModelMapper
{
    public InvoiceViewModel Map(string nit, string cufe, string ublXml)
    {
        _ = ublXml;
        var data = new Dictionary<string, object?>
        {
            ["documento"] = new Dictionary<string, object?>
            {
                ["tipo"] = "FACTURA ELECTRÓNICA DE VENTA",
                ["numero"] = "DEMO-1",
                ["prefijo"] = "DEMO",
                ["cufe"] = cufe,
                ["qrUrl"] =
                    $"https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey={cufe}",
                ["fechaGeneracion"] = "2026-07-18",
                ["horaGeneracion"] = "12:00:00-05:00",
                ["autorizacion"] = "00000000000000",
                ["rangoDesde"] = "1",
                ["rangoHasta"] = "1000",
                ["vigenciaInicio"] = "2026-01-01",
                ["vigenciaFin"] = "2026-12-31"
            },
            ["emisor"] = new Dictionary<string, object?>
            {
                ["razonSocial"] = "EMPRESA DEMO S.A.S.",
                ["nit"] = $"{nit}-1"
            },
            ["cliente"] = new Dictionary<string, object?>
            {
                ["nombre"] = "CLIENTE DE EJEMPLO S.A.S.",
                ["nit"] = "800000000-1"
            },
            ["pago"] = new Dictionary<string, object?>
            {
                ["forma"] = "Crédito",
                ["medio"] = "Transferencia"
            },
            ["items"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["linea"] = 1,
                    ["descripcion"] = "Servicio de ejemplo",
                    ["cantidad"] = 1,
                    ["total"] = 150000
                }
            },
            ["totales"] = new Dictionary<string, object?>
            {
                ["subtotal"] = 150000,
                ["iva"] = 0,
                ["ivaTarifa"] = "0%",
                ["impuestoConsumo"] = 0,
                ["impuestoConsumoTarifa"] = "0%",
                ["total"] = 150000
            },
            ["software"] = new Dictionary<string, object?>
            {
                ["fabricante"] = "Bythewave S.A.S.",
                ["fabricanteNit"] = "900665411",
                ["nombre"] = "BTW Facturación Electrónica",
                ["proveedor"] = "Bythewave S.A.S."
            }
        };

        return new InvoiceViewModel
        {
            Nit = nit,
            Cufe = cufe,
            Data = data
        };
    }
}
