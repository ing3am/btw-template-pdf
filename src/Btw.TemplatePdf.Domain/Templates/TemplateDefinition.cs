namespace Btw.TemplatePdf.Domain.Templates;

public sealed class PageMarginsMm
{
    public decimal Top { get; init; } = 5;
    public decimal Right { get; init; } = 5;
    public decimal Bottom { get; init; } = 5;
    public decimal Left { get; init; } = 5;
}

public sealed class PageSettings
{
    public string SizeId { get; init; } = "carta";
    public decimal WidthMm { get; init; } = 216;
    public decimal HeightMm { get; init; } = 279;
    public string Orientation { get; init; } = "vertical";
    public PageMarginsMm Margins { get; init; } = new();
    public string Background { get; init; } = "#ffffff";
}

public sealed class TemplateFeatures
{
    public bool SectorSalud { get; init; }
}

public sealed class TemplateAssetRef
{
    public required string Id { get; init; }
    public string Role { get; init; } = "image";
    public required string Mime { get; init; }
    public string? StorageKey { get; init; }
    public string? Name { get; init; }
}

/// <summary>
/// Published template configuration used to render PDFs.
/// Does not include invoice sample/runtime data.
/// </summary>
public sealed class TemplateDefinition
{
    public required Guid TemplateId { get; init; }
    public required string Nit { get; init; }
    public required Common.DocumentType DocumentType { get; init; }
    public int Version { get; init; } = 1;
    public Common.TemplateStatus Status { get; init; } = Common.TemplateStatus.Draft;
    public PageSettings Page { get; init; } = new();
    public TemplateFeatures Features { get; init; } = new();
    /// <summary>Opaque JSON array of visual-builder blocks (same shape as studio blocksJson).</summary>
    public required string BlocksJson { get; init; }
    /// <summary>Serialized HTML from the studio (with {{placeholders}}).</summary>
    public string Html { get; init; } = string.Empty;
    /// <summary>Serialized CSS from the studio.</summary>
    public string Css { get; init; } = string.Empty;
    public IReadOnlyList<TemplateAssetRef> Assets { get; init; } = Array.Empty<TemplateAssetRef>();
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
