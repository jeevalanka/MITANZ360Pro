using Microsoft.Extensions.Options;
using MITANZ360Pro.Web.Infrastructure.SharePoint;

namespace MITANZ360Pro.Web.Modules.Entities;

public interface IEntityWorkflowService
{
    Task RecordStatusChangeAsync(
        Entity entity,
        string previousStatus,
        string newStatus,
        CancellationToken cancellationToken = default);
}

public sealed class EntityWorkflowService : IEntityWorkflowService
{
    private readonly ISharePointListClient _sharePointClient;
    private readonly SharePointOptions _options;
    private readonly ILogger<EntityWorkflowService> _logger;

    public EntityWorkflowService(
        ISharePointListClient sharePointClient,
        IOptions<SharePointOptions> options,
        ILogger<EntityWorkflowService> logger)
    {
        _sharePointClient = sharePointClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RecordStatusChangeAsync(
        Entity entity,
        string previousStatus,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Lists.WorkflowHistory))
        {
            _logger.LogInformation(
                "WorkflowHistory: {EntityId} {PreviousStatus} -> {NewStatus}",
                entity.EntityId,
                previousStatus,
                newStatus);
            return;
        }

        if (string.Equals(previousStatus, newStatus, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var fields = new Dictionary<string, object>
            {
                ["Title"] = $"{entity.EntityId} status change",
                ["EntityId"] = entity.EntityId,
                ["EntityType"] = entity.EntityType,
                ["FromStatus"] = previousStatus,
                ["ToStatus"] = newStatus,
                ["ChangedAt"] = DateTime.UtcNow.ToString("O")
            };

            await _sharePointClient.CreateItemAsync(
                _options.SiteId,
                _options.Lists.WorkflowHistory,
                fields,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write workflow history for {EntityId}", entity.EntityId);
        }
    }
}
