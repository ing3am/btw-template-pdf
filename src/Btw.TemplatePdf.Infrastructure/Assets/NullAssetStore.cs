using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Templates;

namespace Btw.TemplatePdf.Infrastructure.Assets;

public sealed class NullAssetStore : IAssetStore
{
    public Task<IReadOnlyDictionary<string, byte[]>> ResolveAsync(
        IEnumerable<TemplateAssetRef> assets,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, byte[]>>(
            new Dictionary<string, byte[]>());
}
