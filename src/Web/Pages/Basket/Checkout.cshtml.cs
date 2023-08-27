using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Ardalis.GuardClauses;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Web.AzureFeatures.OrderDeliveryProcessor;
using Microsoft.eShopWeb.Web.AzureFeatures.OrderReserve;
using Microsoft.eShopWeb.Web.Interfaces;

namespace Microsoft.eShopWeb.Web.Pages.Basket;

[Authorize]
public class CheckoutModel : PageModel
{
    private readonly IBasketService _basketService;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOrderService _orderService;
    private string? _username = null;
    private readonly IBasketViewModelService _basketViewModelService;
    private readonly IAppLogger<CheckoutModel> _logger;

    public CheckoutModel(IBasketService basketService,
        IBasketViewModelService basketViewModelService,
        SignInManager<ApplicationUser> signInManager,
        IOrderService orderService,
        IAppLogger<CheckoutModel> logger)
    {
        _basketService = basketService;
        _signInManager = signInManager;
        _orderService = orderService;
        _basketViewModelService = basketViewModelService;
        _logger = logger;
    }

    public BasketViewModel BasketModel { get; set; } = new BasketViewModel();

    public async Task OnGet()
    {
        await SetBasketModelAsync();
    }

    public async Task<IActionResult> OnPost(IEnumerable<BasketItemViewModel> items)
    {
        try
        {
            await SetBasketModelAsync();

            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var updateModel = items.ToDictionary(b => b.Id.ToString(), b => b.Quantity);
            await _basketService.SetQuantities(BasketModel.Id, updateModel);
            
            var order = await _orderService.CreateOrderAsync(BasketModel.Id,
                new Address("123 Main St.", "Kent", "OH", "United States", "44240"));
            await _basketService.DeleteBasketAsync(BasketModel.Id);

            var orderDelivery = new OrderDelivery
            {
                ShippingAddress = order.ShipToAddress,
                FinalPrice = order.Total(),
                Items = order.OrderItems.Select(x => x.Id).ToList()
            };

            using var httpClientOrderDelivery = new HttpClient();
            StringContent contentOrderDelivery = new StringContent(JsonSerializer.Serialize(orderDelivery));
            var responseMessageOrderDelivery = await httpClientOrderDelivery
                .PostAsync(Environment.GetEnvironmentVariable("OrderDeliveryProcessorUrl"), contentOrderDelivery);
            responseMessageOrderDelivery.EnsureSuccessStatusCode();

            OrderReserve orderReserve = new();
            using var httpClient = new HttpClient();

            var orderReserveItems = order.OrderItems.Select(x => new OrderReserveItem
            {
                Id = x.Id,
                Quantity = x.Units
            }).ToList();

            orderReserve.OrderReserveItems.AddRange(orderReserveItems);

            string? connectionString = Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_CONNECTION");

            await using var client = new ServiceBusClient(connectionString);
            ServiceBusSender sender = client.CreateSender(Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_QUEUE_NAME"));
            ServiceBusMessage message = new ServiceBusMessage(JsonSerializer.Serialize(orderReserve));
            await sender.SendMessageAsync(message);
        }
        catch (EmptyBasketOnCheckoutException emptyBasketOnCheckoutException)
        {
            //Redirect to Empty Basket page
            _logger.LogWarning(emptyBasketOnCheckoutException.Message);
            return RedirectToPage("/Basket/Index");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);
            return RedirectToPage("/Basket/Index");
        }

        return RedirectToPage("Success");
    }

    private async Task SetBasketModelAsync()
    {
        Guard.Against.Null(User?.Identity?.Name, nameof(User.Identity.Name));
        if (_signInManager.IsSignedIn(HttpContext.User))
        {
            BasketModel = await _basketViewModelService.GetOrCreateBasketForUser(User.Identity.Name);
        }
        else
        {
            GetOrSetBasketCookieAndUserName();
            BasketModel = await _basketViewModelService.GetOrCreateBasketForUser(_username!);
        }
    }

    private void GetOrSetBasketCookieAndUserName()
    {
        if (Request.Cookies.ContainsKey(Constants.BASKET_COOKIENAME))
        {
            _username = Request.Cookies[Constants.BASKET_COOKIENAME];
        }
        if (_username != null) return;

        _username = Guid.NewGuid().ToString();
        var cookieOptions = new CookieOptions();
        cookieOptions.Expires = DateTime.Today.AddYears(10);
        Response.Cookies.Append(Constants.BASKET_COOKIENAME, _username, cookieOptions);
    }
}
