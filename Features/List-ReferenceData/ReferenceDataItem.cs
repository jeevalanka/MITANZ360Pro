using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MITANZ360Pro.Web.Modules.ReferenceData;

public static class ReferenceDataFields
{
    public const string Title = "Title";
    public const string Code = "field_1";
    public const string Category = "field_2";
    public const string Description = "field_3";
    public const string Icon = "field_4";
    public const string Color = "field_5";
    public const string SortOrder = "field_6";
    public const string IsActive = "field_7";
    public const string IsDefault = "field_8";
}

public sealed class ReferenceDataItem
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Code is required.")]
    [StringLength(50)]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Category is required.")]
    [StringLength(100)]
    public string Category { get; set; } = "";

    [StringLength(2000)]
    public string Description { get; set; } = "";

    [StringLength(100)]
    public string Icon { get; set; } = "";

    [StringLength(50)]
    public string Color { get; set; } = "";

    [Required(ErrorMessage = "Sort Order is required.")]
    [Range(0, 100000)]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; }
}

public sealed class ReferenceDataFilter
{
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public string? SearchText { get; set; }

    public string? Category { get; set; }

    public bool? IsActive { get; set; }

    public string? OrderBy { get; set; }
}

public sealed class ReferenceDataPagedResult
{
    public IReadOnlyList<ReferenceDataItem> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }
}

public sealed class ReferenceDataImportResult
{
    public int Imported { get; set; }

    public int Skipped { get; set; }

    public int Errors { get; set; }

    public List<string> ErrorMessages { get; set; } = [];
}

public static class ReferenceDataMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ReferenceDataItem FromDictionary(IDictionary<string, object>? fields, string? listItemId = null)
    {
        var item = new ReferenceDataItem
        {
            Title = GetString(fields, ReferenceDataFields.Title),
            Code = GetString(fields, ReferenceDataFields.Code),
            Category = GetString(fields, ReferenceDataFields.Category),
            Description = GetString(fields, ReferenceDataFields.Description),
            Icon = GetString(fields, ReferenceDataFields.Icon),
            Color = GetString(fields, ReferenceDataFields.Color),
            SortOrder = GetInt(fields, ReferenceDataFields.SortOrder),
            IsActive = GetBool(fields, ReferenceDataFields.IsActive, true),
            IsDefault = GetBool(fields, ReferenceDataFields.IsDefault, false)
        };

        if (!string.IsNullOrWhiteSpace(listItemId) && int.TryParse(listItemId, out var id))
        {
            item.Id = id;
        }
        else if (fields != null)
        {
            item.Id = GetInt(fields, "ID");
            if (item.Id <= 0)
            {
                item.Id = GetInt(fields, "Id");
            }
        }

        return item;
    }

    public static Dictionary<string, object> ToFieldDictionary(ReferenceDataItem item)
    {
        return new Dictionary<string, object>
        {
            [ReferenceDataFields.Title] = item.Title ?? "",
            [ReferenceDataFields.Code] = item.Code ?? "",
            [ReferenceDataFields.Category] = item.Category ?? "",
            [ReferenceDataFields.Description] = item.Description ?? "",
            [ReferenceDataFields.Icon] = item.Icon ?? "",
            [ReferenceDataFields.Color] = item.Color ?? "",
            [ReferenceDataFields.SortOrder] = item.SortOrder,
            [ReferenceDataFields.IsActive] = item.IsActive,
            [ReferenceDataFields.IsDefault] = item.IsDefault
        };
    }

    private static string GetString(IDictionary<string, object>? fields, string key, string defaultValue = "")
    {
        if (fields == null || !fields.TryGetValue(key, out var value) || value == null)
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

    private static int GetInt(IDictionary<string, object>? fields, string key)
    {
        if (fields == null || !fields.TryGetValue(key, out var value) || value == null)
        {
            return 0;
        }

        return value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            JsonElement json when json.ValueKind == JsonValueKind.Number => json.GetInt32(),
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => int.TryParse(value.ToString(), out var n) ? n : 0
        };
    }

    private static bool GetBool(IDictionary<string, object>? fields, string key, bool defaultValue)
    {
        if (fields == null || !fields.TryGetValue(key, out var value) || value == null)
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
}
