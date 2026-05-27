namespace Arca.Application.Common;

public sealed record PageRequest(int Page = 1, int PageSize = 25, string? Search = null)
{
    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedPageSize => PageSize switch
    {
        < 1 => 25,
        > 100 => 100,
        _ => PageSize
    };

    public int Offset => (NormalizedPage - 1) * NormalizedPageSize;
    public string? NormalizedSearch => string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
}

public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
