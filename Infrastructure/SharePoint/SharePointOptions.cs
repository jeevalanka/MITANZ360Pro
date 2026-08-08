namespace MITANZ360Pro.Web.Infrastructure.SharePoint;

public sealed class SharePointOptions
{
    public const string SectionName = "SharePoint";

    public string BaseUrl { get; set; } = "";

    public string SiteId { get; set; } = "";

    public SharePointListOptions Lists { get; set; } = new();

    public SharePointLibraryOptions Libraries { get; set; } = new();
}

public sealed class SharePointListOptions
{
    public string Entities { get; set; } = "";

    public string Activities { get; set; } = "";

    public string WorkflowHistory { get; set; } = "";

    public string ReferenceData { get; set; } = "";

    /// <summary>Underlying list id for the Documents document library (metadata queries).</summary>
    public string Documents { get; set; } = "";
}

/// <summary>Document library drive identifiers (not separate SharePoint lists for entity files).</summary>
public sealed class SharePointLibraryOptions
{
    /// <summary>Entity Documents library drive id (library name: Documents).</summary>
    public string Documents { get; set; } = "";

    public string LibDocuments { get; set; } = "";

    public string ListId { get; set; } = "";
}
