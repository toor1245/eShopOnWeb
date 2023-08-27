using Microsoft.eShopWeb.Web.AzureFeatures.OrderReserve;

namespace Microsoft.eShopWeb.Web.Configuration;

public static class ConfigureAzureFunctionClients
{
    public static IServiceCollection AddAzureFunctionClients(this IServiceCollection services)
    {
        services.AddScoped(typeof(IOrderReserveClient), typeof(OrderReserveClient));
        return services;
    }  
}