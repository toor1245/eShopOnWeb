using System.Text.Json;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.Web.AzureFeatures.OrderReserve;

public class OrderReserveClient : IOrderReserveClient
{
    private const string OrderItemsReserveEnvVariable = "OrderItemsReserveUrl";

    public async Task SendAsync(IReadOnlyCollection<OrderItem> orderItems)
    {
        OrderReserve orderReserve = new();
        using var httpClient = new HttpClient();

        var orderReserveItems = orderItems.Select(x => {
            return new OrderReserveItem() {
                Id = x.Id,
                Quantity = x.Units
            };
        }).ToList();

        orderReserve.OrderReserveItems.AddRange(orderReserveItems);

        StringContent stringContent = new StringContent(JsonSerializer.Serialize(orderReserve));
        HttpResponseMessage responseMessage = await httpClient.PostAsync(Environment.GetEnvironmentVariable(OrderItemsReserveEnvVariable), stringContent);
        responseMessage.EnsureSuccessStatusCode();
    }   
}