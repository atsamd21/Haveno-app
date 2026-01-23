using Grpc.Core;
using HavenoSharp.Extensions;
using HavenoSharp.Models;
using HavenoSharp.Models.Requests;
using HavenoSharp.Services;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace Manta.Components.Pages;

public partial class Offer : ComponentBase, IDisposable
{
    [Parameter]
    [SupplyParameterFromQuery]
    public string OfferId { get; set; } = string.Empty;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;
    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;
    [Inject]
    public IHavenoOfferService OfferService { get; set; } = default!;
    [Inject]
    public IHavenoPaymentAccountService PaymentAccountService { get; set; } = default!;
    [Inject]
    public IHavenoTradeService TradeService { get; set; } = default!;

    public bool ShowExtraInfoModal { get; set; }
    public bool ShowPassphraseModal { get; set; }

    public string Passphrase { get; set { field = value.Trim(); } } = string.Empty;

    public OfferInfo? OfferInfo { get; set; }

    private ulong _piconeroAmount;
    public decimal MoneroAmount 
    {
        get; 
        set 
        {
            if (value > OfferInfo!.Amount.ToMonero())
            {
                field = OfferInfo.Amount.ToMonero();
            }
            else if (value < OfferInfo.MinAmount.ToMonero())
            {
                field = OfferInfo.MinAmount.ToMonero();
            }
            else
            {
                if (OfferInfo.PaymentMethodId != "BLOCK_CHAINS")
                {
                    var roundedFiatAmount = Math.Round(decimal.Parse(OfferInfo.Price, CultureInfo.InvariantCulture) * value);
                    field = Math.Round(roundedFiatAmount / decimal.Parse(OfferInfo.Price, CultureInfo.InvariantCulture), 4);
                }
                else
                {
                    field = value;
                }
            }

            _piconeroAmount = field.ToPiconero();

            if (OfferInfo.BuyerSecurityDepositPct > 0)
            {
                if (OfferInfo.Direction == "BUY")
                {
                    ulong transactionFee = 0;
                    ulong takerFee = (ulong)(_piconeroAmount * OfferInfo.TakerFeePct);
                    ulong depositAmount = (ulong)(_piconeroAmount * OfferInfo.SellerSecurityDepositPct);
                    if (depositAmount < 100_000_000_000)
                    {
                        depositAmount = 100_000_000_000;
                    }

                    RequiredFunds = _piconeroAmount + transactionFee + depositAmount + takerFee;
                }
                else
                {
                    ulong transactionFee = 0;
                    ulong takerFee = (ulong)(_piconeroAmount * OfferInfo.TakerFeePct);
                    ulong depositAmount = (ulong)(_piconeroAmount * OfferInfo.BuyerSecurityDepositPct);
                    if (depositAmount < 100_000_000_000)
                    {
                        depositAmount = 100_000_000_000;
                    }

                    RequiredFunds = transactionFee + depositAmount + takerFee;
                }
            }

            if (OfferInfo is not null)  // Price changes...
                CounterCurrencyAmount = decimal.Parse(OfferInfo.Price, CultureInfo.InvariantCulture) * MoneroAmount;
        } 
    }

    public decimal CounterCurrencyAmount 
    {
        get; 
        set
        {
            if (OfferInfo?.PaymentMethodId == "BLOCK_CHAINS")
            {
                field = Math.Round(value, 8); 
            }
            else
            {
                field = Math.Round(value);
            }
        } 
    }

    public string SelectedPaymentAccountId { get; set; } = string.Empty;
    public Dictionary<string, string> PaymentAccounts { get; set; } = [];

    public bool IsTakingOffer { get; set; }
    public bool AnimateTimeout { get; set; }
    public bool UserDoesNotHaveAccount { get; set; }
    public bool OfferError { get; set; }
    public string? OfferErrorMessage { get; set; }
    public ulong RequiredFunds { get; set; }
    public string? AccountToCreate { get; set; }
    public string? AccountErrorMessage { get; set; }
    public ulong AvailableBalance { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            try
            {
                OfferInfo = await OfferService.GetOfferAsync(OfferId);
            }
            catch (RpcException e)
            {
                OfferError = true;
                OfferErrorMessage = $"{e.GetErrorMessage()}";
                return;
            }

            ShowExtraInfoModal = !string.IsNullOrEmpty(OfferInfo.ExtraInfo);

            var paymentAccounts = await PaymentAccountService.GetPaymentAccountsAsync();

            var sameTypePaymentAccounts = paymentAccounts
                .Where(x => x.PaymentMethod.Id == OfferInfo.PaymentMethodId)
                .ToList();

            if (sameTypePaymentAccounts.Count == 0)
            {
                UserDoesNotHaveAccount = true;
                AccountToCreate = OfferInfo.PaymentMethodId;
                AccountErrorMessage = $"You do not have an account of this type ({OfferInfo.PaymentMethodShortName}).";
                return;
            }

            sameTypePaymentAccounts = sameTypePaymentAccounts
                .Where(x => x.TradeCurrencies.Select(x => x.Code).Contains(OfferInfo.CounterCurrencyCode))
                .ToList();

            SelectedPaymentAccountId = sameTypePaymentAccounts
                .FirstOrDefault(x => x.PaymentMethod.Id == OfferInfo.PaymentMethodId)?.Id ?? string.Empty;

            if (string.IsNullOrEmpty(SelectedPaymentAccountId))
            {
                UserDoesNotHaveAccount = true;
                AccountErrorMessage = $"You do not have a {OfferInfo.PaymentMethodShortName} account that supports this currency ({OfferInfo.CounterCurrencyCode}).";
                AccountToCreate = OfferInfo.PaymentMethodId;
                return;
            }

            PaymentAccounts = sameTypePaymentAccounts.ToDictionary(x => x.Id, x => x.AccountName);

            AvailableBalance = BalanceSingleton.WalletInfo?.AvailableXMRBalance ?? 0;

            if (OfferInfo.PaymentMethodId != "BLOCK_CHAINS")
            {
                var roundedFiatAmount = Math.Round(decimal.Parse(OfferInfo.Price, CultureInfo.InvariantCulture) * OfferInfo.Amount.ToMonero());
                MoneroAmount = Math.Round(roundedFiatAmount / decimal.Parse(OfferInfo.Price, CultureInfo.InvariantCulture), 4);
            }
            else
            {
                MoneroAmount = OfferInfo.Amount.ToMonero();
            }
            
            BalanceSingleton.OnBalanceFetch += HandleBalanceFetch;
        }
        catch
        {
            Cancel();
        }

        await base.OnInitializedAsync();
    }

    public async void HandleBalanceFetch(bool isFetching)
    {
        if (!isFetching)
        {
            await InvokeAsync(() => {
                if (OfferInfo is null)
                    return;

                AvailableBalance = BalanceSingleton.WalletInfo?.AvailableXMRBalance ?? 0;
                StateHasChanged();
            });
        }
    }

    public async Task OpenNextTakeOfferStep()
    {
        if (OfferInfo is null)
            return;

        if (OfferInfo.BuyerSecurityDepositPct == 0)
        {
            ShowPassphraseModal = true;
        }
        else
        {
            await TakeOfferAsync();
        }
    }

    public async Task TakeOfferAsync()
    {
        if (OfferInfo is null)
            return;

        ShowPassphraseModal = false;
        IsTakingOffer = true;

        await InvokeAsync(() => AnimateTimeout = true);

        try
        {
            var takeOfferRequest = new TakeOfferRequest
            {
                OfferId = OfferInfo.Id,
                Amount = _piconeroAmount,
                PaymentAccountId = SelectedPaymentAccountId,
                Challenge = Passphrase
            };

            var response = await TradeService.TakeOfferAsync(takeOfferRequest);

            NotificationSingleton.TradeInfos.TryAdd(response.Trade.TradeId, response.Trade);

            NavigationManager.NavigateTo("Trades");
        }
        catch (OperationCanceledException)
        {
            // Show user it was canceled
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            IsTakingOffer = false;
            AnimateTimeout = false;
        }
    }

    public void NavigateToAccount()
    {
        NavigationManager.NavigateTo($"account?accountToCreate={AccountToCreate}&title=Account");
    }

    public void Cancel()
    {
        NavigationManager.NavigateTo("buysell?title=Buy%20%26%20Sell");
    }

    public void Dispose()
    {
        BalanceSingleton.OnBalanceFetch -= HandleBalanceFetch;
    }
}
