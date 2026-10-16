using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Menu.Services;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

/// <summary>
/// Tests for <see cref="RecommendationService"/> — the cart's "frequently added together" row.
/// </summary>
public class RecommendationServiceTests
{
    private static readonly Guid LocationA = Guid.NewGuid();
    private static readonly Guid LocationB = Guid.NewGuid();
    private static readonly Guid Customer = Guid.NewGuid();

    private readonly List<MenuItem> _items = [];
    private readonly List<OrderItem> _orderItems = [];

    private MenuItem Item(string name, bool available = true, int sortOrder = 0)
    {
        var item = new MenuItem
        {
            Name = name, IsAvailable = available, SortOrder = sortOrder,
            Category = new MenuCategory { Name = "Cat" },
        };
        _items.Add(item);
        return item;
    }

    private void Order(params MenuItem[] items) => OrderAt(LocationA, OrderStatus.Completed, null, items);

    private void OrderAt(Guid locationId, OrderStatus status, Guid? customerId, params MenuItem[] items)
    {
        var order = new Order
        {
            LocationId = locationId, Status = status,
            CustomerUserId = customerId ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
        };
        foreach (var i in items)
            _orderItems.Add(new OrderItem { OrderId = order.Id, Order = order, MenuItemId = i.Id, Quantity = 1 });
    }

    private RecommendationService Build()
    {
        var db = Substitute.For<IApplicationDbContext>();
        var itemSet = _items.BuildMockDbSet();
        var orderItemSet = _orderItems.BuildMockDbSet();
        db.MenuItems.Returns(itemSet);
        db.OrderItems.Returns(orderItemSet);
        return new RecommendationService(db, Substitute.For<IMenuCacheService>());
    }

    [Fact]
    public async Task RanksItemsBoughtTogetherFirst()
    {
        var latte = Item("Latte");
        var croissant = Item("Croissant");
        var cookie = Item("Cookie");
        for (var n = 0; n < 4; n++) Order(latte, croissant);
        for (var n = 0; n < 3; n++) Order(latte, cookie);
        for (var n = 0; n < 10; n++) Order(cookie); // cookie is more popular overall

        var result = await Build().GetCartRecommendationsAsync([latte.Id], LocationA, null);

        Assert.Equal(["Croissant", "Cookie"], result.Select(r => r.Name));
    }

    [Fact]
    public async Task IgnoresPairsBelowMinimumSupport_AndFallsBackToPopular()
    {
        var latte = Item("Latte");
        var rare = Item("Rare");
        var bestSeller = Item("BestSeller");
        Order(latte, rare); Order(latte, rare); // only 2 co-orders: noise
        for (var n = 0; n < 5; n++) Order(bestSeller);

        var result = await Build().GetCartRecommendationsAsync([latte.Id], LocationA, null);

        Assert.Equal("BestSeller", result[0].Name);
        Assert.Equal("Rare", result[1].Name); // still eligible as a (less) popular item
    }

    [Fact]
    public async Task ExcludesCartItemsAndUnavailableItems()
    {
        var latte = Item("Latte");
        var tea = Item("Tea");
        var soldOut = Item("SoldOut", available: false);
        var cake = Item("Cake");
        for (var n = 0; n < 5; n++) Order(latte, tea, soldOut, cake);

        var result = await Build().GetCartRecommendationsAsync([latte.Id, tea.Id], LocationA, null);

        Assert.Equal(["Cake"], result.Select(r => r.Name));
    }

    [Fact]
    public async Task ExcludesItemsNotSoldAtLocation()
    {
        var latte = Item("Latte");
        var elsewhere = Item("Elsewhere");
        elsewhere.MenuItemLocations.Add(new MenuItemLocation { MenuItemId = elsewhere.Id, LocationId = LocationB });
        for (var n = 0; n < 5; n++) Order(latte, elsewhere);

        var result = await Build().GetCartRecommendationsAsync([latte.Id], LocationA, null);

        Assert.Empty(result);
    }

    [Fact]
    public async Task OnlyCountsCompletedOrdersAtTheLocation()
    {
        var latte = Item("Latte");
        var declined = Item("Declined");
        var otherShop = Item("OtherShop");
        for (var n = 0; n < 5; n++)
        {
            OrderAt(LocationA, OrderStatus.Declined, null, latte, declined);
            OrderAt(LocationB, OrderStatus.Completed, null, latte, otherShop);
        }

        var result = await Build().GetCartRecommendationsAsync([latte.Id], LocationA, null);

        Assert.Empty(result);
    }

    [Fact]
    public async Task BoostsItemsTheCustomerOrderedBefore()
    {
        var latte = Item("Latte");
        var muffin = Item("Muffin");
        var scone = Item("Scone");
        for (var n = 0; n < 4; n++) Order(latte, muffin);
        for (var n = 0; n < 3; n++) Order(latte, scone);
        for (var n = 0; n < 3; n++) OrderAt(LocationA, OrderStatus.Completed, Customer, scone);

        var anonymous = await Build().GetCartRecommendationsAsync([latte.Id], LocationA, null);
        var signedIn = await Build().GetCartRecommendationsAsync([latte.Id], LocationA, Customer);

        Assert.Equal("Muffin", anonymous[0].Name);
        Assert.Equal("Scone", signedIn[0].Name);
    }

    [Fact]
    public async Task ReturnsAtMostFourItems()
    {
        var latte = Item("Latte");
        var others = Enumerable.Range(0, 6).Select(n => Item($"Item{n}")).ToArray();
        for (var n = 0; n < 3; n++) Order([latte, .. others]);

        var result = await Build().GetCartRecommendationsAsync([latte.Id], LocationA, null, limit: 10);

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public async Task ReturnsEmptyWhenThereIsNoHistory()
    {
        var latte = Item("Latte");
        Item("Unsold");

        var result = await Build().GetCartRecommendationsAsync([latte.Id], LocationA, null);

        Assert.Empty(result);
    }
}
