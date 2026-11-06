using HavenoSharp.Models;
using HavenoSharp.Services;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;

namespace Manta.Components.Pages;

public partial class MyOffers : ComponentBase
{
    [Inject]
    public IHavenoOfferService OfferService { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;
    [Inject]
    public IHavenoPaymentAccountService PaymentAccountService { get; set; } = default!;
    public List<OfferInfo> Offers { get; set; } = [];
    public List<PaymentAccount> PaymentAccounts { get; set; } = [];

    public OfferInfo? OfferInfo { get; set; }
    public bool ShowExtraInfoModal { get; set; }
    public int TabIndex { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Offers = await OfferService.GetMyOffersAsync("", "");
        PaymentAccounts = await PaymentAccountService.GetPaymentAccountsAsync();

        await base.OnInitializedAsync();
    }

    public async Task CancelOfferAsync(string id)
    {
        try
        {
            await OfferService.CancelOfferAsync(id);

            var offer = Offers.FirstOrDefault(x => x.Id == id);
            if (offer is null)
                return;

            Offers.Remove(offer);
        }
        catch
        {

        }
    }

    public void EditOffer()
    {
        NavigationManager.NavigateTo("/buysell?title=Buy%20%26%20Sell");
    }
}
