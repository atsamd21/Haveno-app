using HavenoSharp.Models;
using HavenoSharp.Models.Requests;
using HavenoSharp.Services;
using Manta.Models;

namespace Manta.Singletons;

public class DaemonConnectionSingleton
{
    private readonly IHavenoVersionService _versionService;
    private readonly IHavenoWalletService _walletService;
    private bool _hasCreatedInitializationTransaction;

    public string Version { get; private set; } = string.Empty;
    public bool IsConnected { get; private set; }
    public Action<bool>? OnConnectionChanged;

    public bool IsWalletAvailable { get; private set; }
    public Action<bool>? OnWalletAvailabilityChanged;

    public DaemonConnectionSingleton(IHavenoVersionService versionService, IHavenoWalletService walletService)
    {
        _versionService = versionService;
        _walletService = walletService;

        Task.Run(PollDaemon);
        Task.Run(PollWallet);
    }

    private async Task PollWallet()
    {
        while (true)
        {
            try
            {
                await PauseTokenSource.WaitWhilePausedAsync();

                await _walletService.GetXmrPrimaryAddressAsync();

                if (!IsWalletAvailable)
                {
                    IsWalletAvailable = true;
                    OnWalletAvailabilityChanged?.Invoke(true);

                    // Create transaction to speed up future requests
                    if (!_hasCreatedInitializationTransaction)
                    {
                        var balances = await _walletService.GetBalancesAsync();
                        if (balances.AvailableXMRBalance > 0)
                        {
                            await _walletService.CreateXmrTxAsync(new CreateXmrTxRequest 
                            { 
                                Destinations = [
                                    new XmrDestination {
                                        Address = AppConstants.Network == "XMR_MAINNET" ? "888tNkZrPN6JsEgekjMnABU4TBzc2Dt29EPAvkRxbANsAnjyPbb3iQ1YBRk1UXcdRsiKc9dhwMVgN5S9cQUiyoogDavup3H" : AppConstants.Network == "XMR_STAGENET" ? "53piHrKPV5Yj2KYv3CMiLxepGixrtSw3iWNwuBth9bVSHcxE1y2uXhZJRi4aehDaT3L2PC1W1qWrQD1Mfzu8UMxoDoR8bad" : "9tsUiG9bwcU7oTbAdBwBk2PzxFtysge5qcEsHEpetmEKgerHQa1fDqH7a4FiquZmms7yM22jdifVAD7jAb2e63GSJMuhY75",
                                        Amount = "1"
                                    }
                                ]
                            });
                        }

                        _hasCreatedInitializationTransaction = true;
                    }
                }
            }
            catch (Exception)
            {
                if (IsWalletAvailable)
                {
                    IsWalletAvailable = false;
                    OnWalletAvailabilityChanged?.Invoke(false);
                }
            }
            finally
            {

            }

            await Task.Delay(5_000);
        }
    }

    private async Task PollDaemon()
    {
        while (true)
        {
            try
            {
                await PauseTokenSource.WaitWhilePausedAsync();

                // Checks if the daemon is running, it could still be that its not fully initialized so things like wallet won't work.
                Version = await _versionService.GetVersionAsync();

                // If connection status has changed
                if (!IsConnected)
                {
                    IsConnected = true;
                    OnConnectionChanged?.Invoke(true);
                }
            }
            catch (Exception)
            {
                if (IsConnected)
                {
                    IsConnected = false;
                    OnConnectionChanged?.Invoke(false);
                }
            }
            finally
            {

            }

            await Task.Delay(5_000);
        }
    }
}
