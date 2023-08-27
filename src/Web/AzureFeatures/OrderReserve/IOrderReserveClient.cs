using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.Web.AzureFeatures.OrderReserve;

public interface IOrderReserveClient
{
    public Task SendAsync(IReadOnlyCollection<OrderItem> orderItems);
}