namespace MITANZ360Pro.Web.Infrastructure.SharePoint;

public sealed class SharePointOptions
{
    public const string SectionName = "SharePoint";

    public string BaseUrl { get; set; } = "";

    public string SiteId { get; set; } = "";

    public SharePointListOptions Lists { get; set; } = new();
}

public sealed class SharePointListOptions
{
    public string Entities { get; set; } = "";

    public string Activities { get; set; } = "";

    public string WorkflowHistory { get; set; } = "";

    public string ReferenceData { get; set; } = "";
}
