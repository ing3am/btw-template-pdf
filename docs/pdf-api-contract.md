# Same contract as btw-template-studio/docs/pdf-api-contract.md

## POST /api/v1/pdf/by-cufe

### Request
```json
{
  "nit": "900000000",
  "cufe": "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
  "documentType": "factura"
}
```

### Response 200
```json
{
  "nit": "900000000",
  "cufe": "…",
  "documentType": 0,
  "templateId": "11111111-1111-1111-1111-111111111111",
  "templateVersion": 1,
  "contentType": "application/pdf",
  "fileName": "FE-900000000-00000000.pdf",
  "pdfBase64": "JVBERi0x…"
}
```

Demo NIT seeded in memory: `900000000`.
