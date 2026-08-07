using System.Text.Json.Serialization;

namespace MITANZ360Pro.Web.Modules.Entities;

public sealed class EntityTemplate
{
    [JsonPropertyName("EntityType")]
    public string EntityType { get; set; } = "";

    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("Fields")]
    public List<EntityTemplateField> Fields { get; set; } = [];

    /// <summary>
    /// Default RequiredDocuments[] seeded into Entity Metadata for new records of this type.
    /// Source of required docs remains Metadata JSON at runtime; SharePoint stores uploaded files.
    /// </summary>
    [JsonPropertyName("RequiredDocuments")]
    public List<EntityRequiredDocumentTemplate>? RequiredDocuments { get; set; }
}

public sealed class EntityRequiredDocumentTemplate
{
    [JsonPropertyName("Code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("Required")]
    public bool Required { get; set; } = true;

    [JsonPropertyName("Status")]
    public string Status { get; set; } = "Requested";
}

public sealed class EntityTemplateField
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("Type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("Required")]
    public bool Required { get; set; }

    [JsonPropertyName("MaxLength")]
    public int? MaxLength { get; set; }

    [JsonPropertyName("Pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("Options")]
    public List<string>? Options { get; set; }
}
