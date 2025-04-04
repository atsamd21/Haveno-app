﻿using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;

using static Haveno.Proto.Grpc.Offers;
using static Haveno.Proto.Grpc.PaymentAccounts;
using static Haveno.Proto.Grpc.Trades;

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

    public OfferInfo? OfferInfo { get; set; }

    private ulong _piconeroAmount;
    public decimal Amount 
    {
        get; 
        set 
        { 
            field = value; 
            _piconeroAmount = value.ToPiconero();
            if (OfferInfo is not null)  // Price changes...
                FiatAmount = decimal.Parse(OfferInfo.Price) * Amount;
        } 
    }

    public decimal FiatAmount { get { return Math.Round(field, 0); } set { field = value; } }
    public string SelectedPaymentAccountId { get; set; } = string.Empty;
    public Dictionary<string, string> PaymentAccounts { get; set; } = [];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        using var grpcChannelHelper = new GrpcChannelHelper();
        OffersClient offersClient = new(grpcChannelHelper.Channel);

        var offerResponse = await offersClient.GetOfferAsync(new GetOfferRequest { Id = OfferId });
        OfferInfo = offerResponse.Offer;

        FiatAmount = decimal.Parse(OfferInfo.Price) * Amount;

        var paymentAccountsClient = new PaymentAccountsClient(grpcChannelHelper.Channel);

        var paymentAccountResponse = await paymentAccountsClient.GetPaymentAccountsAsync(new GetPaymentAccountsRequest());
        PaymentAccounts = paymentAccountResponse.PaymentAccounts.ToDictionary(x => x.Id, x => x.AccountName);

        SelectedPaymentAccountId = PaymentAccounts.FirstOrDefault(x => x.Value == OfferInfo.PaymentMethodId).Key;
        // If we don't have an account of this type we should prompt to make one - TODO

        Amount = OfferInfo.Amount.ToMonero();

        await base.OnInitializedAsync();
    }

    public async Task TakeOfferAsync()
    {
        using var grpcChannelHelper = new GrpcChannelHelper();
        var tradesClient = new TradesClient(grpcChannelHelper.Channel);

        var takeOfferRequest = new TakeOfferRequest
        {
            OfferId = OfferInfo?.Id,
            Amount = _piconeroAmount,
            PaymentAccountId = SelectedPaymentAccountId,
            Challenge = OfferInfo?.Challenge
        };

        var response = await tradesClient.TakeOfferAsync(takeOfferRequest);
        var trade = response.Trade;
    }

    public void Cancel()
    {
        NavigationManager.NavigateTo("buysell?title=Buy%20%26%20Sell");
    }

    public void Dispose()
    {

    }
}
