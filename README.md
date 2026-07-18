# Btw.TemplatePdf

.NET 10 modular API for template-driven FE PDF generation.

## Solution layout

| Project | Role |
|---|---|
| `Domain` | `TemplateDefinition`, `InvoiceViewModel`, ports (`ITemplateStore`, `IUblStore`, …) |
| `Application` | Use case `GeneratePdfByCufeUseCase` |
| `Infrastructure` | In-memory/stub adapters (replace with SQL, real UBL mapper, iText/HTML renderer) |
| `Api` | REST `POST /api/v1/pdf/by-cufe` |

## Run

```bash
dotnet run --project src/Btw.TemplatePdf.Api
```

Demo request: see `Btw.TemplatePdf.Api.http` (NIT `900000000`).

## Contract

Aligned with `btw-template-studio/docs/pdf-api-contract.md`.
