using Microsoft.Extensions.Logging;
using MITANZ360Pro.Web.Models.Workflow;

namespace MITANZ360Pro.Web.Services.Workflow.Plugins;

/// <summary>
/// SharePoint workflow plugin.
/// </summary>
public sealed class SharePointPlugin : WorkflowPluginBase
{
    public SharePointPlugin(
        ILogger<SharePointPlugin> logger)
        : base(logger)
    {
    }

    public override string Type => "sharepoint";

    public override async Task ExecuteAsync(
        WorkflowContext context,
        WorkflowStep step,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(step);

        var siteUrl = GetSetting<string>(step, "siteUrl");
        var listName = GetSetting<string>(step, "listName");
        var operation = GetSetting<string>(step, "operation") ?? "read";

        if (string.IsNullOrWhiteSpace(siteUrl))
            throw new InvalidOperationException("SharePoint Site URL is required.");

        if (string.IsNullOrWhiteSpace(listName))
            throw new InvalidOperationException("SharePoint List Name is required.");

        // TODO:
        // Microsoft Graph implementation
        // Read
        // Create
        // Update
        // Delete

        var result = new
        {
            SiteUrl = siteUrl,
            ListName = listName,
            Operation = operation,
            Status = "Pending",
            ExecutedOn = DateTime.UtcNow
        };

        context.Set(step.Output, result);

        await Task.CompletedTask;
    }
}