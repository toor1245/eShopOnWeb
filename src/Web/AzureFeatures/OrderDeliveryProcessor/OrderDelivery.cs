using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.Web.AzureFeatures.OrderDeliveryProcessor;

public class OrderDelivery
{
    public Address ShippingAddress { get; set; }
    public List<int> Items { get; set; }
    public decimal FinalPrice { get; set; }
}
