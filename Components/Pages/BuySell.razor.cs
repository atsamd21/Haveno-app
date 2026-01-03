using HavenoSharp.Models;
using HavenoSharp.Services;
using Manta.Components.Reusable;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Globalization;

namespace Manta.Components.Pages;

public partial class BuySell : ComponentBase, IDisposable
{
    [Inject]
    public IHavenoPaymentAccountService PaymentAccountService { get; set; } = default!;
    [Inject]
    public IHavenoOfferService OfferService { get; set; } = default!;
    [Inject]
    public IJSRuntime JS { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;

    public CancellationTokenSource CancellationTokenSource { get; set; } = new();

    public List<OfferInfo> FilteredOffers { get { return FilterOffers(Offers); } }
    public List<OfferInfo> Offers { get; set; } = [];

    public bool IsFetching { get; set; }

    // Does not update when these change
    public string SelectedCurrencyCode
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                AppPreferences.Set(AppPreferences.SelectedCurrencyCode, value);
                ResetFetch();
            }
        }
    } = string.Empty;

    public string SelectedPaymentMethod
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                AppPreferences.Set(AppPreferences.SelectedPaymentMethod, value);
                ResetFetch();
            }
        }
    } = string.Empty;

    // Needs to be fetched from the users preferences
    public Dictionary<string, string> TraditionalCurrencyCodes { get; set; } = CurrencyCultureInfo.GetCurrencyFullNamesAndCurrencyCodeDictionary().ToDictionary();
    public Dictionary<string, string> CryptoCurrencyCodes { get; set; } = CryptoCurrencyHelper.CryptoCurrenciesDictionary;
    public Dictionary<string, string> VisibleCurrencyCodes { get; set; } = [];
    public Dictionary<string, string> TraditionalPaymentMethods { get; set; } = [];
    public Dictionary<string, string> CryptoPaymentMethods { get; set; } = [];
    public Dictionary<string, string> VisiblePaymentMethods { get; set; } = [];

    public bool IsCreatingOffer { get; set; }
    public int OfferPaymentType
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            switch (field)
            {
                case 0:
                    VisiblePaymentMethods = TraditionalPaymentMethods;
                    VisibleCurrencyCodes = TraditionalCurrencyCodes;
                    break;
                case 1:
                    VisiblePaymentMethods = CryptoPaymentMethods;
                    VisibleCurrencyCodes = CryptoCurrencyCodes;
                    break;
                case 2:
                    break;
                default: break;
            }

            // Clear these as the different tabs don't support the same options
            CurrencySearchableDropdown?.Clear();
            PaymentMethodSearchableDropdown?.Clear();
            SelectedCurrencyCode = string.Empty;
            SelectedPaymentMethod = string.Empty;

            AppPreferences.Set(AppPreferences.OfferPaymentType, value);
        }
    }

    public bool ShowNoDepositOffers 
    { 
        get; 
        set 
        {
            if (field == value)
                return;

            field = value; 
            AppPreferences.Set(AppPreferences.ShowNoDepositOffers, value); 
        } 
    }

    public string Direction 
    { 
        get; 
        set 
        {
            field = value;

            //if (value == "SELL" && ShowNoDepositOffers)
            //{
            //    ShowNoDepositOffers = false;
            //}
        } 
    } = "BUY";

    public Task? OfferFetchTask;

    public bool IsToggled
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            AppPreferences.Set(AppPreferences.IsToggled, value);
            Direction = value ? "SELL" : "BUY";
            ResetFetch();
        }
    }

    public string PreferredCurrency { get; set; } = string.Empty;
    // TODO fetch this on rerender
    public string CurrentMarketPrice { get; set; } = string.Empty;
    public NumberFormatInfo PreferredCurrencyFormat { get; set; } = default!;

    public SearchableDropdown? CurrencySearchableDropdown { get; set; } = default;
    public SearchableDropdown? PaymentMethodSearchableDropdown { get; set; } = default;

    public bool IsCollapsed { get; set; } = true;

    public void CloseCreateOffer()
    {
        IsCreatingOffer = false;
    }

    public SemaphoreSlim ResetSemaphore { get; set; } = new(1);
    public void ResetFetch()
    {
        if (!ResetSemaphore.Wait(0))
            return;

        Task.Run(async() => {
            CancellationTokenSource.Cancel();

            if (OfferFetchTask is not null)
                await OfferFetchTask;

            CancellationTokenSource.Dispose();
            CancellationTokenSource = new();

            OfferFetchTask = Task.Run(FetchOffersAsync);

            ResetSemaphore.Release();
        });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        while (true)
        {
            try
            {
                var paymentMethods = await PaymentAccountService.GetPaymentMethodsAsync();
                var filteredPaymentMethodIds = paymentMethods
                    .Select(x => x.Id);

                TraditionalPaymentMethods = PaymentMethodsHelper.PaymentMethodsDictionary
                    .Where(x => filteredPaymentMethodIds.Contains(x.Key)).ToDictionary();

                var cryptoPaymentMethods = await PaymentAccountService.GetCryptoCurrencyPaymentMethodsAsync();

                var filteredCryptoPaymentMethodIds = cryptoPaymentMethods
                    .Select(x => x.Id);

                CryptoPaymentMethods = PaymentMethodsHelper.PaymentMethodsDictionary
                    .Where(x => filteredCryptoPaymentMethodIds.Contains(x.Key)).ToDictionary();

                PreferredCurrency = AppPreferences.Get<Currency>(AppPreferences.PreferredCurrency).ToString();
                PreferredCurrencyFormat = CurrencyCultureInfo.GetFormatForCurrency((Currency)Enum.Parse(typeof(Currency), PreferredCurrency))!;

                try
                {
                    // If there is no price data for this currency then fallback to USD
                    CurrentMarketPrice = BalanceSingleton.MarketPriceInfoDictionary[PreferredCurrency].ToString("0.00");
                }
                catch (KeyNotFoundException)
                {
                    CurrentMarketPrice = BalanceSingleton.MarketPriceInfoDictionary[CurrencyCultureInfo.FallbackCurrency].ToString("0.00");
                    PreferredCurrency = CurrencyCultureInfo.FallbackCurrency;
                    PreferredCurrencyFormat = CurrencyCultureInfo.GetFormatForCurrency((Currency)Enum.Parse(typeof(Currency), PreferredCurrency))!;
                }

                VisiblePaymentMethods = TraditionalPaymentMethods;
                VisibleCurrencyCodes = TraditionalCurrencyCodes;

                OfferFetchTask = Task.Run(FetchOffersAsync);

                break;
            }
            catch
            {

            }

            await Task.Delay(5_000);
        }

        OfferPaymentType = AppPreferences.Get<int?>(AppPreferences.OfferPaymentType) ?? 0;
        IsToggled = AppPreferences.Get<bool?>(AppPreferences.IsToggled) ?? false;
        SelectedCurrencyCode = AppPreferences.Get<string?>(AppPreferences.SelectedCurrencyCode) ?? string.Empty;
        SelectedPaymentMethod = AppPreferences.Get<string?>(AppPreferences.SelectedPaymentMethod) ?? string.Empty;
        ShowNoDepositOffers = true;

        await base.OnInitializedAsync();
    }

    private List<OfferInfo> FilterOffers(List<OfferInfo> offers)
    {
        IEnumerable<OfferInfo> filteredOffers;

        if (!string.IsNullOrEmpty(SelectedPaymentMethod))
        {
            filteredOffers = offers.Where(x => x.PaymentMethodId == SelectedPaymentMethod);
        }
        else
        {
            switch (OfferPaymentType)
            {
                case 0:
                    filteredOffers = offers.Where(x => TraditionalPaymentMethods.ContainsKey(x.PaymentMethodId)).Where(x => x.PaymentMethodId != "BLOCK_CHAINS");
                    break;
                case 1:
                    filteredOffers = offers.Where(x => CryptoPaymentMethods.ContainsKey(x.PaymentMethodId));
                    break;
                case 2:
                    filteredOffers = offers.Where(x => !TraditionalPaymentMethods.ContainsKey(x.PaymentMethodId)).Where(x => x.PaymentMethodId != "BLOCK_CHAINS");
                    break;
                default:
                    filteredOffers = offers;
                    break;
            }
        }

        if (!ShowNoDepositOffers)
        {
            filteredOffers = filteredOffers.Where(x => x.BuyerSecurityDepositPct > 0);
        }

        return [.. filteredOffers.OrderBy(x => x.MarketPriceMarginPct)];
    }

    private async Task FetchOffersAsync()
    {
        while (true)
        {
            try
            {
                await PauseTokenSource.WaitWhilePausedAsync();

                IsFetching = true;
                await InvokeAsync(StateHasChanged);

                Offers = await OfferService.GetOffersAsync(SelectedCurrencyCode, Direction == "BUY" ? "SELL" : "BUY");
                Console.WriteLine($"Fetched {Offers.Count} offers");

                IsFetching = false;
                await InvokeAsync(StateHasChanged);

                await Task.Delay(5_000, CancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                try
                {
                    await Task.Delay(5_000, CancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    public void NavigateToMyOffers()
    {
        NavigationManager.NavigateTo("buysell/myoffers?title=My%20Offers");
    }

    public void Dispose()
    {
        CancellationTokenSource.Cancel();
        CancellationTokenSource.Dispose();
    }
}