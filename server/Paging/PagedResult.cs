namespace server.Paging;


public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Skip, int Limit, string? NextLink);
