using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using MITANZ360Pro.Web.Models.Workflow;

namespace MITANZ360Pro.Web.Services.Workflow.Plugins;

/// <summary>
/// Website scraping plugin.
/// </summary>
public sealed class ScrapePlugin : WorkflowPluginBase
{
    public ScrapePlugin(ILogger<ScrapePlugin> logger)
        : base(logger)
    {
    }

    public override string Type => "scrape";

    public override async Task ExecuteAsync(
        WorkflowContext context,
        WorkflowStep step,
        CancellationToken cancellationToken = default)
    {
        var url = GetSetting<string>(step, "url");

        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Scrape URL is required.");

        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions
            {
                Headless = true
            });

        var page = await browser.NewPageAsync();

        await page.GotoAsync(
            url,
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

        var title = await page.TitleAsync();

        var html = await page.ContentAsync();

        var text = await page.Locator("body").InnerTextAsync();

        context.Set(step.Output, new
        {
            Url = url,
            Title = title,
            Text = text,
            ScrapedOn = DateTime.UtcNow
        });
    }
}