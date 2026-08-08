using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using MITANZ360Pro.Web.Infrastructure.SharePoint;

namespace MITANZ360Pro.Web.Modules.Entities;

/// <summary>
/// Startup validation for the SharePoint Entities list used by this feature.
/// </summary>
public sealed class EntityListValidator
{
    private static readonly string[] RequiredIndexedFields =
    [
        EntityFields.EntityId,
        EntityFields.EntityType,
        EntityFields.Status,
        EntityFields.IsActive
    ];

    private readonly GraphServiceClient _graphClient;
    private readonly SharePointOptions _options;
    private readonly ILogger<EntityListValidator> _logger;

    public EntityListValidator(
        GraphServiceClient graphClient,
        IOptions<SharePointOptions> options,
        ILogger<EntityListValidator> logger)
    {
        _graphClient = graphClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ValidateEntityListAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Lists.Entities))
        {
            _logger.LogWarning("SharePoint Entities list id is not configured.");
            return;
        }

        try
        {
            var columns = await _graphClient
                .Sites[_options.SiteId]
                .Lists[_options.Lists.Entities]
                .Columns
                .GetAsync(cancellationToken: cancellationToken);

            var indexed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var column in columns?.Value ?? [])
            {
                if (column.Name != null && column.Indexed == true)
                {
                    indexed.Add(column.Name);
                }
            }

            foreach (var field in RequiredIndexedFields)
            {
                if (!indexed.Contains(field))
                {
                    _logger.LogWarning("Required index missing: {Field}", field);
                }
            }

            _logger.LogInformation(
                "SharePoint Entities list validation completed. Indexed fields: {Count}",
                indexed.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to validate SharePoint Entities list indexes.");
        }
    }
}
