using HavenoSharp.Models;
using HavenoSharp.Models.Requests;
using HavenoSharp.Services;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;

namespace Manta.Components.Pages;

public class AdjustedPrices
{
    public decimal FiatPrice;
    public decimal MoneroAmount;
    public decimal AdjustedMktPrice;
    public decimal FixedPrice;
}

public partial class CreateOffer : ComponentBase, IDisposable
{
    // Should probably be a global at this point
    private const ulong _minSecurityDepositAmount = 100_000_000_000;

    [Inject]
    public IHavenoPaymentAccountService PaymentAccountService { get; set; } = default!;
    [Inject]
    public IHavenoOfferService OfferService { get; set; } = default!;
    [Inject]
    public IHavenoWalletService WalletService { get; set; } = default!;
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;

    [Parameter]
    public string Direction
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            Clear();
        }
    } = string.Empty;

    [Parameter]
    public EventCallback OnCloseCreateOffer { get; set; }
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    // Repeat
    public Dictionary<string, string> CurrencyCodes { get; set; } = [];
    public string SelectedPaymentAccountId
    {
        get;
        set
        {
            field = value;

            if (!string.IsNullOrEmpty(value))
            {
                CurrencyCodes = ProtoPaymentAccounts.FirstOrDefault(x => x.Id == field)?.TradeCurrencies.ToDictionary(x => x.Code, x => $"{x.Name} ({x.Code})") ?? [];
                SelectedCurrencyCode = ProtoPaymentAccounts.FirstOrDefault(x => x.Id == SelectedPaymentAccountId)?.TradeCurrencies.FirstOrDefault()?.Code ?? throw new Exception();

                // Get trade limits
                var paymentMethod = ProtoPaymentAccounts.First(x => x.Id == field).PaymentMethod;
                MaxTradeLimit = ((ulong)paymentMethod.MaxTradeLimit).ToMonero();
            }
            else
            {
                CurrencyCodes = [];
                // Doesnt really clear as the dropdown does not re-render TODO
                SelectedCurrencyCode = "";
            }

            Clear();

            StateHasChanged();
        }
    } = string.Empty;

    public List<PaymentAccount> ProtoPaymentAccounts { get; set; } = [];
    public Dictionary<string, string> PaymentAccounts { get; set; } = [];

    public ulong AvailableXMRBalance { get; set; }
    public bool NoMarketPrice { get; set; }
    public bool IsFiat { get; set; }
    public bool UseMinimumSecurityDeposit { get; set; }

    public string SelectedCurrencyCode
    {
        get;
        set
        {
            field = value;

            IsFiat = Enum.TryParse(typeof(Currency), field, out _);

            if (NoMarketPrice = !BalanceSingleton.MarketPriceInfoDictionary.TryGetValue(field, out var _))
            {
                UseFixedPrice = true;
            }
            else
            {
                UseFixedPrice = false;
            }

            Clear();
        }
    } = string.Empty;

    // Currently does not respect trade limits, TODO
    public decimal MaxTradeLimit { get; set; }
    public decimal MoneroAmount
    {
        get;
        set
        {
            field = value;

            var piconeroAmount = field.ToPiconero();

            if (Direction == "BUY")
            {
                TradeFee = (ulong)(piconeroAmount * AppConstants.MakerFeePct);
                ulong securityDepositAmount = (ulong)(piconeroAmount * (SecurityDepositPct / 100));

                if (securityDepositAmount < _minSecurityDepositAmount)
                {
                    securityDepositAmount = _minSecurityDepositAmount;
                    UseMinimumSecurityDeposit = true;
                }
                else
                {
                    UseMinimumSecurityDeposit = false;
                }

                RequiredFunds = securityDepositAmount + TradeFee;
            }
            else
            {
                var tradeFeePct = BuyerAsTakerWithoutDeposit ? (AppConstants.MakerFeePct + AppConstants.TakerFeePct) : AppConstants.MakerFeePct;

                TradeFee = (ulong)(piconeroAmount * tradeFeePct);
                ulong depositAmount = (ulong)(piconeroAmount * (SecurityDepositPct / 100));

                if (depositAmount < _minSecurityDepositAmount)
                {
                    depositAmount = _minSecurityDepositAmount;
                    UseMinimumSecurityDeposit = true;
                }
                else
                {
                    UseMinimumSecurityDeposit = false;
                }

                RequiredFunds = piconeroAmount + depositAmount + TradeFee;
            }

            if (BalanceSingleton.MarketPriceInfoDictionary.TryGetValue(SelectedCurrencyCode, out var priceForOneXMR))
            {
                TradeFeeFiat = Math.Round(TradeFee.ToMonero() * priceForOneXMR, Enum.TryParse(typeof(Helpers.CryptoCurrency), SelectedCurrencyCode, out _) ? 8 : 2);
            }
        }
    }
    public decimal MinimumMoneroAmount { get; set; }
    public decimal MarketPriceMarginPct { get; set; }
    public decimal FiatPrice { get; set; }
    public decimal FixedPrice { get; set; }
    public bool UseFixedPrice { get; set; }
    public decimal TriggerAmount { get; set; }
    public decimal SecurityDepositPct { get; set; } = 15m;
    public bool BuyerAsTakerWithoutDeposit
    {
        get;
        set
        {
            field = value;
            SecurityDepositPct = 15m;
            // Quick fix to show correct trade fee
            MoneroAmount = MoneroAmount;
        }
    }

    public bool IsFetching { get; set; }
    public bool IsConfirmModalOpen { get; set; }

    public ulong RequiredFunds { get; set; }
    public ulong TradeFee { get; set; }
    public decimal TradeFeeFiat { get; set; }

    public void Clear()
    {
        MinimumMoneroAmount = 0m;
        MoneroAmount = 0m;
        FiatPrice = 0m;
        MarketPriceMarginPct = 0m;
        TriggerAmount = 0;
        FixedPrice = 0;
        SecurityDepositPct = 15m;
        TradeFee = 0;
        UseFixedPrice = false;
        BuyerAsTakerWithoutDeposit = false;
    }

    // Gives the adjusted amounts so that the fiat value is a whole number
    public AdjustedPrices GetAdjustedPrices(decimal unadjustedMoneroAmount, decimal priceForOneXMR, decimal marketPriceMarginPct, bool isFiat)
    {
        decimal adjustedMktPrice;
        var percent = marketPriceMarginPct / 100m;

        if (Direction == "BUY")
        {
            adjustedMktPrice = priceForOneXMR - (priceForOneXMR * percent);
        }
        else
        {
            adjustedMktPrice = priceForOneXMR + (priceForOneXMR * percent);
        }

        var adjustedPrices = new AdjustedPrices
        {
            FiatPrice = isFiat ? Math.Round(adjustedMktPrice * unadjustedMoneroAmount) : Math.Round(adjustedMktPrice * unadjustedMoneroAmount, 8),
            AdjustedMktPrice = isFiat ? Math.Round(adjustedMktPrice, 4) : Math.Round(adjustedMktPrice, 8)
        };

        if (adjustedMktPrice != 0m)
        {
            adjustedPrices.MoneroAmount = Math.Round(adjustedPrices.FiatPrice / adjustedMktPrice, 4);
        }
        else
        {
            adjustedPrices.MoneroAmount = 0m;
        }

        return adjustedPrices;
    }

    public void Calculate(decimal value, string inputName)
    {
        BalanceSingleton.MarketPriceInfoDictionary.TryGetValue(SelectedCurrencyCode, out var priceForOneXMR);

        switch (inputName)
        {
            case "MoneroAmount":
                {
                    if (NoMarketPrice)
                    {
                        MoneroAmount = value;
                    }
                    else
                    {
                        var adjustedPrices = GetAdjustedPrices(value, priceForOneXMR, MarketPriceMarginPct, IsFiat);

                        MoneroAmount = adjustedPrices.MoneroAmount;
                        FiatPrice = adjustedPrices.FiatPrice;

                        if (IsFiat)
                            FixedPrice = adjustedPrices.AdjustedMktPrice;
                        else
                        {
                            FixedPrice = adjustedPrices.AdjustedMktPrice;
                            FiatPrice = Math.Round(FixedPrice * MoneroAmount, 8);
                        }
                    }

                    if (MoneroAmount < MinimumMoneroAmount || MinimumMoneroAmount == 0m)
                    {
                        MinimumMoneroAmount = MoneroAmount;
                    }
                }
                break;
            case "MinimumMoneroAmount":
                {
                    AdjustedPrices? adjustedPrices = null;

                    if (NoMarketPrice)
                    {
                        MinimumMoneroAmount = value;
                    }
                    else
                    {
                        adjustedPrices = GetAdjustedPrices(value, priceForOneXMR, MarketPriceMarginPct, IsFiat);

                        MinimumMoneroAmount = adjustedPrices.MoneroAmount;
                    }

                    if (MinimumMoneroAmount > MoneroAmount)
                    {
                        MoneroAmount = MinimumMoneroAmount;

                        if (adjustedPrices is not null)
                        {
                            if (IsFiat)
                                FixedPrice = adjustedPrices.AdjustedMktPrice;
                            else
                            {
                                FixedPrice = adjustedPrices.AdjustedMktPrice;
                                FiatPrice = Math.Round(FixedPrice * MoneroAmount, 8);
                            }
                        }
                    }
                }
                break;
            case "FixedPrice":
                {
                    if (!UseFixedPrice)
                        return;

                    if (NoMarketPrice)
                    {
                        priceForOneXMR = value;
                    }

                    if (value == 0)
                        return;

                    if (true)
                    {
                        var percent = value / priceForOneXMR;

                        if (Direction == "BUY")
                        {
                            MarketPriceMarginPct = -(Math.Round(percent - 1m, 4) * 100m);
                        }
                        else
                        {
                            MarketPriceMarginPct = Math.Round(percent - 1m, 4) * 100m;
                        }

                        if (IsFiat)
                        {
                            FiatPrice = Math.Round(MoneroAmount * value);
                        }
                        else
                        {
                            FiatPrice = MoneroAmount * value;
                        }

                        if (MoneroAmount == MinimumMoneroAmount)
                        {
                            MoneroAmount = Math.Round(FiatPrice / value, 4);
                            MinimumMoneroAmount = MoneroAmount;
                        }
                        else
                        {
                            MoneroAmount = Math.Round(FiatPrice / value, 4);

                            if (MoneroAmount < MinimumMoneroAmount || MinimumMoneroAmount == 0m)
                            {
                                MinimumMoneroAmount = MoneroAmount;
                            }
                        }
                    }
                    else
                    {
                        var oneCryptoInXMR = 1 / priceForOneXMR;
                        MarketPriceMarginPct = Math.Round(((value / oneCryptoInXMR) - 1) * 100, 2);
                        FiatPrice = Math.Round(1 / FixedPrice * MoneroAmount, 8);
                    }
                }
                break;
            case "MarketPriceMarginPct":
                {
                    if (UseFixedPrice)
                        return;

                    if (value >= 100m)
                        value = 99.99m;

                    decimal adjustedMktPrice;
                    var percent = MarketPriceMarginPct / 100m;

                    if (true)
                    {
                        if (Direction == "BUY")
                        {
                            adjustedMktPrice = priceForOneXMR - (priceForOneXMR * percent);
                        }
                        else
                        {
                            adjustedMktPrice = priceForOneXMR + (priceForOneXMR * percent);
                        }

                        FixedPrice = adjustedMktPrice;

                        if (IsFiat)
                        {
                            FiatPrice = Math.Round(adjustedMktPrice * MoneroAmount);
                        }
                        else
                        {
                            FiatPrice = adjustedMktPrice * MoneroAmount;
                        }

                        if (MoneroAmount == MinimumMoneroAmount)
                        {
                            MoneroAmount = Math.Round(FiatPrice / adjustedMktPrice, IsFiat ? 4 : 8);
                            MinimumMoneroAmount = MoneroAmount;
                        }
                        else
                        {
                            MoneroAmount = Math.Round(FiatPrice / adjustedMktPrice, IsFiat ? 4 : 8);

                            if (MoneroAmount < MinimumMoneroAmount || MinimumMoneroAmount == 0m)
                            {
                                MinimumMoneroAmount = MoneroAmount;
                            }
                        }
                    }
                    //else
                    //{
                    //    if (Direction == "BUY")
                    //    {
                    //        var oneCryptoInXMR = 1 / priceForOneXMR;
                    //        FixedPrice = Math.Round(oneCryptoInXMR + (oneCryptoInXMR * percent), 8);
                    //        FiatPrice = Math.Round(1 / FixedPrice * MoneroAmount, 8);
                    //    }
                    //    else
                    //    {
                    //        var oneCryptoInXMR = 1 / priceForOneXMR;
                    //        FixedPrice = Math.Round(oneCryptoInXMR - (oneCryptoInXMR * percent), 8);
                    //        FiatPrice = Math.Round(1 / FixedPrice * MoneroAmount, 8);
                    //    }
                    //}
                }
                break;
            case "FiatPrice":
                {
                    var fiatPrice = IsFiat ? Math.Round(FiatPrice) : Math.Round(FiatPrice, 8);

                    if (NoMarketPrice)
                    {
                        priceForOneXMR = FixedPrice;
                    }

                    decimal adjustedMktPrice;

                    var percent = MarketPriceMarginPct / 100m;

                    if (UseFixedPrice)
                    {
                        adjustedMktPrice = FixedPrice;
                    }
                    else
                    {
                        if (Direction == "BUY")
                        {
                            adjustedMktPrice = priceForOneXMR - (priceForOneXMR * percent);
                        }
                        else
                        {
                            adjustedMktPrice = priceForOneXMR + (priceForOneXMR * percent);
                        }
                    }

                    if (MoneroAmount == MinimumMoneroAmount)
                    {
                        MoneroAmount = Math.Round(fiatPrice / adjustedMktPrice, 4);
                        MinimumMoneroAmount = MoneroAmount;
                    }
                    else
                    {
                        MoneroAmount = Math.Round(fiatPrice / adjustedMktPrice, 4);

                        if (MoneroAmount < MinimumMoneroAmount || MinimumMoneroAmount == 0m)
                        {
                            MinimumMoneroAmount = MoneroAmount;
                        }
                    }
                }
                break;
            default: break;
        }
    }

    public async void HandleBalanceFetch(bool isFetching)
    {
        // Or invoke on UI thread?
        await InvokeAsync(() =>
        {
            if (!isFetching)
            {
                AvailableXMRBalance = BalanceSingleton.WalletInfo!.AvailableXMRBalance;
                StateHasChanged();
            }
        });
    }

    protected override async Task OnInitializedAsync()
    {
        var paymentAccounts = await PaymentAccountService.GetPaymentAccountsAsync();
        PaymentAccounts = paymentAccounts.ToDictionary(x => x.Id, x => x.AccountName);
        ProtoPaymentAccounts = [.. paymentAccounts];

        if (ProtoPaymentAccounts.Count != 0)
        {
            SelectedPaymentAccountId = PaymentAccounts.Select(x => x.Key).FirstOrDefault() ?? string.Empty;
            SelectedCurrencyCode = paymentAccounts.FirstOrDefault(x => x.Id == SelectedPaymentAccountId)?.TradeCurrencies.FirstOrDefault()?.Code ?? throw new Exception();

            // Get trade limits
            var paymentMethod = paymentAccounts.First(x => x.Id == SelectedPaymentAccountId).PaymentMethod;
            MaxTradeLimit = ((ulong)paymentMethod.MaxTradeLimit).ToMonero();
        }

        if (BalanceSingleton.WalletInfo is not null)
        {
            AvailableXMRBalance = BalanceSingleton.WalletInfo.AvailableXMRBalance;
        }

        BalanceSingleton.OnBalanceFetch += HandleBalanceFetch;

        await base.OnInitializedAsync();
    }

    public async Task PostOfferAsync()
    {
        try
        {
            IsFetching = true;

            var request = new PostOfferRequest
            {
                Amount = MoneroAmount.ToPiconero(),
                MinAmount = MinimumMoneroAmount.ToPiconero(),
                PaymentAccountId = SelectedPaymentAccountId,
                CurrencyCode = SelectedCurrencyCode,
                Direction = Direction,
                SecurityDepositPct = (double)(SecurityDepositPct / 100m),
                TriggerPrice = TriggerAmount.ToString(),
                MarketPriceMarginPct = 0,
                Price = "",
                UseMarketBasedPrice = true,
                BuyerAsTakerWithoutDeposit = BuyerAsTakerWithoutDeposit
            };

            if (BuyerAsTakerWithoutDeposit)
                request.IsPrivateOffer = true;

            if (UseFixedPrice)
            {
                request.Price = FixedPrice.ToString();
                request.UseMarketBasedPrice = false;
            }
            else
            {
                if (MarketPriceMarginPct == 0m)
                {
                    request.MarketPriceMarginPct = 0;
                }
                else
                {
                    request.MarketPriceMarginPct = (double)MarketPriceMarginPct;
                }
            }

            var response = await OfferService.PostOfferAsync(request);

            NavigationManager.NavigateTo("buysell/myoffers?title=My%20Offers");
        }
        catch
        {
            throw;
        }
        finally
        {
            IsFetching = false;
        }
    }

    public void Dispose()
    {
        BalanceSingleton.OnBalanceFetch -= HandleBalanceFetch;
    }
}
