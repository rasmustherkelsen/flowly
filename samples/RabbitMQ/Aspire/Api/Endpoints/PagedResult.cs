namespace Api.Endpoints;

internal record PagedResult<T>(T[] Items, int TotalCount, int Page, int PageSize);
