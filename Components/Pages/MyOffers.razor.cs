using HavenoSharp.Models;
using HavenoSharp.Services;
using Microsoft.AspNetCore.Components;

namespace Manta.Components.Pages;

public partial class MyOffers : ComponentBase
{
    [Inject]
    public IHavenoOfferService OfferService { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    public List<OfferInfo> Offers { get; set; } = [];

    public OfferInfo? OfferInfo { get; set; }
    public bool ShowExtraInfoModal { get; set; }

    protected override async Task OnInitializedAsync()
    {
        while (true)
        {
            try
            {
                Offers = await OfferService.GetMyOffersAsync("", "");

                // Why does this not get set?
                Offers.ForEach(x => x.IsMyOffer = true);

                break;
            }
            catch
            {

            }

            await Task.Delay(5_000);
        }

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
