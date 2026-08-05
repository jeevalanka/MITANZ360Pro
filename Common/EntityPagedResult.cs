namespace MITANZ360Pro.Web.Common;

/// <summary>
/// UI-oriented paged result with total count and optional Graph next link.
/// </summary>
public sealed class EntityPagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public string? NextLink { get; init; }

    public bool HasMore => !string.IsNullOrWhiteSpace(NextLink);
}
