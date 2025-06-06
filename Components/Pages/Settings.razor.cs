﻿using Blazored.LocalStorage;
using CommunityToolkit.Maui.Storage;
using HavenoSharp.Services;
using HavenoSharp.Singletons;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Grpc.Net.Client.Web;
using Manta.Services;

#if ANDROID
using Manta.Platforms.Android.Services;
#endif

namespace Manta.Components.Pages;

public partial class Settings : ComponentBase, IDisposable
{
    public bool XMRNodeIsRunning { get; private set; }

    public string Version { get; } = AppInfo.Current.VersionString;
    public string Build { get; } = AppInfo.Current.BuildString;
    public string HavenoVersion { get; private set; } = string.Empty;

    // Languages aren't implemented
    public Dictionary<string, string> Countries { get; set; } = new Dictionary<string, string> { { "English", "English" } };
    public Dictionary<string, string> Currencies { get; set; } = CurrencyCultureInfo.GetCurrencyFullNamesAndCurrencyCodeDictionary().ToDictionary();
    public string PreferredCurrency { get; set; } = string.Empty;

    [Inject]
    public DaemonInfoSingleton DaemonInfoSingleton { get; set; } = default!;
    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;
    [Inject]
    public DaemonConnectionSingleton DaemonConnectionSingleton { get; set; } = default!;
    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    public IHavenoAccountService AccountService { get; set; } = default!;
    [Inject]
    public GrpcChannelSingleton GrpcChannelSingleton { get; set; } = default!;
    [Inject]
    public IHavenoDaemonService HavenoDaemonService { get; set; } = default!;

    public bool IsFetching { get; set; }
    public bool IsBackingUp { get; set; }
    public bool IsConnecting { get; set; }

    public bool IsNotificationsToggled { get; set; }
    public bool IsWakeLockToggled { get; set; }
    public bool IsRemoteNodeToggled { get; set; }

    public bool IsConnected { get; set; }
    public DaemonInstallOptions DaemonInstallOption { get; set; }

    public string? Password { get; set; }
    public string? Host { get; set; }

    public CancellationTokenSource? RemoteNodeConnectCts { get; set; }
    public CancellationTokenSource? BackupCts { get; set; }
    public string? ConnectionError { get; set; }

    public bool ShowRestoreModal { get; set; }

    public async Task RestoreFromBackupAsync()
    {
        //var backupZip = await FilePicker.PickAsync();
        //if (backupZip is null)
        //    return;

        //using var fileStream = File.Open(backupZip.FullPath, FileMode.Open);

        //using MemoryStream memoryStream = new();
        //fileStream.CopyTo(memoryStream);

        //var zipBytes = await Google.Protobuf.ByteString.FromStreamAsync(memoryStream);

        //await DeleteAccountAsync();

        //using var grpcChannelHelper = new GrpcChannelHelper(disableMessageSizeLimit: true);
        //var accountClient = new AccountClient(grpcChannelHelper.Channel);

        //var response = await accountClient.RestoreAccountAsync(new RestoreAccountRequest { ZipBytes = zipBytes, TotalLength = (ulong)zipBytes.Length, HasMore = false, Offset = 0 });
    }

    public async Task BackupAsync()
    {
        IsBackingUp = true;

        try
        {
            var result = await FolderPicker.Default.PickAsync();
            if (result.IsSuccessful)
            {

            }
            else
            {
                IsBackingUp = false;
                return;
            }

            BackupCts = new();
            using var backupStream = await AccountService.BackupAccountAsync(BackupCts.Token);

#pragma warning disable CA1416
            var fileSaverResult = await FileSaver.Default.SaveAsync(result.Folder.Path, $"haveno_backup_{DateTime.Now.ToString()}-{Guid.NewGuid()}.zip", backupStream);
#pragma warning restore CA1416

            if (fileSaverResult.IsSuccessful)
            {

            }
            else
            {
                IsBackingUp = false;
                return;
            }
        }
        catch
        {

        }

        IsBackingUp = false;
    }

    public async Task ScanQRCodeAsync()
    {
        await Permissions.RequestAsync<Permissions.Camera>();

        var cameraPage = new CameraPage();

        var current = Application.Current;

        if (current?.MainPage is null)
            throw new Exception("current.MainPage was null");

        await current.MainPage.Navigation.PushModalAsync(cameraPage);

        var scanResults = await cameraPage.WaitForResultAsync();

        var barcode = scanResults.FirstOrDefault();
        if (barcode is not null)
        {
            try
            {
                var split = barcode.Value.Split(';');
                if (split.Length == 2)
                {
                    Host = split[0];
                    Password = split[1];

                    StateHasChanged();
                }
            }
            catch
            {
                throw new Exception("Error parsing QR code");
            }
        }

        await current.MainPage.Navigation.PopModalAsync();
    }

    public async Task CancelConnectToRemoteNode()
    {
        if (RemoteNodeConnectCts is null)
            return;

        await RemoteNodeConnectCts.CancelAsync();
    }

    public async Task ConnectToRemoteNode()
    {
        // Todo validation
        if (string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(Host))
            return;

        IsConnecting = true;

        await SecureStorageHelper.SetAsync("daemon-installation-type", DaemonInstallOptions.RemoteNode);

        var host = "http://" + Host + ":2134";

        await SecureStorageHelper.SetAsync("password", Password);
        await SecureStorageHelper.SetAsync("host", host);

#if ANDROID
        GrpcChannelSingleton.CreateChannel(host, Password, new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new AndroidSocks5Handler())));
#endif

        await HavenoDaemonService.StopHavenoDaemonAsync();

        RemoteNodeConnectCts = new();

        try
        {
            // Try for 2 minutes
            for (int i = 0; i < 120; i++)
            {
                if (await HavenoDaemonService.IsHavenoDaemonRunningAsync(RemoteNodeConnectCts.Token))
                {
                    IsConnecting = false;
                    return;
                }
            }
        }
        catch (TaskCanceledException)
        {
            RemoteNodeConnectCts.Dispose();
            RemoteNodeConnectCts = new();
            return;
        }

        IsConnecting = false;
        ConnectionError = "Could not connect to remote node. Make sure Orbot is installed and configured.";
    }

    public async Task HandleRemoteNodeToggle(bool isToggled)
    {
        // Prompt that account won't be synced and that if running, local daemon, termux etc needs to be stopped
        if (!isToggled) 
        {
            // Theres a small issue if orbot is running at the same time as it listens to the same ports that the Termux tor instance listens on, however users should not be regularly switching hosting modes
            await SecureStorageHelper.SetAsync("daemon-installation-type", DaemonInstallOptions.Standalone);

            if (await HavenoDaemonService.GetIsDaemonInstalledAsync())
            {
                await HavenoDaemonService.TryStartLocalHavenoDaemonAsync(Guid.NewGuid().ToString(), "http://127.0.0.1:3201");
            }
            else
            {
                NavigationManager.NavigateTo("/");
            }

            Password = null;
            Host = null;
        }
    }

    public async Task HandleToggle(bool isToggled)
    {
#if ANDROID
        if (isToggled)
        {
            var status = await Permissions.RequestAsync<NotificationPermission>() == PermissionStatus.Granted;
            if (status)
                await SecureStorageHelper.SetAsync("notifications-enabled", true);
            else 
                IsNotificationsToggled = false;
        }
        else
        {
            await SecureStorageHelper.SetAsync("notifications-enabled", false);
        }
#endif
    }

    public async Task HandleWakeLockToggle(bool isToggled)
    {
        if (!isToggled)
            return;
        
#if ANDROID
        if (!await AndroidPermissionService.RequestIgnoreBatteryOptimizationsAsync())
        {
            IsWakeLockToggled = false;
        }
#endif
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private async void HandleDaemonInfoFetch(bool isFetching)
    {
        await InvokeAsync(() => {
            XMRNodeIsRunning = DaemonInfoSingleton.XMRNodeIsRunning;
            StateHasChanged();
        });
    }

    private async void HandleDaemonConnectionChanged(bool isConnected)
    {
        await InvokeAsync(() => {
            IsConnected = isConnected;
            StateHasChanged();
        });
    }

    protected override async Task OnInitializedAsync()
    {
#if ANDROID
        IsNotificationsToggled = (await Permissions.CheckStatusAsync<NotificationPermission>() == PermissionStatus.Granted) && (await SecureStorageHelper.GetAsync<bool>("notifications-enabled"));
#endif
        var preferredCurrency = await LocalStorage.GetItemAsStringAsync("preferredCurrency");
        if (preferredCurrency is not null)
        {
            PreferredCurrency = preferredCurrency;
        }
        
        var host = await SecureStorageHelper.GetAsync<string>("host");
        if (host != "http://127.0.0.1:3201")
        {
            Host = host?.Replace("http://", "").Replace(":2134", "");
            Password = await SecureStorageHelper.GetAsync<string>("password");
        }

        DaemonInstallOption = await SecureStorageHelper.GetAsync<DaemonInstallOptions>("daemon-installation-type");
        IsRemoteNodeToggled = DaemonInstallOption == DaemonInstallOptions.RemoteNode;

#if ANDROID
        IsWakeLockToggled = AndroidPermissionService.GetIgnoreBatteryOptimizationsEnabled() || DaemonInstallOption == DaemonInstallOptions.RemoteNode;
#endif

        HavenoVersion = DaemonConnectionSingleton.Version;
        IsConnected = DaemonConnectionSingleton.IsConnected;

        DaemonInfoSingleton.OnDaemonInfoFetch += HandleDaemonInfoFetch;
        DaemonConnectionSingleton.OnConnectionChanged += HandleDaemonConnectionChanged;

        StateHasChanged();

        await base.OnInitializedAsync();
    }

    public async Task HandlePreferredCurrencySubmit(string currencyCode)
    {
        if (string.IsNullOrEmpty(currencyCode))
            return;

        await LocalStorage.SetItemAsStringAsync("preferredCurrency", currencyCode);
        PreferredCurrency = currencyCode;
    }

    public void Dispose()
    {
        DaemonInfoSingleton.OnDaemonInfoFetch -= HandleDaemonInfoFetch;
        DaemonConnectionSingleton.OnConnectionChanged -= HandleDaemonConnectionChanged;
    }
}
