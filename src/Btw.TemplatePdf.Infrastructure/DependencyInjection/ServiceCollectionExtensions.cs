using Btw.TemplatePdf.Application.Pdf;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Infrastructure.Assets;
using Btw.TemplatePdf.Infrastructure.Invoices;
using Btw.TemplatePdf.Infrastructure.Pdf;
using Btw.TemplatePdf.Infrastructure.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace Btw.TemplatePdf.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTemplatePdfInfrastructure(
        this IServiceCollection services)
    {
        services.AddSingleton<ITemplateStore, InMemoryTemplateStore>();
        services.AddSingleton<IUblStore, InMemoryUblStore>();
        services.AddSingleton<IUblToViewModelMapper, StubUblToViewModelMapper>();
        services.AddSingleton<IAssetStore, NullAssetStore>();
        services.AddSingleton<IPdfRenderer, StubPdfRenderer>();
        services.AddScoped<GeneratePdfByCufeUseCase>();
        return services;
    }
}
