using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using MITANZ360Pro.Web.Models.Workflow;

namespace MITANZ360Pro.Web.Services.Workflow.Plugins;

/// <summary>
/// HTTP workflow plugin.
/// </summary>
public sealed class HttpPlugin : WorkflowPluginBase
{
    private readonly HttpClient _httpClient;

    public HttpPlugin(
        HttpClient httpClient,
        ILogger<HttpPlugin> logger)
        : base(logger)
    {
        _httpClient = httpClient;
    }

    public override string Type => "http";

    public override async Task ExecuteAsync(
        WorkflowContext context,
        WorkflowStep step,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(step);

        var url = GetSetting<string>(step, "url");

        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("HTTP URL is required.");

        var method = GetSetting<string>(step, "method")?.ToUpperInvariant() ?? "GET";

        object? result = method switch
        {
            "GET" => await _httpClient.GetFromJsonAsync<object>(
                url,
                cancellationToken),

            _ => throw new NotSupportedException($"HTTP method '{method}' is not supported.")
        };

        context.Set(step.Output, result);
    }
}