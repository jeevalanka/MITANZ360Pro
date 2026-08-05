using MITANZ360Pro.Web.Common;
using MITANZ360Pro.Web.Infrastructure.SharePoint;

namespace MITANZ360Pro.Web.Modules.Entities;

public interface IEntityService
{
    Task<EntityPagedResult<Entity>> GetPagedAsync(
        EntityFilter filter,
        CancellationToken cancellationToken = default);

    Task<Entity?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<bool> EntityIdExistsAsync(
        string entityId,
        int? excludeId = null,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<Entity>> CreateAsync(
        Entity entity,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<Entity>> UpdateAsync(
        Entity entity,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> ArchiveAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> RestoreAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<string> GenerateEntityIdAsync(CancellationToken cancellationToken = default);
}

public sealed class EntityService : IEntityService
{
    private readonly IEntityRepository _repository;
    private readonly IEntityTemplateService _templateService;
    private readonly IEntitySequenceService _sequenceService;
    private readonly IEntityActivityService _activityService;
    private readonly IEntityWorkflowService _workflowService;
    private readonly ILogger<EntityService> _logger;

    public EntityService(
        IEntityRepository repository,
        IEntityTemplateService templateService,
        IEntitySequenceService sequenceService,
        IEntityActivityService activityService,
        IEntityWorkflowService workflowService,
        ILogger<EntityService> logger)
    {
        _repository = repository;
        _templateService = templateService;
        _sequenceService = sequenceService;
        _activityService = activityService;
        _workflowService = workflowService;
        _logger = logger;
    }

    public Task<EntityPagedResult<Entity>> GetPagedAsync(
        EntityFilter filter,
        CancellationToken cancellationToken = default)
        => _repository.GetPagedAsync(filter, cancellationToken);

    public Task<Entity?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<bool> EntityIdExistsAsync(
        string entityId,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
        => _repository.ExistsAsync(entityId, excludeId, cancellationToken);

    public async Task<ServiceResult<Entity>> CreateAsync(
        Entity entity,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entity.EntityId))
        {
            return ServiceResult<Entity>.Failure("Entity ID is required.");
        }

        if (string.IsNullOrWhiteSpace(entity.Title))
        {
            return ServiceResult<Entity>.Failure("Title is required.");
        }

        if (string.IsNullOrWhiteSpace(entity.EntityType))
        {
            return ServiceResult<Entity>.Failure("Entity type is required.");
        }

        try
        {
            if (await _repository.ExistsAsync(entity.EntityId, cancellationToken: cancellationToken))
            {
                return ServiceResult<Entity>.Failure($"Entity ID '{entity.EntityId}' already exists.");
            }

            var template = await _templateService.GetTemplateAsync(entity.EntityType, cancellationToken);

            if (template != null)
            {
                var validation = _templateService.ValidateMetadata(template, entity.Metadata);

                if (!validation.IsSuccess)
                {
                    return ServiceResult<Entity>.Failure(validation.ErrorMessage!);
                }
            }

            var created = await _repository.CreateAsync(entity, cancellationToken);

            _logger.LogInformation("Entity created successfully {Id}", created.Id);

            await _activityService.LogAsync("Create", created, cancellationToken: cancellationToken);
            await _workflowService.RecordStatusChangeAsync(
                created,
                string.Empty,
                created.Status,
                cancellationToken);

            return ServiceResult<Entity>.Success(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create entity failed for {EntityId}", entity.EntityId);
            return ServiceResult<Entity>.Failure(GraphErrorMapper.Map(ex));
        }
    }

    public async Task<ServiceResult<Entity>> UpdateAsync(
        Entity entity,
        CancellationToken cancellationToken = default)
    {
        if (entity.Id <= 0)
        {
            return ServiceResult<Entity>.Failure("Invalid entity record.");
        }

        try
        {
            var existing = await _repository.GetByIdAsync(entity.Id, cancellationToken);

            if (existing == null)
            {
                return ServiceResult<Entity>.Failure("Entity not found.");
            }

            if (await _repository.ExistsAsync(entity.EntityId, entity.Id, cancellationToken))
            {
                return ServiceResult<Entity>.Failure($"Entity ID '{entity.EntityId}' already exists.");
            }

            var template = await _templateService.GetTemplateAsync(entity.EntityType, cancellationToken);

            if (template != null)
            {
                var validation = _templateService.ValidateMetadata(template, entity.Metadata);

                if (!validation.IsSuccess)
                {
                    return ServiceResult<Entity>.Failure(validation.ErrorMessage!);
                }
            }

            var previousStatus = existing.Status;
            var updated = await _repository.UpdateAsync(entity, cancellationToken);

            _logger.LogInformation("Entity updated successfully {Id}", updated.Id);

            await _activityService.LogAsync("Update", updated, cancellationToken: cancellationToken);

            if (!string.Equals(previousStatus, updated.Status, StringComparison.OrdinalIgnoreCase))
            {
                await _workflowService.RecordStatusChangeAsync(
                    updated,
                    previousStatus,
                    updated.Status,
                    cancellationToken);
            }

            return ServiceResult<Entity>.Success(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update entity failed for {Id}", entity.Id);
            return ServiceResult<Entity>.Failure(GraphErrorMapper.Map(ex));
        }
    }

    public async Task<ServiceResult> ArchiveAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id, cancellationToken);

            if (existing == null)
            {
                return ServiceResult.Failure("Entity not found.");
            }

            var previousStatus = existing.Status;

            await _repository.ArchiveAsync(id, cancellationToken);

            existing.Status = EntityStatuses.Archived;
            existing.IsActive = false;

            await _activityService.LogAsync("Archive", existing, cancellationToken: cancellationToken);
            await _workflowService.RecordStatusChangeAsync(
                existing,
                previousStatus,
                EntityStatuses.Archived,
                cancellationToken);

            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Archive entity failed for {Id}", id);
            return ServiceResult.Failure(GraphErrorMapper.Map(ex));
        }
    }

    public async Task<ServiceResult> RestoreAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id, cancellationToken);

            if (existing == null)
            {
                return ServiceResult.Failure("Entity not found.");
            }

            var previousStatus = existing.Status;

            await _repository.RestoreAsync(id, cancellationToken);

            existing.Status = EntityStatuses.Active;
            existing.IsActive = true;

            await _activityService.LogAsync("Restore", existing, cancellationToken: cancellationToken);
            await _workflowService.RecordStatusChangeAsync(
                existing,
                previousStatus,
                EntityStatuses.Active,
                cancellationToken);

            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore entity failed for {Id}", id);
            return ServiceResult.Failure(GraphErrorMapper.Map(ex));
        }
    }

    public Task<string> GenerateEntityIdAsync(CancellationToken cancellationToken = default)
        => _sequenceService.GenerateNextEntityIdAsync(cancellationToken);
}
