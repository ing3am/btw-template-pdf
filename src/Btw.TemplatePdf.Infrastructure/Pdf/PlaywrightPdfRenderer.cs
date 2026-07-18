using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Invoices;
using Btw.TemplatePdf.Domain.Templates;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Btw.TemplatePdf.Infrastructure.Pdf;

/// <summary>
/// Renders bound HTML/CSS to PDF via Chromium (Microsoft Playwright, MIT).
/// </summary>
public sealed class PlaywrightPdfRenderer : IPdfRenderer
{
    private static readonly SemaphoreSlim BrowserLock = new(1, 1);
    private readonly ILogger<PlaywrightPdfRenderer> _logger;

    public PlaywrightPdfRenderer(ILogger<PlaywrightPdfRenderer> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> RenderAsync(
        TemplateDefinition template,
        InvoiceViewModel invoice,
        IReadOnlyDictionary<string, byte[]> assets,
        CancellationToken cancellationToken = default)
    {
        var html = HtmlTemplateBinder.Bind(
            template.Html,
            template.Css,
            invoice,
            assets);

        if (string.IsNullOrWhiteSpace(template.Html))
        {
            _logger.LogWarning(
                "Template {TemplateId} has empty Html; PDF will be mostly blank.",
                template.TemplateId);
        }

        await BrowserLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            await using var browser = await playwright.Chromium
                .LaunchAsync(new BrowserTypeLaunchOptions { Headless = true })
                .ConfigureAwait(false);
            var page = await browser.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(
                    html,
                    new PageSetContentOptions { WaitUntil = WaitUntilState.NetworkIdle })
                .ConfigureAwait(false);

            var widthMm = template.Page.WidthMm <= 0 ? 216 : template.Page.WidthMm;
            var heightMm = template.Page.HeightMm <= 0 ? 279 : template.Page.HeightMm;
            var margins = template.Page.Margins;

            return await page.PdfAsync(new PagePdfOptions
                {
                    Width = $"{widthMm.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm",
                    Height = $"{heightMm.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm",
                    PrintBackground = true,
                    Margin = new Margin
                    {
                        Top = $"{margins.Top.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm",
                        Right = $"{margins.Right.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm",
                        Bottom = $"{margins.Bottom.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm",
                        Left = $"{margins.Left.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm"
                    }
                })
                .ConfigureAwait(false);
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Playwright Chromium is not installed. Run: pwsh bin/Debug/net10.0/playwright.ps1 install chromium",
                ex);
        }
        finally
        {
            BrowserLock.Release();
        }
    }
}
