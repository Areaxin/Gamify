using System.Net;
using System.Text;
using Gamify.Api;
using Xunit;

namespace Gamify.Api.Tests;

public sealed class LiveStoreClientTests
{
    [Fact]
    public async Task UsesCurrentSteamPriceForLithuania()
    {
        using var http = new HttpClient(new StoreHandler("""
            {"739630":{"success":true,"data":{
                "name":"Phasmophobia","type":"game","is_free":false,"required_age":"0",
                "price_overview":{"currency":"EUR","initial":1999,"final":1399,"discount_percent":30},
                "categories":[{"id":38}]
            }}}
            """));
        var result = await new LiveStoreClient(http).CheckAsync(
            StoreLinks.Resolve("https://store.steampowered.com/app/739630/Phasmophobia/")!, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1399, result.PriceCents);
        Assert.True(result.Multiplayer);
    }

    [Fact]
    public async Task DoesNotUsePriceInAnotherCurrency()
    {
        using var http = new HttpClient(new StoreHandler("""
            {"739630":{"success":true,"data":{
                "name":"Phasmophobia","type":"game","is_free":false,"required_age":0,
                "price_overview":{"currency":"USD","initial":1999,"final":1399}
            }}}
            """));
        var result = await new LiveStoreClient(http).CheckAsync(
            StoreLinks.Resolve("https://store.steampowered.com/app/739630/")!, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.PriceCents);
    }

    [Theory]
    [InlineData("https://image.api.playstation.com/vulcan/art.png", true)]
    [InlineData("https://image.api.playstation.com.example.test/vulcan/art.png", false)]
    public async Task ReadsOfficialPlayStationCoverAndPrice(string image, bool trusted)
    {
        const string storeUrl = "https://store.playstation.com/en-ie/concept/10005673";
        var page = $$$"""
            <script id="mfe-jsonld-tags" type = "application/ld+json">
            {"@type":"Product","image":"{{{image}}}","offers":{"@type":"Offer","price":18.99,"priceCurrency":"EUR"}}
            </script>
            """;
        using var http = new HttpClient(new StoreHandler(page, storeUrl));
        var result = await new LiveStoreClient(http).CheckAsync(StoreLinks.Resolve(storeUrl)!, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1899, result.PriceCents);
        Assert.Equal(trusted ? image : null, result.CoverImageUrl);
    }

    private sealed class StoreHandler(string body,
        string expectedUrl = "https://store.steampowered.com/api/appdetails?appids=739630&cc=lt&l=english") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(expectedUrl, request.RequestUri?.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
