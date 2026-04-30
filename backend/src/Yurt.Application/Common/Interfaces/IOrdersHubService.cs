using Yurt.Domain.Entities;

namespace Yurt.Application.Common.Interfaces;

public interface IOrdersHubService
{
    Task NotifyOrderCreatedAsync(Order order, CancellationToken cancellationToken = default);
    Task NotifyOrderUpdatedAsync(Order order, CancellationToken cancellationToken = default);
    Task NotifyOrderDeclinedAsync(Order order, CancellationToken cancellationToken = default);
    Task NotifyPaymentUpdatedAsync(Order order, CancellationToken cancellationToken = default);
}
