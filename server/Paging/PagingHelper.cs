using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace server.Paging;


public static class PagingHelper
{
    public const int DefaultLimit = 12;
    public const int MaxLimit = 100;

    
    public static bool TryNormalize(
        int? skip,
        int? limit,
        out int normalizedSkip,
        out int normalizedLimit,
        out string? error
    )
    {
        normalizedSkip = skip ?? 0;
        normalizedLimit = limit ?? DefaultLimit;
        error = null;

        if (normalizedSkip < 0)
        {
            error = "Параметр skip не може бути від'ємним";
            return false;
        }

        if (normalizedLimit < 1)
        {
            error = "Параметр limit має бути не менше 1";
            return false;
        }

        if (normalizedLimit > MaxLimit)
        {
            normalizedLimit = MaxLimit;
        }

        return true;
    }

    
    public static string? BuildNextLink(HttpRequest request, int skip, int limit, int total)
    {
        var nextSkip = skip + limit;
        if (nextSkip >= total)
        {
            return null;
        }

        var parameters = request
            .Query.Where(q =>
                !q.Key.Equals("skip", StringComparison.OrdinalIgnoreCase)
                && !q.Key.Equals("limit", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string?>(q.Key, v)))
            .ToList();
        parameters.Add(new("skip", nextSkip.ToString()));
        parameters.Add(new("limit", limit.ToString()));

        var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";
        return QueryHelpers.AddQueryString(baseUrl, parameters);
    }
}
