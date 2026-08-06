using System.Text.Json;
using System.Text.Json.Serialization;

namespace MITANZ360Pro.Web.Modules.Entities;

public static class EntityFields
{
    public const string Id = "id";
    public const string Title = "Title";
    public const string EntityId = "field_1";
    public const string EntityType = "field_2";
    public const string Status = "field_8";
    public const string IsActive = "field_10";
    public const string MetadataJson = "MetadataJson";
    public const string Created = "Created";
    public const string Modified = "Modified";
    public const string Author = "Author";
    public const string Editor = "Editor";

    /// <summary>App-login audit keys stored inside MetadataJson (not Office 365 person fields).</summary>
    public const string AppCreatedByMeta = "__CreatedBy";
    public const string AppModifiedByMeta = "__ModifiedBy";

    /// <summary>Reserved: one-time token for public student email verification.</summary>
    public const string EmailVerifyTokenMeta = "__EmailVerifyToken";
}

public static class EntityStatuses
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string Archived = "Archived";
}

public sealed class Entity
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public string EntityId { get; set; } = "";

    public string EntityType { get; set; } = "";

    public string Status { get; set; } = EntityStatuses.Draft;

    public bool IsActive { get; set; } = true;

    public Dictionary<string, object?> Metadata { get; set; } = new();

    public DateTime? Created { get; set; }

    public DateTime? Modified { get; set; }

    public string? CreatedBy { get; set; }

    public string? ModifiedBy { get; set; }
}

public sealed class EntityFilter
{
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public string? SearchText { get; set; }

    public string? EntityType { get; set; }

    public string? Status { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedFrom { get; set; }

    public DateTime? CreatedTo { get; set; }

    public string? NextLink { get; set; }
}

public static class EntityMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static Entity FromDictionary(IDictionary<string, object>? fields, string? listItemId = null)
    {
        if (fields == null)
        {
            return new Entity();
        }

        var entity = new Entity
        {
            Title = GetString(fields, EntityFields.Title),
            EntityId = FirstNonEmpty(
                GetString(fields, EntityFields.EntityId),
                GetString(fields, "EntityId")),
            EntityType = FirstNonEmpty(
                GetString(fields, EntityFields.EntityType),
                GetString(fields, "EntityType")),
            Status = FirstNonEmpty(
                GetString(fields, EntityFields.Status),
                GetString(fields, "Status"),
                EntityStatuses.Draft),
            IsActive = GetBool(fields, EntityFields.IsActive, GetBool(fields, "IsActive", true)),
            Created = GetDate(fields, EntityFields.Created),
            Modified = GetDate(fields, EntityFields.Modified),
            // Prefer app-login audit values from MetadataJson; fall back to SharePoint Author/Editor.
            CreatedBy = null,
            ModifiedBy = null
        };

        // Graph returns the SharePoint list item id on ListItem.Id — not reliably in fields.
        // Prefer listItemId; fall back to common field keys used by Graph/SharePoint.
        if (!string.IsNullOrWhiteSpace(listItemId) && int.TryParse(listItemId, out var idFromItem))
        {
            entity.Id = idFromItem;
        }
        else
        {
            entity.Id = GetInt(fields, "ID");
            if (entity.Id <= 0)
            {
                entity.Id = GetInt(fields, "Id");
            }
            if (entity.Id <= 0)
            {
                entity.Id = GetInt(fields, "id");
            }
        }

        var metadataJson = GetString(fields, EntityFields.MetadataJson);
        if (!string.IsNullOrWhiteSpace(metadataJson))
        {
            try
            {
                entity.Metadata = JsonSerializer.Deserialize<Dictionary<string, object?>>(metadataJson, JsonOptions)
                    ?? new Dictionary<string, object?>();
            }
            catch
            {
                entity.Metadata = new Dictionary<string, object?>();
            }
        }

        entity.CreatedBy = FirstNonEmpty(
            GetMetaString(entity.Metadata, EntityFields.AppCreatedByMeta),
            GetLookupDisplay(fields, EntityFields.Author) ?? string.Empty);

        entity.ModifiedBy = FirstNonEmpty(
            GetMetaString(entity.Metadata, EntityFields.AppModifiedByMeta),
            GetLookupDisplay(fields, EntityFields.Editor) ?? string.Empty);

        return entity;
    }

    public static Dictionary<string, object> ToFieldDictionary(Entity entity)
    {
        // Persist app-login Created/Modified By inside MetadataJson (SharePoint Author/Editor
        // are Office 365 person fields controlled by Graph app-only identity).
        var metadata = entity.Metadata != null
            ? new Dictionary<string, object?>(entity.Metadata)
            : new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(entity.CreatedBy))
        {
            metadata[EntityFields.AppCreatedByMeta] = entity.CreatedBy;
        }

        if (!string.IsNullOrWhiteSpace(entity.ModifiedBy))
        {
            metadata[EntityFields.AppModifiedByMeta] = entity.ModifiedBy;
        }

        var fields = new Dictionary<string, object>
        {
            [EntityFields.Title] = entity.Title,
            [EntityFields.EntityId] = entity.EntityId,
            [EntityFields.EntityType] = entity.EntityType,
            [EntityFields.Status] = entity.Status,
            [EntityFields.IsActive] = entity.IsActive,
            [EntityFields.MetadataJson] = JsonSerializer.Serialize(metadata, JsonOptions)
        };

        return fields;
    }

    public static Dictionary<string, object?> TemplateMetadataOnly(Dictionary<string, object?> metadata)
    {
        return metadata
            .Where(kv => !IsReservedMetaKey(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public static bool IsReservedMetaKey(string key)
        => key.StartsWith("__", StringComparison.Ordinal);

    private static string GetMetaString(Dictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value == null)
        {
            return string.Empty;
        }

        return value switch
        {
            JsonElement json when json.ValueKind == JsonValueKind.String => json.GetString() ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string GetString(IDictionary<string, object> fields, string key, string defaultValue = "")
    {
        if (!fields.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        return value switch
        {
            JsonElement json => json.ValueKind switch
            {
                JsonValueKind.String => json.GetString() ?? defaultValue,
                JsonValueKind.Number => json.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => json.ToString() ?? defaultValue
            },
            _ => value.ToString() ?? defaultValue
        };
    }

    private static bool GetBool(IDictionary<string, object> fields, string key, bool defaultValue)
    {
        if (!fields.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        return value switch
        {
            bool b => b,
            JsonElement json when json.ValueKind == JsonValueKind.True => true,
            JsonElement json when json.ValueKind == JsonValueKind.False => false,
            string s when bool.TryParse(s, out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private static int GetInt(IDictionary<string, object> fields, string key)
    {
        if (!fields.TryGetValue(key, out var value) || value == null)
        {
            return 0;
        }

        return value switch
        {
            int i => i,
            long l => (int)l,
            JsonElement json when json.ValueKind == JsonValueKind.Number => json.GetInt32(),
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => 0
        };
    }

    private static DateTime? GetDate(IDictionary<string, object> fields, string key)
    {
        if (!fields.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is DateTimeOffset dto)
        {
            return dto.UtcDateTime;
        }

        if (value is DateTime dt)
        {
            return dt;
        }

        if (value is JsonElement json && json.ValueKind == JsonValueKind.String)
        {
            return DateTime.TryParse(json.GetString(), out var parsed) ? parsed : null;
        }

        return DateTime.TryParse(value.ToString(), out var result) ? result : null;
    }

    private static string? GetLookupDisplay(IDictionary<string, object> fields, string key)
    {
        if (!fields.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is JsonElement json && json.ValueKind == JsonValueKind.Object)
        {
            if (json.TryGetProperty("LookupValue", out var lookup))
            {
                return lookup.GetString();
            }

            if (json.TryGetProperty("Email", out var email))
            {
                return email.GetString();
            }
        }

        return value.ToString();
    }
}
