using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Yurt.Application.Features.Loyalty;
using Yurt.Infrastructure.Iiko;

namespace Yurt.UnitTests;

/// <summary>
/// Regression coverage for the empty iiko mapping picker: the live iiko API sends
/// product types capitalised ("Dish", "Good", "Modifier") while its docs show them in
/// lower case. Filtering on the lower-case spelling dropped every product.
/// </summary>
public class IikoNomenclatureTests
{
    private sealed class StubHandler(string nomenclatureJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = path.EndsWith("access_token")
                ? """{"correlationId":"c1","token":"fake-token"}"""
                : path.EndsWith("api/1/nomenclature") ? nomenclatureJson : null;
            return Task.FromResult(body == null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
        }
    }

    private const string Payload = """
    {
      "correlationId": "c1",
      "revision": 1,
      "groups": [ { "id": "aaaaaaaa-0000-0000-0000-000000000001", "name": "Coffee" } ],
      "productCategories": [ { "id": "bbbbbbbb-0000-0000-0000-000000000001", "name": "Bakery" } ],
      "sizes": [ { "id": "cccccccc-0000-0000-0000-000000000001", "name": "L" } ],
      "products": [
        { "id": "11111111-0000-0000-0000-000000000001", "name": "Latte", "code": "00042", "type": "Dish",
          "isDeleted": false, "parentGroup": "aaaaaaaa-0000-0000-0000-000000000001",
          "sizePrices": [ { "sizeId": "cccccccc-0000-0000-0000-000000000001", "price": { "currentPrice": 1500 } } ] },
        { "id": "11111111-0000-0000-0000-000000000002", "name": "Croissant", "code": "", "type": "Good",
          "isDeleted": false, "productCategoryId": "bbbbbbbb-0000-0000-0000-000000000001",
          "sizePrices": [ { "sizeId": null, "price": null } ] },
        { "id": "11111111-0000-0000-0000-000000000003", "name": "Extra shot", "type": "Modifier", "isDeleted": false },
        { "id": "11111111-0000-0000-0000-000000000004", "name": "Old latte", "type": "Dish", "isDeleted": true }
      ]
    }
    """;

    [Fact]
    public async Task GetNomenclatureAsync_KeepsCapitalisedDishesAndGoods_DropsModifiersAndDeleted()
    {
        var options = new IikoOptions
        {
            Enabled = true, BaseUrl = "https://iiko.test", ApiKey = "k", AppId = "a", ClientSecret = "s",
            OrganizationId = Guid.NewGuid(),
        };
        var client = new IikoApiClient(
            new HttpClient(new StubHandler(Payload)), options, new IikoTokenStore(), NullLogger<IikoApiClient>.Instance);

        var products = await client.GetNomenclatureAsync();

        Assert.Equal(["Croissant", "Latte"], products.Select(p => p.Name).ToArray());

        var latte = products.Single(p => p.Name == "Latte");
        Assert.Equal("00042", latte.Code);
        Assert.Equal("Coffee", latte.GroupName);
        Assert.Equal(1500m, latte.Price);
        Assert.Equal("L", Assert.Single(latte.Sizes).Name);

        var croissant = products.Single(p => p.Name == "Croissant");
        Assert.Null(croissant.Code);
        Assert.Equal("Bakery", croissant.GroupName); // falls back to the RMS category
        Assert.Equal(0m, croissant.Price);
        Assert.Empty(croissant.Sizes);
    }
}
