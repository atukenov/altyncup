using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Yurt.Application.Features.Loyalty;
using Yurt.Infrastructure.Iiko;

namespace Yurt.UnitTests;

/// <summary>
/// iiko discontinued api/1/nomenclature (410 METHOD_DISCONTINUED), so the admin mapping
/// picker reads the catalog from external menus: api/2/menu lists them and
/// api/menu/v3/by_id returns each menu's groups and items.
/// </summary>
public class IikoNomenclatureTests
{
    private sealed class StubHandler(string menuListJson, Dictionary<string, string> menusById) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public List<string> FailingPaths { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            string? body = null;
            if (path.EndsWith("access_token")) body = """{"correlationId":"c1","token":"fake-token"}""";
            else if (path.EndsWith("api/2/menu")) body = menuListJson;
            else if (path.Contains("api/nomenclature/v1/"))
            {
                // Like the live API: these list endpoints reject limit <= 0.
                var req = await request.Content!.ReadAsStringAsync(ct);
                if (req.Contains("\"limit\":0") || FailingPaths.Any(f => path.EndsWith(f)))
                    return new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent("""{"message":"limit must be greater than 0"}""")
                    };
                if (path.EndsWith("product/list")) body = CatalogProducts;
                else if (path.EndsWith("group/list")) body = CatalogGroups;
                else if (path.EndsWith("product-scale/list")) body = CatalogScales;
            }
            else if (path.EndsWith("api/menu/v3/by_id"))
            {
                var req = await request.Content!.ReadAsStringAsync(ct);
                body = menusById.FirstOrDefault(kv => req.Contains($"\"externalMenuId\":\"{kv.Key}\"")).Value;
            }
            return body == null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private const string CoffeeMenu = """
    {
      "id": "101", "name": "App",
      "products": [ { "id": "11111111-0000-0000-0000-000000000001", "sku": "00042", "type": "DISH" } ],
      "itemsGroups": [
        { "id": "g1", "name": "Coffee", "items": [
          { "productId": "11111111-0000-0000-0000-000000000001", "name": "Latte",
            "sizePrices": [
              { "sizeId": "cccccccc-0000-0000-0000-000000000001", "sizeName": "S", "price": 1200 },
              { "sizeId": "cccccccc-0000-0000-0000-000000000002", "sizeName": "L", "price": 1500, "isDefault": true } ] },
          { "name": "Breakfast combo", "itemType": "COMBO", "comboId": "x" }
        ] }
      ]
    }
    """;

    private const string BakeryMenu = """
    {
      "id": "102", "name": "Bakery",
      "itemsGroups": [
        { "id": "g2", "name": "Bakery", "items": [
          { "productId": "11111111-0000-0000-0000-000000000002", "name": "Croissant",
            "sizePrices": [ { "sizeId": null, "price": 900 } ] },
          { "productId": "11111111-0000-0000-0000-000000000001", "name": "Latte (duplicate)" }
        ] }
      ]
    }
    """;

    private static IikoApiClient Client(StubHandler handler, string externalMenuId = "") =>
        new(new HttpClient(handler), new IikoOptions
        {
            Enabled = true, BaseUrl = "https://iiko.test", ApiKey = "k", AppId = "a", ClientSecret = "s",
            OrganizationId = Guid.NewGuid(), ExternalMenuId = externalMenuId,
        }, new IikoTokenStore(), NullLogger<IikoApiClient>.Instance);

    [Fact]
    public async Task GetNomenclatureAsync_MergesAllExternalMenus_SkipsCombosAndDuplicates()
    {
        var handler = new StubHandler(
            """{"correlationId":"c","externalMenus":[{"id":"101","name":"App"},{"id":"102","name":"Bakery"}]}""",
            new() { ["101"] = CoffeeMenu, ["102"] = BakeryMenu });

        var products = await Client(handler).GetNomenclatureAsync();

        Assert.DoesNotContain(handler.Paths, p => p.EndsWith("api/1/nomenclature"));
        Assert.Equal(["Croissant", "Latte"], products.Select(p => p.Name).ToArray());

        var latte = products.Single(p => p.Name == "Latte");
        Assert.Equal("00042", latte.Code);
        Assert.Equal("Coffee", latte.GroupName);
        Assert.Equal(1500m, latte.Price); // the default size's price
        Assert.Equal(["S", "L"], latte.Sizes.Select(s => s.Name).ToArray());

        var croissant = products.Single(p => p.Name == "Croissant");
        Assert.Null(croissant.Code);
        Assert.Equal(900m, croissant.Price);
        Assert.Empty(croissant.Sizes);
    }

    [Fact]
    public async Task GetNomenclatureAsync_UsesConfiguredMenuOnly()
    {
        var handler = new StubHandler("""{"externalMenus":[]}""", new() { ["101"] = CoffeeMenu, ["102"] = BakeryMenu });

        var products = await Client(handler, externalMenuId: "102").GetNomenclatureAsync();

        Assert.DoesNotContain(handler.Paths, p => p.EndsWith("api/2/menu"));
        Assert.Equal(["Croissant", "Latte (duplicate)"], products.Select(p => p.Name).ToArray());
    }

    private const string CatalogProducts = """
    {
      "totalCount": 4,
      "items": [
        { "productId": "22222222-0000-0000-0000-000000000001", "name": "Капучино", "type": "DISH", "deleted": false,
          "productArticle": "00101", "parentGroupId": "gggggggg-1", "productScaleId": "ssssssss-1",
          "disabledProductSizeIds": [ "dddddddd-0000-0000-0000-000000000003" ], "defaultSalePrice": 1400 },
        { "productId": "22222222-0000-0000-0000-000000000002", "name": "Вода", "type": "GOODS", "deleted": false,
          "code": "77", "defaultSalePrice": 400 },
        { "productId": "22222222-0000-0000-0000-000000000003", "name": "Сироп", "type": "MODIFIER", "deleted": false },
        { "productId": "22222222-0000-0000-0000-000000000004", "name": "Старый латте", "type": "DISH", "deleted": true }
      ]
    }
    """;

    private const string CatalogGroups = """
    { "groups": [ { "groupId": "gggggggg-1", "name": "Кофе" } ] }
    """;

    private const string CatalogScales = """
    { "items": [ { "id": "ssssssss-1", "productSizes": [
        { "id": "dddddddd-0000-0000-0000-000000000002", "name": "L", "priority": 2 },
        { "id": "dddddddd-0000-0000-0000-000000000001", "name": "S", "priority": 1 },
        { "id": "dddddddd-0000-0000-0000-000000000003", "name": "XL", "priority": 3 },
        { "id": "dddddddd-0000-0000-0000-000000000004", "name": "Old", "priority": 4, "deleted": true } ] } ] }
    """;

    [Fact]
    public async Task GetNomenclatureAsync_NoExternalMenus_FallsBackToNomenclatureCatalog()
    {
        var handler = new StubHandler("""{"externalMenus":[]}""", []);

        var products = await Client(handler).GetNomenclatureAsync();

        Assert.Contains(handler.Paths, p => p.EndsWith("api/nomenclature/v1/product/list"));
        Assert.Equal(["Капучино", "Вода"], products.Select(p => p.Name).ToArray()); // grouped first, ungrouped last

        var cappuccino = products[0];
        Assert.Equal("Кофе", cappuccino.GroupName);
        Assert.Equal("00101", cappuccino.Code);
        Assert.Equal(1400m, cappuccino.Price);
        Assert.Equal(["S", "L"], cappuccino.Sizes.Select(s => s.Name).ToArray()); // disabled + deleted sizes dropped

        var water = products[1];
        Assert.Null(water.GroupName);
        Assert.Equal("77", water.Code);
        Assert.Empty(water.Sizes);
    }

    [Fact]
    public async Task GetNomenclatureAsync_CatalogFallback_StillReturnsProductsWhenGroupLookupFails()
    {
        var handler = new StubHandler("""{"externalMenus":[]}""", []);
        handler.FailingPaths.Add("group/list");

        var products = await Client(handler).GetNomenclatureAsync();

        Assert.Equal(2, products.Count);
        Assert.All(products, p => Assert.Null(p.GroupName));
        Assert.Equal(["S", "L"], products.Single(p => p.Name == "Капучино").Sizes.Select(s => s.Name).ToArray());
    }
}
