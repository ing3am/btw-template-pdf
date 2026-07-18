namespace Btw.TemplatePdf.Infrastructure.Auth;

/// <summary>
/// Holds the FE JWT forwarded from the studio (<c>Authorization: Bearer</c>).
/// Used when calling GetDocumentFromDian so TemplatePdf does not need its own FE login.
/// </summary>
public interface IFeBearerTokenAccessor
{
    string? Token { get; set; }
}

public sealed class FeBearerTokenAccessor : IFeBearerTokenAccessor
{
    public string? Token { get; set; }
}
