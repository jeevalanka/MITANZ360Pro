using Microsoft.Extensions.Logging;
using MITANZ360Pro.Web.Models.Workflow;

namespace MITANZ360Pro.Web.Services.Workflow.Plugins;

/// <summary>
/// Email workflow plugin.
/// </summary>
public sealed class EmailPlugin : WorkflowPluginBase
{
    public EmailPlugin(
        ILogger<EmailPlugin> logger)
        : base(logger)
    {
    }

    public override string Type => "email";

    public override async Task ExecuteAsync(
        WorkflowContext context,
        WorkflowStep step,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(step);

        var to = GetSetting<string>(step, "to");
        var subject = GetSetting<string>(step, "subject");
        var body = GetSetting<string>(step, "body");

        if (string.IsNullOrWhiteSpace(to))
            throw new InvalidOperationException("Email recipient is required.");

        // TODO:
        // SMTP
        // Microsoft Graph
        // SendGrid
        // Exchange

        var result = new
        {
            To = to,
            Subject = subject,
            Status = "Pending",
            SentOn = DateTime.UtcNow
        };

        context.Set(step.Output, result);

        await Task.CompletedTask;
    }
}