using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MITANZ360Pro.Web.Models.Workflow;

namespace MITANZ360Pro.Web.Services.Workflow.Plugins;

/// <summary>
/// SQL workflow plugin.
/// </summary>
public sealed class SqlPlugin : WorkflowPluginBase
{
    private readonly IConfiguration _configuration;

    public SqlPlugin(
        IConfiguration configuration,
        ILogger<SqlPlugin> logger)
        : base(logger)
    {
        _configuration = configuration;
    }

    public override string Type => "sql";

    public override async Task ExecuteAsync(
        WorkflowContext context,
        WorkflowStep step,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(step);

        var connectionName = GetSetting<string>(step, "connection") ?? "DefaultConnection";
        var sql = GetSetting<string>(step, "query");

        if (string.IsNullOrWhiteSpace(sql))
            throw new InvalidOperationException("SQL query is required.");

        var connectionString = _configuration.GetConnectionString(connectionName);

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Connection string '{connectionName}' was not found.");

        var table = new DataTable();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        table.Load(reader);

        context.Set(step.Output, table);
    }
}