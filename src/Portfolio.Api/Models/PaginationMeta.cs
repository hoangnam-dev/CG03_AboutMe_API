namespace Portfolio.Api.Models;

public sealed record PaginationMeta(
    int Page,
    int PageSize,
    long Total,
    int TotalPages);
