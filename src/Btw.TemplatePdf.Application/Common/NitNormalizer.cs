namespace Btw.TemplatePdf.Application.Common;

public static class NitNormalizer
{
    /// <summary>Keeps digits only so "900.000.000-1" and "900000000" match.</summary>
    public static string Normalize(string? nit)
    {
        if (string.IsNullOrWhiteSpace(nit)) return string.Empty;
        return new string(nit.Where(char.IsDigit).ToArray());
    }

    public static bool Matches(string? stored, string? candidate)
    {
        var a = Normalize(stored);
        var b = Normalize(candidate);
        return a.Length > 0 && a == b;
    }
}
