using System.Text.Json;
using System.Text.RegularExpressions;

using MITANZ360Pro.Web.Common;

namespace MITANZ360Pro.Web.Modules.Entities;

public interface IEntityTemplateService
{
    Task<IReadOnlyList<EntityTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    Task<EntityTemplate?> GetTemplateAsync(string entityType, CancellationToken cancellationToken = default);

    ServiceResult ValidateMetadata(EntityTemplate template, Dictionary<string, object?> metadata);
}

public sealed class EntityTemplateService : IEntityTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EntityTemplateService> _logger;

    public EntityTemplateService(
        IWebHostEnvironment environment,
        ILogger<EntityTemplateService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EntityTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(_environment.WebRootPath, "Data", "EntityTemplates");

        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Entity template directory not found: {Path}", directory);
            return [];
        }

        var templates = new List<EntityTemplate>();

        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var stream = File.OpenRead(file);
                var template = await JsonSerializer.DeserializeAsync<EntityTemplate>(stream, JsonOptions, cancellationToken);

                if (template != null && !string.IsNullOrWhiteSpace(template.EntityType))
                {
                    templates.Add(template);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load entity template {File}", file);
            }
        }

        return templates.OrderBy(x => x.DisplayName).ToList();
    }

    public async Task<EntityTemplate?> GetTemplateAsync(string entityType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return null;
        }

        var templates = await GetTemplatesAsync(cancellationToken);
        return templates.FirstOrDefault(x =>
            x.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase));
    }

    public ServiceResult ValidateMetadata(EntityTemplate template, Dictionary<string, object?> metadata)
    {
        var allowed = template.Fields
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in metadata.Keys)
        {
            if (!allowed.Contains(key))
            {
                return ServiceResult.Failure($"Unknown metadata field '{key}' for template '{template.EntityType}'.");
            }
        }

        foreach (var field in template.Fields)
        {
            metadata.TryGetValue(field.Name, out var value);
            var text = value?.ToString();

            if (field.Required && string.IsNullOrWhiteSpace(text))
            {
                return ServiceResult.Failure($"{field.Label} is required.");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (field.MaxLength.HasValue && text.Length > field.MaxLength.Value)
            {
                return ServiceResult.Failure($"{field.Label} exceeds maximum length of {field.MaxLength.Value}.");
            }

            if (!string.IsNullOrWhiteSpace(field.Pattern) &&
                !Regex.IsMatch(text, field.Pattern, RegexOptions.IgnoreCase))
            {
                return ServiceResult.Failure($"{field.Label} has an invalid format.");
            }

            if (field.Options is { Count: > 0 } &&
                !field.Options.Any(x => x.Equals(text, StringComparison.OrdinalIgnoreCase)))
            {
                return ServiceResult.Failure($"{field.Label} must be one of the allowed values.");
            }
        }

        return ServiceResult.Success();
    }
}
