using Microsoft.AspNetCore.Components.Authorization;
using MITANZ360Pro.Web.Infrastructure.SharePoint;
using MITANZ360Pro.Web.Services;
using System.Security.Claims;

namespace MITANZ360Pro.Web.Modules.Entities;

public interface IEntityService
{
    Task<EntityPagedResult<Entity>> GetPagedAsync(
        EntityFilter filter,
        CancellationToken cancellationToken = default);

    Task<Entity?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Entity?> GetByEntityIdAsync(
        string entityId,
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

    Task<ServiceResult> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<bool> IsReferencedAsync(
        string entityId,
        int? excludeId = null,
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
    private readonly UserSessionService _userSession;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<EntityService> _logger;

    public EntityService(
        IEntityRepository repository,
        IEntityTemplateService templateService,
        IEntitySequenceService sequenceService,
        IEntityActivityService activityService,
        IEntityWorkflowService workflowService,
        UserSessionService userSession,
        AuthenticationStateProvider authStateProvider,
        ILogger<EntityService> logger)
    {
        _repository = repository;
        _templateService = templateService;
        _sequenceService = sequenceService;
        _activityService = activityService;
        _workflowService = workflowService;
        _userSession = userSession;
        _authStateProvider = authStateProvider;
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

    public Task<Entity?> GetByEntityIdAsync(
        string entityId,
        CancellationToken cancellationToken = default)
        => _repository.GetByEntityIdAsync(entityId, cancellationToken);

    public Task<bool> EntityIdExistsAsync(
        string entityId,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
        => _repository.ExistsAsync(entityId, excludeId, cancellationToken);

    public Task<bool> IsReferencedAsync(
        string entityId,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
        => _repository.IsReferencedAsync(entityId, excludeId, cancellationToken);

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

            await ApplyAppUserAuditAsync(entity, isCreate: true);

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
        if (entity.Id <= 0 && !string.IsNullOrWhiteSpace(entity.EntityId))
        {
            var byBusinessId = await _repository.GetByEntityIdAsync(entity.EntityId, cancellationToken);
            if (byBusinessId != null)
            {
                entity.Id = byBusinessId.Id;
            }
        }

        if (entity.Id <= 0)
        {
            return ServiceResult<Entity>.Failure(
                "Invalid entity record. Missing SharePoint item Id — reopen the record from the grid and try again.");
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

            // Preserve original app CreatedBy; always refresh ModifiedBy from app login.
            if (string.IsNullOrWhiteSpace(entity.CreatedBy))
            {
                entity.CreatedBy = existing.CreatedBy;
            }

            await ApplyAppUserAuditAsync(entity, isCreate: false);

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

    public async Task<ServiceResult> DeleteAsync(
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

            if (await _repository.IsReferencedAsync(existing.EntityId, existing.Id, cancellationToken))
            {
                return ServiceResult.Failure(
                    $"Cannot delete '{existing.EntityId}' because it is referenced by other records.");
            }

            await _repository.DeleteAsync(id, cancellationToken);
            await _activityService.LogAsync("Delete", existing, cancellationToken: cancellationToken);

            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete entity failed for {Id}", id);
            return ServiceResult.Failure(GraphErrorMapper.Map(ex));
        }
    }

    public Task<string> GenerateEntityIdAsync(CancellationToken cancellationToken = default)
        => _sequenceService.GenerateNextEntityIdAsync(cancellationToken);

    private async Task ApplyAppUserAuditAsync(Entity entity, bool isCreate)
    {
        var display = await ResolveAppUserDisplayAsync();

        if (isCreate || string.IsNullOrWhiteSpace(entity.CreatedBy))
        {
            entity.CreatedBy = display;
        }

        entity.ModifiedBy = display;
    }

    private async Task<string> ResolveAppUserDisplayAsync()
    {
        var session = _userSession.CurrentUser;

        if (!string.IsNullOrWhiteSpace(session.Email))
        {
            return session.Email;
        }

        if (!string.IsNullOrWhiteSpace(session.FullName))
        {
            return session.FullName;
        }

        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var principal = authState.User;

        if (principal.Identity?.IsAuthenticated == true)
        {
            var email = principal.FindFirst(ClaimTypes.Email)?.Value
                        ?? principal.FindFirst("email")?.Value
                        ?? principal.FindFirst("preferred_username")?.Value
                        ?? principal.Identity.Name;

            if (!string.IsNullOrWhiteSpace(email))
            {
                return email;
            }
        }

        return "unknown";
    }
}
