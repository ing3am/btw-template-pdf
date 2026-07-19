namespace Btw.TemplatePdf.Application.Tests;

/// <summary>
/// Guards against regressing the EF translation bug where
/// <c>VersionStatuses.IsPublished(...)</c> was used inside an IQueryable
/// (InvalidOperationException → 500 on POST /pdf/by-cufe without templateId).
/// </summary>
public sealed class PostgresTemplateStoreEfTranslatabilityTests
{
    [Fact]
    public void GetPublishedAsync_IQueryable_filter_is_ef_translatable()
    {
        var source = File.ReadAllText(ResolveStorePath());

        Assert.Contains(
            "t.Versions.Any(v => v.IsPublished || v.Status == VersionStatuses.Published)",
            source,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "t.Versions.Any(v => VersionStatuses.IsPublished",
            source,
            StringComparison.Ordinal);
    }

    private static string ResolveStorePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "src",
                "Btw.TemplatePdf.Infrastructure",
                "Templates",
                "PostgresTemplateStore.cs");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate PostgresTemplateStore.cs from test base directory.");
    }
}
