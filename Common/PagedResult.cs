using System.Collections.ObjectModel;

namespace MITANZ360Pro.Web.Common;

/// <summary>
/// Paged Graph list response.
/// </summary>
public sealed class PagedResult<T>
{
    public PagedResult(IEnumerable<T> items, string? nextLink, int pageSize)
    {
        Items = new ReadOnlyCollection<T>(items.ToList());
        NextLink = nextLink;
        PageSize = pageSize;
    }

    public IReadOnlyCollection<T> Items { get; }

    public string? NextLink { get; }

    public int PageSize { get; }
}
