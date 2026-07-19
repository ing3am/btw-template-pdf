namespace Btw.TemplatePdf.Infrastructure.Templates;

/// <summary>Lifecycle of a template version snapshot.</summary>
public static class VersionStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Used = "used";

    public static bool IsDraft(string? status) =>
        string.Equals(status, Draft, StringComparison.OrdinalIgnoreCase);

    public static bool IsPublished(string? status) =>
        string.Equals(status, Published, StringComparison.OrdinalIgnoreCase);

    public static bool IsUsed(string? status) =>
        string.Equals(status, Used, StringComparison.OrdinalIgnoreCase);
}
