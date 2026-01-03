using Blazored.LocalStorage;
using CommunityToolkit.Maui.Storage;
using HavenoSharp.Services;
using HavenoSharp.Singletons;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Grpc.Net.Client.Web;
using Manta.Services;
using System.Net;
using System.Threading.Tasks;

namespace Manta.Components.Pages;

public partial class Settings : ComponentBase, IDisposable
{
    public bool IsXmrNodeOnline { get; private set; }

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
    [Inject]
    public IHavenoXmrNodeService HavenoXmrNodeService { get; set; } = default!;

    public bool IsFetching { get; set; }
    public bool IsBackingUp { get; set; }
    public bool ShowConnectToRemoteNodeModal { get; set; }

    public bool IsNotificationsToggled { get; set; }
    public bool IsWakeLockToggled { get; set; }
    public bool IsRemoteNodeToggled { get; set; }

    public bool IsConnected { get; set; }
    public DaemonInstallOptions DaemonInstallOption { get; set; }

    public string? Password { get; set { field = value?.Trim(); } }
    public string? Host { get; set { field = value?.Trim(); } }

    public CancellationTokenSource? RemoteNodeConnectCts { get; set; }
    public CancellationTokenSource? BackupCts { get; set; }
    public string? ConnectionError { get; set; }

    public bool ShowRestoreModal { get; set; }

    public bool ShowConnectToMoneroNodeModal { get; set; }

    Timer? ValidateMoneroNodeUrlTimer { get; set; }

    public string? IsMoneroNodeUrlInvalidReason { get; set; }
    public bool IsMoneroNodeUrlInvalid { get; set; }

    public string? MoneroNodePassword { get; set { field = value?.Trim(); } }
    public string? MoneroNodeUrl 
    { 
        get; 
        set 
        { 
            field = value?.Trim().ToLower();

            if (field is null)
                return;

            ValidateMoneroNodeUrlTimer?.Dispose();
            ValidateMoneroNodeUrlTimer = new(async(e) => await ValidateUrl(field), null, 1000, Timeout.Infinite);
        } 
    }

    public string? MoneroNodeUsername { get; set { field = value?.Trim(); } }

    public string ConnectedMoneroNodeUrl { get; set; } = string.Empty;

    public async Task ValidateUrl(string field)
    {
        try
        {
        // TODO update this to allow onion addresses
        
            if (field == string.Empty)
            {
                IsMoneroNodeUrlInvalidReason = null;
                return;
            }
            
            var protocol = field.Split("http://");
            if (protocol.Length < 2)
            {
                protocol = field.Split("https://");
            }
            if (protocol.Length < 2)
            {
                IsMoneroNodeUrlInvalidReason = "Please include http:// or https:// at the beginning of your URL.";
                return;
            }

            var addressAndPort = protocol[1].Split(":");
            if (addressAndPort.Length < 2)
            {
                IsMoneroNodeUrlInvalidReason = null;
                return;
            }

            if (!IPAddress.TryParse(addressAndPort[0], out _))
            {
                //IsMoneroNodeUrlInvalidReason = "Please use an IP address or onion address and not a hostname.";
                IsMoneroNodeUrlInvalidReason = "Please use an IP address and not a domain name.";
                return;
            }

            if (!int.TryParse(addressAndPort[1], out _))
            {
                IsMoneroNodeUrlInvalidReason = "Please include a valid port number.";
                return;
            }

            IsMoneroNodeUrlInvalidReason = null;
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    public async Task RestoreFromBackupAsync()
    {
        var backupZip = await FilePicker.PickAsync();
        if (backupZip is null)
            return;

        using var fileStream = File.Open(backupZip.FullPath, FileMode.Open);

        using MemoryStream memoryStream = new();
        fileStream.CopyTo(memoryStream);

        // Bugged, for some reason daemon restarts after account deleted and creates a new account
        await AccountService.DeleteAccountAsync();

        await AccountService.RestoreAccountAsync(memoryStream);
    }

    public async Task BackupAsync()
    {
        IsBackingUp = true;

        try
        {
            var result = await FolderPicker.Default.PickAsync();
            if (!result.IsSuccessful)
            {
                IsBackingUp = false;
                return;
            }

            BackupCts = new();
            using var backupStream = await AccountService.BackupAccountAsync(BackupCts.Token);

#pragma warning disable CA1416
            var fileSaverResult = await FileSaver.Default.SaveAsync(result.Folder.Path, $"haveno_backup_{DateTime.Now}-{Guid.NewGuid()}.zip", backupStream);
#pragma warning restore CA1416

            if (!fileSaverResult.IsSuccessful)
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
        var permissionStatus = await Permissions.RequestAsync<Permissions.Camera>();
        if (permissionStatus == PermissionStatus.Denied || permissionStatus == PermissionStatus.Unknown || permissionStatus == PermissionStatus.Disabled)
            return;

        var current = Application.Current;
        if (current?.MainPage is null)
            throw new Exception("current.MainPage was null");

        var cameraPage = new CameraPage();
        await current.MainPage.Navigation.PushModalAsync(cameraPage, true);

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

        await current.MainPage.Navigation.PopModalAsync(true);
    }

    public async Task ConnectToMoneroNodeAsync()
    {
        // Should show error etc
        if (MoneroNodeUrl is null)
            return;

        MoneroNodeUsername ??= string.Empty;
        MoneroNodePassword ??= string.Empty;

        // Does not throw an exception if fails?
        await HavenoXmrNodeService.SetAutoSwitchAsync(false);
        await HavenoXmrNodeService.SetMoneroNodeAsync(MoneroNodeUrl, MoneroNodeUsername, MoneroNodePassword, 0);

        var response = await HavenoXmrNodeService.GetMoneroNodeAsync();
        ConnectedMoneroNodeUrl = response.Url;

        AppPreferences.Set(AppPreferences.CustomXmrNode, ConnectedMoneroNodeUrl);

        ShowConnectToMoneroNodeModal = false;
        MoneroNodeUrl = string.Empty;
        MoneroNodeUsername = string.Empty;
        MoneroNodePassword = string.Empty;
    }

    public async Task CancelConnectToRemoteNodeAsync()
    {
        if (RemoteNodeConnectCts is null)
            return;

        await RemoteNodeConnectCts.CancelAsync();

        ShowConnectToRemoteNodeModal = false;
    }

    public async Task ConnectToRemoteNodeAsync()
    {
        if (string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(Host))
            return;

        ShowConnectToRemoteNodeModal = true;

        await SecureStorageHelper.SetAsync("daemon-installation-type", DaemonInstallOptions.RemoteNode);

        var host = "http://" + Host + ":2134";

        await SecureStorageHelper.SetAsync("password", Password);
        await SecureStorageHelper.SetAsync("host", host);

#if ANDROID
        GrpcChannelSingleton.CreateChannel(host, Password, new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new AndroidSocks5Handler())));
#endif

        //await HavenoDaemonService.StopHavenoDaemonAsync();

        RemoteNodeConnectCts = new();

        try
        {
            // Try for 2 minutes
            for (int i = 0; i < 120; i++)
            {
                if (await HavenoDaemonService.IsHavenoDaemonRunningAsync(RemoteNodeConnectCts.Token))
                {
                    IsConnected = true;
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

        ShowConnectToRemoteNodeModal = false;
        ConnectionError = "Could not connect to remote node. Make sure Orbot is installed and configured.";
    }

    public async Task HandleRemoteNodeToggleAsync(bool isToggled)
    {
        // Prompt that account won't be synced and that if running, local daemon, termux etc needs to be stopped
        if (!isToggled) 
        {
            // Theres a small issue if orbot is running at the same time as it listens to the same ports that the Termux tor instance listens on, however users should not be regularly switching hosting modes
            await SecureStorageHelper.SetAsync("daemon-installation-type", DaemonInstallOptions.Standalone);

            if ((await HavenoDaemonService.GetIsDaemonInstalledAsync()).Item1)
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

    public async Task HandleToggleAsync(bool isToggled)
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
            IsXmrNodeOnline = DaemonInfoSingleton.IsXmrNodeOnline;
            ConnectedMoneroNodeUrl = DaemonInfoSingleton.ConnectedMoneroNodeUrl;
            StateHasChanged();
        });
    }

    private async void HandleDaemonConnectionChanged(bool isConnected)
    {
        await InvokeAsync(() => {
            if (IsConnected == isConnected)
                return;

            IsConnected = isConnected;
            StateHasChanged();
        });
    }

    protected override async Task OnInitializedAsync()
    {
#if ANDROID
        IsNotificationsToggled = (await Permissions.CheckStatusAsync<NotificationPermission>() == PermissionStatus.Granted) && (await SecureStorageHelper.GetAsync<bool>("notifications-enabled"));
#endif
        PreferredCurrency = AppPreferences.Get<Currency>(AppPreferences.PreferredCurrency).ToString();
        
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
        IsXmrNodeOnline = DaemonInfoSingleton.IsXmrNodeOnline;
        ConnectedMoneroNodeUrl = DaemonInfoSingleton.ConnectedMoneroNodeUrl;

        DaemonInfoSingleton.OnDaemonInfoFetch += HandleDaemonInfoFetch;
        DaemonConnectionSingleton.OnConnectionChanged += HandleDaemonConnectionChanged;

        await base.OnInitializedAsync();
    }

    public void HandlePreferredCurrencySubmitAsync(string currencyCode)
    {
        if (string.IsNullOrEmpty(currencyCode))
            return;

        AppPreferences.Set(AppPreferences.PreferredCurrency, Enum.Parse<Currency>(currencyCode));
        PreferredCurrency = currencyCode;
    }

    public void Dispose()
    {
        DaemonInfoSingleton.OnDaemonInfoFetch -= HandleDaemonInfoFetch;
        DaemonConnectionSingleton.OnConnectionChanged -= HandleDaemonConnectionChanged;
    }
}
