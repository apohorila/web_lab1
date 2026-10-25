using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using server.Paging;
using Xunit;

namespace server.Tests;


public class PagingHelperTests
{
    private static HttpRequest RequestFor(string query, string path = "/api/products", string pathBase = "")
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "http";
        http.Request.Host = new HostString("localhost:5038");
        http.Request.PathBase = pathBase;
        http.Request.Path = path;
        http.Request.QueryString = new QueryString(query);
        return http.Request;
    }


    [Fact]
    public void Defaults_AreSkip0AndLimit12()
    {
        Assert.True(PagingHelper.TryNormalize(null, null, out var skip, out var limit, out var error));

        Assert.Equal(0, skip);
        Assert.Equal(12, limit);
        Assert.Null(error);
    }

    [Fact]
    public void ValidValues_AreKept()
    {
        Assert.True(PagingHelper.TryNormalize(30, 5, out var skip, out var limit, out _));

        Assert.Equal(30, skip);
        Assert.Equal(5, limit);
    }

    [Fact]
    public void OnlyLimit_StartsFromTheBeginning_AndOnlySkip_UsesTheDefaultLimit()
    {
        PagingHelper.TryNormalize(null, 4, out var skipA, out var limitA, out _);
        PagingHelper.TryNormalize(8, null, out var skipB, out var limitB, out _);

        Assert.Equal(0, skipA);
        Assert.Equal(4, limitA);
        Assert.Equal(8, skipB);
        Assert.Equal(12, limitB);
    }

    [Fact]
    public void TooLargeLimit_IsClampedToTheMaximum()
    {
        Assert.True(PagingHelper.TryNormalize(0, 100000, out _, out var limit, out _));

        Assert.Equal(100, limit);
    }

    [Fact]
    public void NegativeSkip_IsRejected()
    {
        Assert.False(PagingHelper.TryNormalize(-1, 10, out _, out _, out var error));

        Assert.Contains("skip", error);
    }

    [Fact]
    public void LimitBelowOne_IsRejected()
    {
        Assert.False(PagingHelper.TryNormalize(0, 0, out _, out _, out var errorZero));
        Assert.False(PagingHelper.TryNormalize(0, -5, out _, out _, out var errorNegative));

        Assert.Contains("limit", errorZero);
        Assert.Contains("limit", errorNegative);
    }



    [Fact]
    public void NextLink_PointsToTheFollowingPage()
    {
        var link = PagingHelper.BuildNextLink(RequestFor("?skip=0&limit=3"), skip: 0, limit: 3, total: 7);

        Assert.Equal("http://localhost:5038/api/products?skip=3&limit=3", link);
    }

    [Fact]
    public void NextLink_KeepsOtherQueryParameters_AndReplacesSkipAndLimit()
    {
        var link = PagingHelper.BuildNextLink(RequestFor("?categoryId=2&skip=3&limit=3"), 3, 3, 10);

        Assert.Equal("http://localhost:5038/api/products?categoryId=2&skip=6&limit=3", link);
    }

    [Fact]
    public void NextLink_ParameterNamesAreCaseInsensitive_NoDuplicates()
    {
        var link = PagingHelper.BuildNextLink(RequestFor("?SKIP=3&Limit=3"), 3, 3, 10);

        Assert.Equal("http://localhost:5038/api/products?skip=6&limit=3", link);
    }

    [Fact]
    public void NextLink_IsNull_OnTheLastPage()
    {
        Assert.Null(PagingHelper.BuildNextLink(RequestFor("?skip=6&limit=3"), 6, 3, 7));
    }

    [Fact]
    public void NextLink_IsNull_WhenThePageEndsExactlyAtTheTotal()
    {
        Assert.Null(PagingHelper.BuildNextLink(RequestFor("?limit=5"), 0, 5, 5));
        Assert.NotNull(PagingHelper.BuildNextLink(RequestFor("?limit=5"), 0, 5, 6));
    }

    [Fact]
    public void NextLink_IsNull_ForAnEmptyResultOrSkipBeyondTheEnd()
    {
        Assert.Null(PagingHelper.BuildNextLink(RequestFor("?skip=0&limit=3"), 0, 3, 0));
        Assert.Null(PagingHelper.BuildNextLink(RequestFor("?skip=50&limit=3"), 50, 3, 7));
    }

    [Fact]
    public void NextLink_RespectsThePathBase()
    {
        var link = PagingHelper.BuildNextLink(RequestFor("?limit=2", pathBase: "/shop"), 0, 2, 9);

        Assert.Equal("http://localhost:5038/shop/api/products?limit=2&skip=2".Replace("limit=2&skip=2", "skip=2&limit=2"), link);
    }

    [Fact]
    public void NextLink_KeepsSpecialCharactersIntact()
    {
        var link = PagingHelper.BuildNextLink(RequestFor("?q=a%20b%26c&skip=0&limit=1"), 0, 1, 5)!;

        var query = QueryHelpers.ParseQuery(new Uri(link).Query);
        Assert.Equal("a b&c", query["q"].ToString());
        Assert.Equal("1", query["skip"].ToString());
    }

    [Fact]
    public void FollowingNextLinks_VisitsEveryItemExactlyOnce()
    {
        const int total = 10;
        var visited = new List<int>();
        var skip = 0;
        string? link;

        do
        {
            var items = Math.Min(4, total - skip);
            visited.AddRange(Enumerable.Range(skip, items));
            link = PagingHelper.BuildNextLink(RequestFor($"?skip={skip}&limit=4"), skip, 4, total);
            if (link != null)
            {
                skip = int.Parse(QueryHelpers.ParseQuery(new Uri(link).Query)["skip"].ToString());
            }
        } while (link != null);

        Assert.Equal(Enumerable.Range(0, total).ToArray(), visited.ToArray());
    }

    [Fact]
    public void PagedResult_IsSerializedWithCamelCaseNames()
    {
        var page = new PagedResult<int>(new[] { 1, 2 }, 5, 0, 2, "http://x/next");

        var json = JsonDocument.Parse(JsonSerializer.Serialize(page, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement;

        Assert.Equal(2, json.GetProperty("items").GetArrayLength());
        Assert.Equal(5, json.GetProperty("total").GetInt32());
        Assert.Equal(0, json.GetProperty("skip").GetInt32());
        Assert.Equal(2, json.GetProperty("limit").GetInt32());
        Assert.Equal("http://x/next", json.GetProperty("nextLink").GetString());
    }
}
