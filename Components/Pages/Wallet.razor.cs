using Blazored.LocalStorage;
using HavenoSharp.Models;
using HavenoSharp.Models.Requests;
using HavenoSharp.Services;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace Manta.Components.Pages;

public partial class Wallet : ComponentBase, IDisposable
{
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;
    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;
    [Inject]
    public IHavenoWalletService WalletService { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    public WalletInfo? Balance { get; set; }
    public List<string> Addresses { get; set; } = [];
    public List<XmrTx> Transactions { get; set; } = [];

    public decimal PendingFiat { get; set; }
    public decimal AvailableFiat { get; set; }

    public string Memo { get; set; } = string.Empty;
    public string WithdrawalAddress { get; set; } = string.Empty;

    public bool VerifyModalIsOpen { get; set; }
    public bool CreatingTxModalIsOpen { get; set; }

    public string WalletSeed { get; set; } = string.Empty;
    public bool ShowWalletSeed { get; set; }

    public XmrTx? Transaction { get; set; }

    private ulong _piconeroAmount;
    public decimal Amount
    {
        get
        {
            return _piconeroAmount.ToMonero();
        }
        set
        {
            _piconeroAmount = value.ToPiconero();
        }
    }

    public int SelectedTabIndex { get; set; }
    public string PreferredCurrency { get; set; } = string.Empty;
    public NumberFormatInfo PreferredCurrencyFormat { get; set; } = default!;
    public bool IsFetching { get; set; }

    public CancellationTokenSource CancellationTokenSource { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        while (true) 
        { 
            try
            {
                if (!AppPreferences.Get<bool>(AppPreferences.SeedBackupDone))
                {
                    NavigationManager.NavigateTo("/wallet/seedbackup?title=Seed%20Backup");
                    return;
                }

                PreferredCurrency = await LocalStorage.GetItemAsStringAsync("preferredCurrency") ?? CurrencyCultureInfo.FallbackCurrency;
                PreferredCurrencyFormat = CurrencyCultureInfo.GetFormatForCurrency((Currency)Enum.Parse(typeof(Currency), PreferredCurrency))!;

                Balance = BalanceSingleton.WalletInfo;

                if (BalanceSingleton.WalletInfo is not null)
                    Addresses = [BalanceSingleton.WalletInfo.PrimaryAddress];

                PendingFiat = BalanceSingleton.ConvertMoneroToFiat(Balance?.PendingXMRBalance.ToMonero() ?? 0m, PreferredCurrency);
                AvailableFiat = BalanceSingleton.ConvertMoneroToFiat(Balance?.AvailableXMRBalance.ToMonero() ?? 0m, PreferredCurrency);

                BalanceSingleton.OnBalanceFetch += HandleBalanceFetch;

                _ = Task.Run(GetTransactionsAsync);

                break;
            }
            catch
            {

            }
        
            await Task.Delay(5_000);
        }

        await base.OnInitializedAsync();
    }

    private async Task GetTransactionsAsync()
    {
        while (true)
        {
            try
            {
                await PauseTokenSource.WaitWhilePausedAsync();

                Transactions = await WalletService.GetXmrTxsAsync();

                await Task.Delay(5_000, CancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
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

    private async void HandleBalanceFetch(bool isFetching)
    {
        await InvokeAsync(() => {
            IsFetching = isFetching;

            Balance = BalanceSingleton.WalletInfo;

            if (BalanceSingleton.WalletInfo is not null)
                Addresses = [BalanceSingleton.WalletInfo.PrimaryAddress];

            PendingFiat = BalanceSingleton.ConvertMoneroToFiat(Balance?.PendingXMRBalance.ToMonero() ?? 0m, PreferredCurrency);
            AvailableFiat = BalanceSingleton.ConvertMoneroToFiat(Balance?.AvailableXMRBalance.ToMonero() ?? 0m, PreferredCurrency);

            StateHasChanged();
        });
    }

    public async Task CreateTransaction()
    {
        if (Balance is null)
            return;

        try
        {
            CreatingTxModalIsOpen = true;

            var request = new CreateXmrTxRequest();
            request.Destinations.Add(new XmrDestination
            {
                Address = WithdrawalAddress,
                Amount = _piconeroAmount.ToString()
            });

            Transaction = await WalletService.CreateXmrTxAsync(request);

            CreatingTxModalIsOpen = false;
            VerifyModalIsOpen = true;
        }
        catch
        {
            throw;
        }
        finally
        {

        }
    }

    public async Task GetXmrSeedAsync()
    {
        WalletSeed = await WalletService.GetXmrSeedAsync();
        ShowWalletSeed = true;
    }

    public async Task WithdrawAsync()
    {
        if (Transaction is null)
            return;

        var response = await WalletService.RelayXmrTxsAsync([Transaction.Metadata]);

        Amount = 0;
        WithdrawalAddress = string.Empty;
        Memo = string.Empty;

        // Subtract amount here?
    }

    public void Dispose()
    {
        BalanceSingleton.OnBalanceFetch -= HandleBalanceFetch;
        CancellationTokenSource.Cancel();
        CancellationTokenSource.Dispose();
    }
}
