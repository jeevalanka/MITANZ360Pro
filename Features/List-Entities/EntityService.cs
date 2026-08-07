using Microsoft.AspNetCore.Components.Authorization;
using MITANZ360Pro.Web.Infrastructure.SharePoint;
using MITANZ360Pro.Web.Services;
using System.Security.Claims;
using System.Security.Cryptography;

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
        string? activityDetails = null,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<Entity>> UpdateAsync(
        Entity entity,
        string? activityDetails = null,
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

    Task<string> GenerateStudentNumberAsync(CancellationToken cancellationToken = default);

    Task<Entity?> FindStudentByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<Entity?> ResolveReferralPartnerAsync(
        string? referralCode,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<StudentVisaSaveResult>> UpsertStudentVisaRegistrationAsync(
        Dictionary<string, object?> metadata,
        StudentVisaSaveContext context,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<Entity>> VerifyStudentEmailAsync(
        string token,
        StudentVisaSaveContext? context = null,
        CancellationToken cancellationToken = default);
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
        string? activityDetails = null,
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
                SeedRequiredDocumentsFromTemplate(entity, template);

                var validation = _templateService.ValidateMetadata(template, entity.Metadata);

                if (!validation.IsSuccess)
                {
                    return ServiceResult<Entity>.Failure(validation.ErrorMessage!);
                }
            }

            var created = await _repository.CreateAsync(entity, cancellationToken);

            _logger.LogInformation("Entity created successfully {Id}", created.Id);

            await _activityService.LogAsync("Create", created, activityDetails, cancellationToken);
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
        string? activityDetails = null,
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

            await _activityService.LogAsync("Update", updated, activityDetails, cancellationToken);

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

    public Task<string> GenerateStudentNumberAsync(CancellationToken cancellationToken = default)
        => _sequenceService.GenerateNextStudentNumberAsync(cancellationToken);

    public Task<Entity?> FindStudentByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
        => _repository.FindStudentByEmailAsync(email, cancellationToken);

    public async Task<Entity?> ResolveReferralPartnerAsync(
        string? referralCode,
        CancellationToken cancellationToken = default)
    {
        var code = ReferralCodeHelper.Normalize(referralCode);
        if (code == null)
        {
            return null;
        }

        return await _repository.FindReferralPartnerByCodeAsync(code, cancellationToken);
    }

    public async Task<ServiceResult<StudentVisaSaveResult>> UpsertStudentVisaRegistrationAsync(
        Dictionary<string, object?> metadata,
        StudentVisaSaveContext context,
        CancellationToken cancellationToken = default)
    {
        var email = StudentVisaMetadataHelper.GetString(metadata, StudentVisaMetadataKeys.Email).Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return ServiceResult<StudentVisaSaveResult>.Failure("Email Address is required.");
        }

        var firstName = StudentVisaMetadataHelper.GetString(metadata, StudentVisaMetadataKeys.FirstName).Trim();
        var lastName = StudentVisaMetadataHelper.GetString(metadata, StudentVisaMetadataKeys.LastName).Trim();
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            return ServiceResult<StudentVisaSaveResult>.Failure("First Name and Last Name are required.");
        }

        try
        {
            var existing = await _repository.FindStudentByEmailAsync(email, cancellationToken);
            var wasCreated = existing == null;

            var entity = existing ?? new Entity
            {
                EntityType = "Student",
                Status = EntityStatuses.Draft,
                IsActive = true
            };

            // Key fields: EntityType always Student; Email / EntityId / ReferralCode locked once set.
            entity.EntityType = "Student";
            entity.Title = $"{firstName} {lastName}".Trim();

            if (wasCreated)
            {
                entity.EntityId = await _sequenceService.GenerateNextStudentNumberAsync(cancellationToken);
                entity.Status = EntityStatuses.Draft;
                entity.IsActive = true;
                entity.CreatedBy = email;
                entity.ModifiedBy = email;
            }
            else
            {
                // Preserve key fields from existing record
                entity.ModifiedBy = email;

                var existingEmail = StudentVisaMetadataHelper.GetString(existing!.Metadata, StudentVisaMetadataKeys.Email);
                if (!string.IsNullOrWhiteSpace(existingEmail))
                {
                    metadata[StudentVisaMetadataKeys.Email] = existingEmail;
                }

                var existingReferral = StudentVisaMetadataHelper.GetString(existing.Metadata, StudentVisaMetadataKeys.ReferralCode);
                if (!string.IsNullOrWhiteSpace(existingReferral))
                {
                    metadata[StudentVisaMetadataKeys.ReferralCode] = existingReferral;
                }

                // Preserve verification state unless re-issuing token below
                var existingVerified = StudentVisaMetadataHelper.GetString(existing.Metadata, StudentVisaMetadataKeys.EmailVerified);
                var existingVerifiedDate = StudentVisaMetadataHelper.GetString(existing.Metadata, StudentVisaMetadataKeys.EmailVerifiedDate);
                if (!string.IsNullOrWhiteSpace(existingVerified))
                {
                    metadata[StudentVisaMetadataKeys.EmailVerified] = existingVerified;
                }

                if (!string.IsNullOrWhiteSpace(existingVerifiedDate))
                {
                    metadata[StudentVisaMetadataKeys.EmailVerifiedDate] = existingVerifiedDate;
                }

                // Carry forward reserved keys except verification token (rotated on each save)
                foreach (var kv in existing.Metadata)
                {
                    if (EntityMapper.IsReservedMetaKey(kv.Key) &&
                        !string.Equals(kv.Key, EntityFields.EmailVerifyTokenMeta, StringComparison.Ordinal))
                    {
                        metadata[kv.Key] = kv.Value;
                    }
                }
            }

            // Normalize referral on create (or when blank on existing)
            var incomingReferral = ReferralCodeHelper.Normalize(
                StudentVisaMetadataHelper.GetString(metadata, StudentVisaMetadataKeys.ReferralCode));
            var currentReferral = StudentVisaMetadataHelper.GetString(metadata, StudentVisaMetadataKeys.ReferralCode);
            if (string.IsNullOrWhiteSpace(currentReferral) && incomingReferral != null)
            {
                metadata[StudentVisaMetadataKeys.ReferralCode] = incomingReferral;
            }
            else if (!string.IsNullOrWhiteSpace(currentReferral))
            {
                var normalizedExisting = ReferralCodeHelper.Normalize(currentReferral);
                if (normalizedExisting != null)
                {
                    metadata[StudentVisaMetadataKeys.ReferralCode] = normalizedExisting;
                }
            }

            var referralForPartner = StudentVisaMetadataHelper.GetString(metadata, StudentVisaMetadataKeys.ReferralCode);
            if (!string.IsNullOrWhiteSpace(referralForPartner))
            {
                var partner = await _repository.FindReferralPartnerByCodeAsync(referralForPartner, cancellationToken);
                if (partner != null)
                {
                    var partnerName = StudentVisaMetadataHelper.GetString(partner.Metadata, "CompanyName");
                    if (string.IsNullOrWhiteSpace(partnerName))
                    {
                        partnerName = partner.Title;
                    }

                    metadata[StudentVisaMetadataKeys.ReferralPartner] = partnerName;
                }
            }

            if (!metadata.ContainsKey(StudentVisaMetadataKeys.EmailVerified))
            {
                metadata[StudentVisaMetadataKeys.EmailVerified] = false;
            }

            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            metadata[EntityFields.EmailVerifyTokenMeta] = token;

            entity.Metadata = metadata;

            var auditDetails = BuildPublicAuditDetails(context, wasCreated ? "Create" : "Update", email);

            ServiceResult<Entity> saveResult;
            if (wasCreated)
            {
                saveResult = await CreateAsync(entity, auditDetails, cancellationToken);
            }
            else
            {
                saveResult = await UpdateAsync(entity, auditDetails, cancellationToken);
            }

            if (!saveResult.IsSuccess || saveResult.Data == null)
            {
                return ServiceResult<StudentVisaSaveResult>.Failure(
                    saveResult.ErrorMessage ?? "Unable to save student registration.");
            }

            var baseUrl = (context.BaseUrl ?? "https://hub.mitanz.com").TrimEnd('/');
            var confirmationUrl = $"{baseUrl}/student-visa/confirm?token={token}";

            return ServiceResult<StudentVisaSaveResult>.Success(new StudentVisaSaveResult
            {
                Entity = saveResult.Data,
                WasCreated = wasCreated,
                VerificationToken = token,
                ConfirmationUrl = confirmationUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Student visa upsert failed for {Email}", email);
            return ServiceResult<StudentVisaSaveResult>.Failure(GraphErrorMapper.Map(ex));
        }
    }

    public async Task<ServiceResult<Entity>> VerifyStudentEmailAsync(
        string token,
        StudentVisaSaveContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ServiceResult<Entity>.Failure("Verification token is missing.");
        }

        try
        {
            var entity = await _repository.FindByEmailVerifyTokenAsync(token.Trim(), cancellationToken);
            if (entity == null)
            {
                return ServiceResult<Entity>.Failure("This confirmation link is invalid or has already been used.");
            }

            entity.Metadata[StudentVisaMetadataKeys.EmailVerified] = true;
            entity.Metadata[StudentVisaMetadataKeys.EmailVerifiedDate] = DateTime.UtcNow.ToString("O");
            entity.Metadata.Remove(EntityFields.EmailVerifyTokenMeta);

            var email = StudentVisaMetadataHelper.GetString(entity.Metadata, StudentVisaMetadataKeys.Email);
            if (!string.IsNullOrWhiteSpace(email))
            {
                entity.ModifiedBy = email;
            }

            var update = await UpdateAsync(
                entity,
                BuildPublicAuditDetails(context ?? new StudentVisaSaveContext(), "EmailVerify", email),
                cancellationToken);
            if (!update.IsSuccess || update.Data == null)
            {
                return ServiceResult<Entity>.Failure(update.ErrorMessage ?? "Unable to verify email.");
            }

            return ServiceResult<Entity>.Success(update.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Student email verification failed");
            return ServiceResult<Entity>.Failure(GraphErrorMapper.Map(ex));
        }
    }

    private static string BuildPublicAuditDetails(StudentVisaSaveContext context, string operation, string? who)
    {
        return $"Who={who ?? "unknown"}; When={DateTime.UtcNow:O}; IP={context.IpAddress ?? "n/a"}; Browser={Truncate(context.UserAgent, 200)}; Operation={operation}";
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "n/a";
        }

        return value.Length <= max ? value : value[..max];
    }

    private async Task ApplyAppUserAuditAsync(Entity entity, bool isCreate)
    {
        var display = await ResolveAppUserDisplayAsync();

        // Anonymous / public callers: keep caller-provided identity (e.g. student email).
        if (string.Equals(display, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            if (isCreate && string.IsNullOrWhiteSpace(entity.CreatedBy))
            {
                entity.CreatedBy = "public";
            }

            if (string.IsNullOrWhiteSpace(entity.ModifiedBy))
            {
                entity.ModifiedBy = entity.CreatedBy ?? "public";
            }

            return;
        }

        if (isCreate || string.IsNullOrWhiteSpace(entity.CreatedBy))
        {
            entity.CreatedBy = display;
        }

        entity.ModifiedBy = display;
    }

    private static void SeedRequiredDocumentsFromTemplate(Entity entity, EntityTemplate template)
    {
        if (entity.Metadata.ContainsKey("RequiredDocuments"))
            return;

        if (template.RequiredDocuments is not { Count: > 0 })
            return;

        entity.Metadata["RequiredDocuments"] = template.RequiredDocuments
            .Select(d => new Dictionary<string, object?>
            {
                ["Code"] = d.Code,
                ["Required"] = d.Required,
                ["Status"] = d.Status
            })
            .ToList();
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
