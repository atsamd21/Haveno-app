using HavenoSharp.Models;
using HavenoSharp.Services;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;

namespace Manta.Components.Pages;

public partial class Trades : ComponentBase, IDisposable
{
    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    public IHavenoDisputeService DisputeService { get; set; } = default!;
    [Inject]
    public IHavenoTradeService TradeService { get; set; } = default!;

    [Parameter]
    [SupplyParameterFromQuery]
    public int SelectedTabIndex 
    { 
        get;
        set;
    }

    public List<TradeInfo> FilteredTradeInfos 
    { 
        get 
        {
            switch(SelectedTabIndex)
            {
                case 0:
                    return OpenTradeInfos.OrderByDescending(x => x.Date).ToList();
                case 1:
                    return ClosedTradeInfos.OrderByDescending(x => x.Date).ToList();
                case 2:
                    return FailedTradeInfos.OrderByDescending(x => x.Date).ToList();
                case 3:
                    return [];
                default: 
                    return [];
            }
        }
    }

    public List<Dispute> Disputes { get; set; } = [];

    public TradeInfo? CompletedTradeInfo;

    public List<TradeInfo> OpenTradeInfos { get; set; } = [];
    public List<TradeInfo> ClosedTradeInfos { get; set; } = [];
    public List<TradeInfo> FailedTradeInfos { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        if (!NotificationSingleton.InitializedTCS.Task.IsCompleted)
        {
            // Timeout needed
            await NotificationSingleton.InitializedTCS.Task;
        }

        OpenTradeInfos = await TradeService.GetTradesAsync(Category.Open);
        ClosedTradeInfos = await TradeService.GetTradesAsync(Category.Closed);
        FailedTradeInfos = await TradeService.GetTradesAsync(Category.Failed);

        Disputes = await DisputeService.GetDisputesAsync();

        NotificationSingleton.OnChatMessage += HandleChatMessage;
        NotificationSingleton.OnTradeUpdate += HandleTradeUpdate;

        await base.OnInitializedAsync();
    }

    public void DisplayCompletedTradeModal(string tradeId)
    {
        CompletedTradeInfo = FilteredTradeInfos.FirstOrDefault(x => x.TradeId == tradeId);
        StateHasChanged();
    }

    public async Task GetDisputesAsync()
    {
        Disputes = await DisputeService.GetDisputesAsync();
    }

    public async void HandleChatMessage(ChatMessage chatMessage)
    {
        await InvokeAsync(() => {
            StateHasChanged();
        });
    }

    public async void HandleTradeUpdate(TradeInfo tradeInfo)
    {
        await InvokeAsync(() => {
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        NotificationSingleton.OnChatMessage -= HandleChatMessage;
        NotificationSingleton.OnTradeUpdate -= HandleTradeUpdate;
    }
}
