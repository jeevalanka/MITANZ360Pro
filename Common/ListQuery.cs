namespace MITANZ360Pro.Web.Common;

/// <summary>
/// Query options for list retrieval.
/// </summary>
public sealed class ListQuery
{
    public int PageSize { get; set; } = 100;

    public string? SearchText { get; set; }

    public string? Filter { get; set; }

    public string? OrderBy { get; set; }

    public string? NextLink { get; set; }
}
