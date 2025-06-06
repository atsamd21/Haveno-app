﻿using Blazored.LocalStorage;
using Manta.Models;
using Manta.Services;
using Microsoft.AspNetCore.Components;
using Manta.Singletons;
using Manta.Helpers;
using HavenoSharp.Singletons;
using Grpc.Net.Client.Web;

namespace Manta.Components.Pages;

public partial class Index : ComponentBase
{
    public bool IsInitializing { get; set; }
    public DaemonSetupState DaemonSetupState { get; set; }
    public bool IsInstallTypeModalOpen { get; set; }
    public string DaemonStartInfo { get; set; } = string.Empty;
    public bool IsDaemonStartInfoModalOpen { get; set; }
    public string? InstallationErrorMessage { get; set; }

    [Inject]
    public ISetupService SetupService { get; set; } = default!;
    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    public IHavenoDaemonService HavenoDaemonService { get; set; } = default!;

    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;
    [Inject]
    public GrpcChannelSingleton GrpcChannelSingleton { get; set; } = default!;
    [Inject]
    public DaemonConnectionSingleton DaemonConnectionSingleton { get; set; } = default!;

    public async void HandleDaemonStartInfoChange(string info)
    {
        await InvokeAsync(() =>
        {
            DaemonStartInfo = info;
            StateHasChanged();
        });
    }

    public async Task HandleInstall(DaemonInstallOptions daemonInstallOption)
    {
        // Should be able to change from local to remote node and manual to auto. account sync is not a current feature
        await SecureStorageHelper.SetAsync("daemon-installation-type", daemonInstallOption);

        switch (daemonInstallOption)
        {
            case DaemonInstallOptions.Standalone:
                await InstallHavenoDaemonAsync();
                break;
            case DaemonInstallOptions.RemoteNode:
                NavigationManager.NavigateTo("/Settings");
                break;
            default: return;
        }
    }

    public async Task StartHaveno()
    {
        IsDaemonStartInfoModalOpen = true;
        await HavenoDaemonService.TryStartLocalHavenoDaemonAsync(Guid.NewGuid().ToString(), "http://127.0.0.1:3201", HandleDaemonStartInfoChange);

        CancellationTokenSource initializingTokenSource = new();
        var daemonInitialized = await HavenoDaemonService.WaitHavenoDaemonInitializedAsync(initializingTokenSource.Token);
        if (!daemonInitialized)
        {
            // Tell user
        }

        HandleDaemonStartInfoChange("Initializing wallet");
        await HavenoDaemonService.WaitWalletInitializedAsync(initializingTokenSource.Token);

        NavigationManager.NavigateTo("/Market");
    }

    public async Task InstallHavenoDaemonAsync()
    {
        IsInstallTypeModalOpen = false;

        var isDaemonInstalled = await HavenoDaemonService.GetIsDaemonInstalledAsync();
        if (isDaemonInstalled)
            return;

        DaemonSetupState = DaemonSetupState.InstallingDependencies;
        StateHasChanged();

        try
        {
            await HavenoDaemonService.InstallHavenoDaemonAsync();
        }
        catch (Exception e)
        {
            await SecureStorageHelper.SetAsync("daemonInstallOption", DaemonInstallOptions.None);
            IsInstallTypeModalOpen = true;
            InstallationErrorMessage = e.ToString();
            StateHasChanged();
            return;
        }

        await StartHaveno();
    }

    protected override async Task OnInitializedAsync()
    {
        IsInitializing = true;
        StateHasChanged();

        // Set up things like default currency
        await SetupService.InitialSetupAsync();

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
                    if (!isDaemonInstalled)
                    {
                        // If install failed or partially completed etc
                        IsInstallTypeModalOpen = true;
                    }
                    else
                    {
                        var host = await SecureStorageHelper.GetAsync<string>("host");
                        var password = await SecureStorageHelper.GetAsync<string>("password");

                        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(password))
                        {

                        }
                        else
                        {
                            GrpcChannelSingleton.CreateChannel(host, password);
                        }

                        await StartHaveno();
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

                            if (await HavenoDaemonService.IsHavenoDaemonRunningAsync())
                            {
                                NavigationManager.NavigateTo("/Market");
                            }
                            else
                            {
                                // Send param to open modal?
                                NavigationManager.NavigateTo("/Settings");
                            }
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
