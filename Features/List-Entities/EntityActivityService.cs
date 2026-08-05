using Microsoft.Extensions.Options;
using MITANZ360Pro.Web.Infrastructure.SharePoint;

namespace MITANZ360Pro.Web.Modules.Entities;

public interface IEntityActivityService
{
    Task LogAsync(
        string action,
        Entity entity,
        string? details = null,
        CancellationToken cancellationToken = default);
}

public sealed class EntityActivityService : IEntityActivityService
{
    private readonly ISharePointListClient _sharePointClient;
    private readonly SharePointOptions _options;
    private readonly ILogger<EntityActivityService> _logger;

    public EntityActivityService(
        ISharePointListClient sharePointClient,
        IOptions<SharePointOptions> options,
        ILogger<EntityActivityService> logger)
    {
        _sharePointClient = sharePointClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        Entity entity,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Lists.Activities))
        {
            _logger.LogInformation(
                "Activity: {Action} EntityId={EntityId} Details={Details}",
                action,
                entity.EntityId,
                details);
            return;
        }

        try
        {
            var fields = new Dictionary<string, object>
            {
                ["Title"] = $"{action}: {entity.EntityId}",
                ["Action"] = action,
                ["EntityId"] = entity.EntityId,
                ["EntityType"] = entity.EntityType,
                ["Details"] = details ?? string.Empty,
                ["Status"] = entity.Status
            };

            await _sharePointClient.CreateItemAsync(
                _options.SiteId,
                _options.Lists.Activities,
                fields,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write activity log for {EntityId}", entity.EntityId);
        }
    }
}
