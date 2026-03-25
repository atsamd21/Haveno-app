using Android.Content;
using AndroidX.Core.Content;
using CommunityToolkit.Maui.Storage;
using Grpc.Net.Client.Web;
using HavenoSharp.Services;
using HavenoSharp.Singletons;
using Manta.Helpers;
using Manta.Models;
using Manta.Services;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using System.IO.Compression;
using System.IO.Pipelines;

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
    [Inject]
    public IHavenoServerService HavenoServerService { get; set; } = default!;

    public CancellationTokenSource? MoneroNodeConnectCancellationTokenSource;
    public bool IsConnectingToMoneroNode { get; set; }

    public bool IsFetching { get; set; }
    public bool IsBackingUp { get; set; }
    public bool IsRestoring { get; set; }
    public bool ShowConnectToRemoteNodeModal { get; set; }
    public bool ShowConfirmLinkModal { get; set; }
    public string? LinkToOpen { get; set; }

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
        IsRestoring = true;

        var backupZip = await FilePicker.PickAsync();
        if (backupZip is null)
        {
            IsRestoring = false;
            return;
        }

        if (!backupZip.FileName.EndsWith(".zip"))
            throw new Exception("Backup file should be a zip file.");

        var task = Task.Run(async() =>
        {
            await Task.Yield();

#if ANDROID
            // So we can unregister later
            var receiver = new ProgressReceiver();
#endif

            await NotificationSingleton.StopNotificationListenerAsync();
            PauseTokenSource.Pause();

            try
            {
                await HavenoServerService.StopAsync();
                while (await HavenoDaemonService.IsHavenoDaemonRunningAsync())
                {
                    await Task.Delay(50);
                }

#if ANDROID
                var filter = new IntentFilter($"{AppConstants.ApplicationId}.BACKEND_EXIT");

                if (OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    Platform.AppContext.RegisterReceiver(receiver, filter, ReceiverFlags.NotExported);
                }
                else
                {
                    Platform.AppContext.RegisterReceiver(receiver, filter);
                }

                var startBackendIntent = new Intent(Platform.AppContext, typeof(BackendService)).SetAction("ACTION_STOP_BACKEND");
                ContextCompat.StartForegroundService(Platform.AppContext, startBackendIntent);

                try
                {
                    await receiver.CompletedTCS.Task;
                }
                catch (TaskCanceledException)
                {
                    // Should throw
                }
#endif

                Directory.Delete(Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, ".local", "share", AppConstants.HavenoAppName), true);

                using var fileStream = File.Open(backupZip.FullPath, FileMode.Open);
                using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);

                try
                {
                    if (Directory.Exists(Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, ".local", "share", "tmp")))
                        Directory.Delete(Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, ".local", "share", "tmp"), true);
                }
                catch { }

                var tmpDir = Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, ".local", "share", "tmp");

                zipArchive.ExtractToDirectory(tmpDir);

                var files = Directory.GetDirectories(tmpDir);
                if (files.FirstOrDefault(x => x.Contains("xmr_")) is null)
                    throw new Exception("The selected zip file does not contain Haveno backup files.");

                // Rename
                Directory.Move(
                    tmpDir,
                    Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, ".local", "share", AppConstants.HavenoAppName));
            }
            catch (Exception e)
            {
                try
                {
                    if (Directory.Exists(Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, ".local", "share", "tmp")))
                        Directory.Delete(Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, ".local", "share", "tmp"), true);
                }
                catch (Exception e2)
                {
                    throw new AggregateException(e, e2);
                }

                throw;
            }
            finally
            {
#if ANDROID
                try
                {
                    var cacheDir = Android.App.Application.Context.ExternalCacheDir!.Path;
                    if (Directory.Exists(cacheDir))
                    {
                        Directory.Delete(cacheDir, true);
                        Directory.CreateDirectory(cacheDir);
                    }
                }
                catch
                {

                }

                try
                {
                    Platform.AppContext.UnregisterReceiver(receiver);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to unregister receiver: {e}");
                }
#endif
                PauseTokenSource.Resume();
            }
        });

        await task;

        NavigationManager.NavigateTo("/", forceLoad: true);
    }

    public async Task BackupAsync()
    {
        IsBackingUp = true;

        var result = await FolderPicker.Default.PickAsync();
        if (!result.IsSuccessful)
        {
            IsBackingUp = false;
            return;
        }

        var backupTask = Task.Run(async() =>
        {
            try
            {
                PauseTokenSource.Pause();

                BackupCts = new();

                if (SecureStorageHelper.Get<DaemonInstallOptions>("daemon-installation-type") == DaemonInstallOptions.RemoteNode)
                {
                    using var backupStream = await AccountService.BackupAccountAsync(BackupCts.Token);

#pragma warning disable CA1416
                    // UI thread needed?
                    var fileSaverResult = await FileSaver.Default.SaveAsync(result.Folder.Path, $"haveno_backup_{DateTime.Now}-{Guid.NewGuid()}.zip", backupStream, BackupCts.Token);
#pragma warning restore CA1416

                    if (!fileSaverResult.IsSuccessful)
                    {
                        throw new AggregateException(fileSaverResult.Exception);
                    }
                }
                else
                {
                    var directoryToZip = Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, ".local", "share", AppConstants.HavenoAppName);

                    var pipe = new Pipe();

                    var zipTask = Task.Run(async () =>
                    {
                        await using var pipeStream = pipe.Writer.AsStream();

                        using var archive = new ZipArchive(pipeStream, ZipArchiveMode.Create, leaveOpen: true);

                        foreach (var file in Directory.GetFiles(directoryToZip, "*", SearchOption.AllDirectories))
                        {
                            var relativePath = Path.GetRelativePath(directoryToZip, file);
                            if (relativePath == "monerod" || relativePath == "monero-wallet-rpc")
                                continue;

                            var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);

                            await using var entryStream = entry.Open();
                            await using var fileStream = File.OpenRead(file);

                            await fileStream.CopyToAsync(entryStream, BackupCts.Token);
                        }

                        archive.Dispose();

                        await pipeStream.FlushAsync(BackupCts.Token);
                        await pipe.Writer.CompleteAsync();
                    }, BackupCts.Token);

                    await using var backupStream = pipe.Reader.AsStream();

#pragma warning disable CA1416
                    // UI thread?
                    var fileSaverResult = await FileSaver.Default.SaveAsync(result.Folder.Path, $"haveno_backup_{DateTime.Now}-{Guid.NewGuid()}.zip", backupStream, BackupCts.Token);
#pragma warning restore CA1416

                    await zipTask;

                    if (!fileSaverResult.IsSuccessful)
                    {
                        throw new AggregateException(fileSaverResult.Exception);
                    }
                }
            }
            finally
            {
                PauseTokenSource.Resume();
            }
        });

        await backupTask;
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

        MoneroNodeConnectCancellationTokenSource = new(10_000);
        IsConnectingToMoneroNode = true;

        try
        {
            MoneroNodeUsername ??= string.Empty;
            MoneroNodePassword ??= string.Empty;

            // Does not throw an exception if fails?
            await HavenoXmrNodeService.SetAutoSwitchAsync(false);
            await HavenoXmrNodeService.SetMoneroNodeAsync(MoneroNodeUrl, MoneroNodeUsername, MoneroNodePassword, 0);

            var response = await HavenoXmrNodeService.GetMoneroNodeAsync();
            ConnectedMoneroNodeUrl = response.Url;

            while (!MoneroNodeConnectCancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    response = await HavenoXmrNodeService.GetMoneroNodeAsync(MoneroNodeConnectCancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (response.OnlineStatus == HavenoSharp.Models.OnlineStatus.ONLINE)
                    break;

                await Task.Delay(500);
            }

            if (MoneroNodeConnectCancellationTokenSource.IsCancellationRequested)
            {
                await HavenoXmrNodeService.SetAutoSwitchAsync(true);
                await HavenoXmrNodeService.RemoveConnectionAsync(MoneroNodeUrl);

                var moneroNodes = await HavenoXmrNodeService.GetConnectionsAsync();
                var previousAuthedNode = moneroNodes.FirstOrDefault(x => x.AuthenticationStatus == HavenoSharp.Models.AuthenticationStatus.AUTHENTICATED && x.OnlineStatus == HavenoSharp.Models.OnlineStatus.ONLINE);

                if (previousAuthedNode is not null) // Hope that previous node did not require auth
                    await HavenoXmrNodeService.SetMoneroNodeAsync(previousAuthedNode.Url, string.Empty, string.Empty, 0);

                response = await HavenoXmrNodeService.GetMoneroNodeAsync();
                ConnectedMoneroNodeUrl = response.Url;

                throw new Exception("Failed to connect to node.");
            }
            else
            {
                AppPreferences.Set(AppPreferences.CustomXmrNode, ConnectedMoneroNodeUrl);
            }
        }
        finally
        {
            ShowConnectToMoneroNodeModal = false;
            MoneroNodeUrl = string.Empty;
            MoneroNodeUsername = string.Empty;
            MoneroNodePassword = string.Empty;

            MoneroNodeConnectCancellationTokenSource.Dispose();
            MoneroNodeConnectCancellationTokenSource = null;
            IsConnectingToMoneroNode = false;
        }
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
