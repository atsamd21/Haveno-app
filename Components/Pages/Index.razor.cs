using HavenoSharp.Services;
using HavenoSharp.Singletons;
using Manta.Helpers;
using Manta.Models;
using Manta.Services;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Grpc.Net.Client.Web;
using Grpc.Core;

namespace Manta.Components.Pages;

public partial class Index : ComponentBase
{
    public bool IsInitializing { get; set; }
    public DaemonSetupState DaemonSetupState { get; set; }
    public bool IsInstallTypeModalOpen { get; set; }
    public HavenoSharp.Models.Responses.GetWalletHeightResponse? WalletHeight { get; set; }
    public string DaemonStartInfo { get; set; } = string.Empty;
    public double InstallProgress { get; set; }
    public bool IsDaemonStartInfoModalOpen { get; set; }
    public string? InstallationErrorMessage { get; set; }
    public string? UpdateErrorMessage { get; set; }

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    public IHavenoDaemonService HavenoDaemonService { get; set; } = default!;
    [Inject]
    public IHavenoXmrNodeService HavenoXmrNodeService { get; set; } = default!;
    [Inject]
    public IHavenoWalletService HavenoWalletService { get; set; } = default!;
    [Inject]
    public IHavenoAccountService HavenoAccountService { get; set; } = default!;

    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;
    [Inject]
    public GrpcChannelSingleton GrpcChannelSingleton { get; set; } = default!;
    [Inject]
    public DaemonConnectionSingleton DaemonConnectionSingleton { get; set; } = default!;

    public CancellationTokenSource? InitializingTokenSource;

    public async void HandleDaemonStartInfoChange(string info)
    {
        await InvokeAsync(() =>
        {
            DaemonStartInfo = info;
            StateHasChanged();
        });
    }

    public async Task HandleInstallAsync(DaemonInstallOptions daemonInstallOption)
    {
        // Should be able to change from local to remote node and manual to auto. account sync is not a current feature
        await SecureStorageHelper.SetAsync("daemon-installation-type", daemonInstallOption);

        switch (daemonInstallOption)
        {
            case DaemonInstallOptions.Standalone:
                if (!await InstallHavenoDaemonAsync()) 
                    return;

                await StartHavenoStandalone();
                break;
            case DaemonInstallOptions.RemoteNode:
                NavigationManager.NavigateTo("/Settings");
                break;
            default: return;
        }
    }

    public async Task StartHavenoStandalone()
    {
        IsDaemonStartInfoModalOpen = true;

        InitializingTokenSource = new();
        var task = Task.Run(async () =>
        {
            while (!InitializingTokenSource.IsCancellationRequested)
            {
                try
                {
                    WalletHeight = await HavenoWalletService.GetHeightAsync(InitializingTokenSource.Token);
                    Console.WriteLine(WalletHeight.Height);
                    await InvokeAsync(StateHasChanged);
                }
                catch (RpcException)
                {

                }

                await Task.Delay(100);
            }
        });

        await HavenoDaemonService.TryStartLocalHavenoDaemonAsync(Guid.NewGuid().ToString(), "http://127.0.0.1:3201", HandleDaemonStartInfoChange);

        while (!await HavenoDaemonService.IsHavenoDaemonRunningAsync(InitializingTokenSource.Token))
        {
            await Task.Delay(50);
        }

        // If password is null account should open automatically
        if (!await HavenoAccountService.IsAccountOpenAsync(InitializingTokenSource.Token))
        {
            if (!await HavenoAccountService.AccountExistsAsync())
            {
                await HavenoAccountService.CreateAccountAsync("haveno0000");
            }

            await HavenoAccountService.OpenAccountAsync("haveno0000");
        }

        await HavenoDaemonService.WaitHavenoDaemonInitializedAsync(InitializingTokenSource.Token);

        InitializingTokenSource.Cancel();
        await task;

        NavigationManager.NavigateTo("/Market");
    }

    public async Task<bool> InstallHavenoDaemonAsync()
    {
        IsInstallTypeModalOpen = false;

        var isDaemonInstalled = await HavenoDaemonService.GetIsDaemonInstalledAsync();
        if (isDaemonInstalled.Item1)
            return true;

        DaemonSetupState = DaemonSetupState.InstallingDependencies;
        StateHasChanged();

        try
        {
            var progressCb = new Progress<double>(progress =>
            {
                if (progress == 101f)
                {
                    DaemonSetupState = DaemonSetupState.ExtractingRootfs;
                    InstallProgress = 0f;
                }
                else if (progress == 102f)
                {
                    DaemonSetupState = DaemonSetupState.InstallingDaemon;
                    InstallProgress = 0f;
                }
                else
                {
                    InstallProgress = progress;
                }

                StateHasChanged();
            });

            await HavenoDaemonService.InstallHavenoDaemonAsync(progressCb);

            isDaemonInstalled = await HavenoDaemonService.GetIsDaemonInstalledAsync();
            if (!isDaemonInstalled.Item1)
                throw new Exception(isDaemonInstalled.Item2);

            return true;
        }
        catch (Exception e)
        {
            // Todo don't need to use SecureStorage for everything
            await SecureStorageHelper.SetAsync("daemon-installation-type", DaemonInstallOptions.None);
            DaemonSetupState = DaemonSetupState.Initial;
            InstallationErrorMessage = e.ToString();
            StateHasChanged();
            return false;
        }
    }

    private void InitializePreferences()
    {
        if (AppPreferences.Get<bool>(AppPreferences.PreferencesInitialized) is true)
            return;

        AppPreferences.Set(AppPreferences.PreferredCurrency, Currency.USD);
        AppPreferences.Set(AppPreferences.PreferencesInitialized, true);
    }

    protected override async Task OnInitializedAsync()
    {
        IsInitializing = true;
        StateHasChanged();

        InitializePreferences();

        // For first time and if user does not install when first using app
        var daemonInstallOption = await SecureStorageHelper.GetAsync<DaemonInstallOptions>("daemon-installation-type");
        if (daemonInstallOption == DaemonInstallOptions.None)
        {
            IsInstallTypeModalOpen = true;
        }
        else
        {
            switch (daemonInstallOption)
            {
                case DaemonInstallOptions.Standalone:
                    var isDaemonInstalled = await HavenoDaemonService.GetIsDaemonInstalledAsync();
                    if (!isDaemonInstalled.Item1)
                    {
                        // If install failed or partially completed etc
                        IsInstallTypeModalOpen = true;
                    }
                    else
                    {
                        var progressCb = new Progress<double>(progress =>
                        {
                            if (DaemonSetupState == DaemonSetupState.Initial)
                            {
                                InstallProgress = 0;
                                DaemonSetupState = DaemonSetupState.UpdatingRootfs;
                            }

                            if (progress == 101f)
                            {
                                DaemonSetupState = DaemonSetupState.ExtractingRootfs;
                                InstallProgress = 0f;
                            }
                            else if (progress == 102f)
                            {
                                DaemonSetupState = DaemonSetupState.UpdatingDaemon;
                                InstallProgress = 0f;
                            }
                            else
                            {
                                InstallProgress = progress;
                            }

                            StateHasChanged();
                        });

                        try
                        {
                            await HavenoDaemonService.TryUpdateHavenoAsync(progressCb);
                        }
                        catch (Exception e)
                        {
                            UpdateErrorMessage = e.Message;
                            return;
                        }

                        // If restoring
                        NotificationSingleton.TradeInfos.Clear();

                        await StartHavenoStandalone();
                    }
                    break;
#if ANDROID
                case DaemonInstallOptions.RemoteNode:
                    {
                        var host = await SecureStorageHelper.GetAsync<string>("host");
                        var password = await SecureStorageHelper.GetAsync<string>("password");

                        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(password))
                        {
                            NavigationManager.NavigateTo("/Settings");
                        }
                        else
                        {
                            GrpcChannelSingleton.CreateChannel(host, password, new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new AndroidSocks5Handler())));

                            for (int i = 0; i < 2; i++)
                            {
                                using CancellationTokenSource cancellationTokenSource = new(5_000);

                                try
                                {
                                    await HavenoDaemonService.IsHavenoDaemonRunningAsync(cancellationTokenSource.Token);
                                    NavigationManager.NavigateTo("/Market");
                                    return;
                                }
                                catch (TaskCanceledException)
                                {

                                }
                            }

                            NavigationManager.NavigateTo("/Settings");
                        }
                    }
                    break;
#endif
                default: throw new Exception("Invalid DaemonInstallOption");
            }
        }

        IsInitializing = false;

        await base.OnInitializedAsync();
    }
}
