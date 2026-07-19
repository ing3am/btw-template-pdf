using Btw.TemplatePdf.Application.Templates;
using Btw.TemplatePdf.Infrastructure.Assets;
using Btw.TemplatePdf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Btw.TemplatePdf.Infrastructure.Templates;

public sealed class PostgresTemplateCatalog : ITemplateCatalog
{
    private readonly TemplateDbContext _db;

    public PostgresTemplateCatalog(TemplateDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TemplateDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _db.Templates
            .AsNoTracking()
            .Include(t => t.Versions)
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return templates.Select(MapTemplate).ToList();
    }

    public async Task<TemplateBundleDto?> GetBundleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .AsNoTracking()
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return template is null ? null : MapBundle(template);
    }

    public async Task<TemplateDto> CreateAsync(
        CreateTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var entity = new TemplateEntity
        {
            Id = templateId,
            Name = request.Name.Trim(),
            DocumentType = string.IsNullOrWhiteSpace(request.DocumentType) ? "factura" : request.DocumentType,
            Status = "draft",
            CurrentVersionNumber = 1,
            Nit = request.Nit.Trim(),
            SectorSalud = request.SectorSalud,
            UpdatedAt = now,
            Versions =
            {
                new TemplateVersionEntity
                {
                    Id = versionId,
                    TemplateId = templateId,
                    VersionNumber = 1,
                    Html = request.Html ?? "",
                    Css = request.Css ?? "",
                    SchemaJson = request.SchemaJson ?? "{}",
                    SampleDataJson = request.SampleDataJson ?? "{}",
                    BlocksJson = request.BlocksJson ?? "[]",
                    PageJson = request.PageJson ?? "{}",
                    AssetsJson = await BrandAssetHydrator.HydrateAssetsJsonAsync(
                            _db,
                            request.AssetsJson,
                            "[]",
                            cancellationToken)
                        .ConfigureAwait(false),
                    CreatedAt = now,
                    Status = VersionStatuses.Draft,
                    IsPublished = false
                }
            }
        };

        _db.Templates.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapTemplate(entity);
    }

    public async Task<TemplateVersionDto> SaveDraftAsync(
        Guid id,
        SaveDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Template '{id}' was not found.");

        var tip = Tip(template);
        var now = DateTimeOffset.UtcNow;

        if (request.SectorSalud is bool sector)
            template.SectorSalud = sector;
        if (!string.IsNullOrWhiteSpace(request.Nit))
            template.Nit = request.Nit.Trim();

        // Published / used tips are immutable — fork a new draft.
        if (!VersionStatuses.IsDraft(tip.Status))
        {
            var assetsJson = await BrandAssetHydrator.HydrateAssetsJsonAsync(
                    _db,
                    request.AssetsJson,
                    tip.AssetsJson,
                    cancellationToken)
                .ConfigureAwait(false);

            var draft = new TemplateVersionEntity
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                VersionNumber = tip.VersionNumber + 1,
                Html = request.Html ?? tip.Html,
                Css = request.Css ?? tip.Css,
                SchemaJson = request.SchemaJson ?? tip.SchemaJson,
                SampleDataJson = request.SampleDataJson ?? tip.SampleDataJson,
                BlocksJson = request.BlocksJson ?? tip.BlocksJson,
                PageJson = request.PageJson ?? tip.PageJson,
                AssetsJson = assetsJson,
                CreatedAt = now,
                Status = VersionStatuses.Draft,
                IsPublished = false
            };
            template.Versions.Add(draft);
            _db.TemplateVersions.Add(draft);
            template.UpdatedAt = now;
            DatabaseInitializer.SyncTemplateFlags(template);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return MapVersion(draft);
        }

        // Upsert existing draft tip (same version number).
        var draftAssets = await BrandAssetHydrator.HydrateAssetsJsonAsync(
                _db,
                request.AssetsJson,
                tip.AssetsJson,
                cancellationToken)
            .ConfigureAwait(false);

        tip.Html = request.Html ?? tip.Html;
        tip.Css = request.Css ?? tip.Css;
        tip.SchemaJson = request.SchemaJson ?? tip.SchemaJson;
        tip.SampleDataJson = request.SampleDataJson ?? tip.SampleDataJson;
        tip.BlocksJson = request.BlocksJson ?? tip.BlocksJson;
        if (request.PageJson is not null)
            tip.PageJson = request.PageJson;
        tip.AssetsJson = draftAssets;
        tip.CreatedAt = now;
        tip.Status = VersionStatuses.Draft;
        tip.IsPublished = false;

        template.UpdatedAt = now;
        DatabaseInitializer.SyncTemplateFlags(template);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapVersion(tip);
    }

    public async Task<TemplateVersionDto> PublishAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Template '{id}' was not found.");

        var tip = Tip(template);
        var now = DateTimeOffset.UtcNow;

        // Already on a published tip — no empty clone.
        if (VersionStatuses.IsPublished(tip.Status))
        {
            DatabaseInitializer.SyncTemplateFlags(template);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return MapVersion(tip);
        }

        if (VersionStatuses.IsUsed(tip.Status))
            throw new InvalidOperationException("Cannot publish a used version. Save a new draft first.");

        foreach (var version in template.Versions)
        {
            if (VersionStatuses.IsPublished(version.Status) || version.IsPublished)
            {
                version.Status = VersionStatuses.Used;
                version.IsPublished = false;
            }
        }

        tip.Status = VersionStatuses.Published;
        tip.IsPublished = true;
        tip.CreatedAt = now;
        template.UpdatedAt = now;
        DatabaseInitializer.SyncTemplateFlags(template);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapVersion(tip);
    }

    public async Task DeleteDraftAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Template '{id}' was not found.");

        var tip = Tip(template);
        if (!VersionStatuses.IsDraft(tip.Status))
            throw new InvalidOperationException("Only a draft tip can be discarded.");

        if (template.Versions.Count == 1)
            throw new InvalidOperationException(
                "Cannot discard the only version. Delete the template instead, or publish first.");

        template.Versions.Remove(tip);
        _db.TemplateVersions.Remove(tip);
        template.UpdatedAt = DateTimeOffset.UtcNow;
        DatabaseInitializer.SyncTemplateFlags(template);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TemplateVersionEntity Tip(TemplateEntity template) =>
        template.Versions.OrderByDescending(v => v.VersionNumber).First();

    private static TemplateBundleDto MapBundle(TemplateEntity template) =>
        new(
            MapTemplate(template),
            template.Versions
                .OrderByDescending(v => v.VersionNumber)
                .Select(MapVersion)
                .ToList());

    private static TemplateDto MapTemplate(TemplateEntity t)
    {
        var tip = t.Versions.Count == 0
            ? null
            : t.Versions.OrderByDescending(v => v.VersionNumber).First();
        var published = t.Versions
            .Where(v => VersionStatuses.IsPublished(v.Status) || v.IsPublished)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();
        var hasDraft = tip is not null && VersionStatuses.IsDraft(tip.Status);

        return new TemplateDto(
            t.Id,
            t.Name,
            t.DocumentType,
            published is null ? "draft" : "published",
            tip?.VersionNumber ?? t.CurrentVersionNumber,
            t.UpdatedAt,
            t.Nit,
            t.SectorSalud,
            published?.VersionNumber ?? 0,
            hasDraft);
    }

    private static TemplateVersionDto MapVersion(TemplateVersionEntity v)
    {
        var status = string.IsNullOrWhiteSpace(v.Status)
            ? (v.IsPublished ? VersionStatuses.Published : VersionStatuses.Draft)
            : v.Status;
        return new(
            v.Id,
            v.TemplateId,
            v.VersionNumber,
            v.Html,
            v.Css,
            v.SchemaJson,
            v.SampleDataJson,
            v.BlocksJson,
            v.CreatedAt,
            VersionStatuses.IsPublished(status),
            BrandAssetHydrator.StripDataUrls(v.AssetsJson),
            status);
    }
}
