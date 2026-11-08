using HavenoSharp.Models;
using HavenoSharp.Models.Requests;
using HavenoSharp.Services;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace Manta.Components.Pages;

public partial class EditOffer : ComponentBase
{
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    public IHavenoOfferService OfferService { get; set; } = default!;
    [Inject]
    public IHavenoPaymentAccountService PaymentAccountService { get; set; } = default!;
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;

    [Parameter]
    [SupplyParameterFromQuery]
    public string OfferId { get; set; } = string.Empty;

    public OfferInfo? OfferInfo { get; set; }
    public PaymentAccount? PaymentAccount { get; set; }

    public decimal MarketPrice { get; set; }

    public bool IsConfirmModalOpen { get; set; }
    public bool IsPostingOffer { get; set; }

    public decimal? TriggerPrice { get; set; }

    private decimal _price;
    public decimal Price
    {
        get
        {
            return _price;
        }
        set
        {
            value = Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);

            TotalCounterCurrency = Math.Round(
                OfferInfo.Amount.ToMonero() * value,
                MidpointRounding.AwayFromZero) + 0.0000m;

            _price = Math.Round(
                TotalCounterCurrency / OfferInfo.Amount.ToMonero(),
                DecimalPlaces, 
                MidpointRounding.AwayFromZero) + 0.0000m;

            if (OfferInfo.Direction == "BUY")
            {
                _marketPriceMarginPct = Math.Round((1m - (_price / BalanceSingleton.MarketPriceInfoDictionary[OfferInfo.CounterCurrencyCode])) * 100m, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                _marketPriceMarginPct = Math.Round(((_price / BalanceSingleton.MarketPriceInfoDictionary[OfferInfo.CounterCurrencyCode]) - 1m) * 100m, 2, MidpointRounding.AwayFromZero);
            }
        }
    }

    public decimal TotalCounterCurrency { get; set; }

    private decimal _marketPriceMarginPct;
    public decimal MarketPriceMarginPct
    {
        get
        {
            return _marketPriceMarginPct;
        }
        set
        {
            _marketPriceMarginPct = value;

            if (OfferInfo.Direction == "BUY")
            {
                _price = Math.Round((1m - (value / 100m)) * MarketPrice,
                    DecimalPlaces,
                    MidpointRounding.AwayFromZero) + 0.0000m;
            }
            else
            {
                _price = Math.Round((1m + (value / 100m)) * MarketPrice,
                    DecimalPlaces,
                    MidpointRounding.AwayFromZero) + 0.0000m;
            }

            TotalCounterCurrency = Math.Round(
                _price * OfferInfo.Amount.ToMonero(),
                MidpointRounding.AwayFromZero);
        }
    }

    public int DecimalPlaces { get; set; }

    protected override async Task OnInitializedAsync()
    {
        OfferInfo = await OfferService.GetMyOfferAsync(OfferId);
        var paymentAccounts = await PaymentAccountService.GetPaymentAccountsAsync();

        PaymentAccount = paymentAccounts.FirstOrDefault(x => x.Id == OfferInfo.PaymentAccountId);
        DecimalPlaces = OfferInfo.IsFiat() ? 4 : 8;

        _marketPriceMarginPct = (decimal)OfferInfo.MarketPriceMarginPct * 100m;

        _price = Math.Round(
            decimal.Parse(OfferInfo.Price, CultureInfo.InvariantCulture), 
            DecimalPlaces, 
            MidpointRounding.AwayFromZero);

        TotalCounterCurrency = Math.Round(
            decimal.Parse(OfferInfo.Volume, CultureInfo.InvariantCulture), 
            DecimalPlaces, 
            MidpointRounding.AwayFromZero);

        if (OfferInfo.TriggerPrice != "0")
        {
            TriggerPrice = Math.Round(
                decimal.Parse(OfferInfo.TriggerPrice, CultureInfo.InvariantCulture), 
                DecimalPlaces, 
                MidpointRounding.AwayFromZero);
        }

        if (OfferInfo.UseMarketBasedPrice)
        {
            MarketPrice = BalanceSingleton.MarketPriceInfoDictionary[OfferInfo.CounterCurrencyCode];
        }
        else
        {
            if (OfferInfo.Direction == "BUY")
            {
                _marketPriceMarginPct = Math.Round((1m - (_price / BalanceSingleton.MarketPriceInfoDictionary[OfferInfo.CounterCurrencyCode])) * 100m, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                _marketPriceMarginPct = Math.Round(((_price / BalanceSingleton.MarketPriceInfoDictionary[OfferInfo.CounterCurrencyCode]) - 1m) * 100m, 2, MidpointRounding.AwayFromZero);
            }
        }

        await base.OnInitializedAsync();
    }

    public void OnCloseCreateOffer()
    {
        NavigationManager.NavigateTo("buysell/myoffers?title=My%20Offers");
    }

    public async Task EditOfferAsync()
    {
        IsPostingOffer = true;

        var request = new EditOfferRequest
        {
            PaymentAccountId = OfferInfo.PaymentAccountId,
            CurrencyCode = OfferInfo.CounterCurrencyCode,
            UseMarketBasedPrice = OfferInfo.UseMarketBasedPrice,
            OfferId = OfferInfo.Id,
        };

        if (TriggerPrice is not null && TriggerPrice.Value != 0)
        {
            request.TriggerPrice = TriggerPrice.Value.ToString();
        }

        if (OfferInfo.UseMarketBasedPrice)
        {
            request.MarketPriceMarginPct = (double)(MarketPriceMarginPct / 100m);
        }
        else
        {
            request.Price = Price.ToString();
        }

        var response = await OfferService.EditOffer(request);
        
        NavigationManager.NavigateTo("buysell/myoffers?title=My%20Offers");
    }
}
