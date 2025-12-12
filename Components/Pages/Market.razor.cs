using Blazored.LocalStorage;
using HavenoSharp.Models;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Manta.Extensions;

namespace Manta.Components.Pages;

public partial class Market : ComponentBase, IDisposable
{
    private int _index = -1;

    private AxisChartOptions _axisChartOptions = new() 
    { 
        MatchBoundsToSize = true,
        XAxisLabelRotation = 45
    };

    private ChartOptions _options = new()
    {
        YAxisLines = false,
        YAxisTicks = 1,
        MaxNumYAxisTicks = 10,
        YAxisRequireZeroPoint = true,
        XAxisLines = false,
        LineStrokeWidth = 8,
        ShowLabels = false,
        ShowLegend = false,
        ShowLegendLabels = false,
        ShowToolTips = false,
        YAxisLabelPosition = YAxisLabelPosition.Left,
        ChartPalette = ["#0cb952"]
    };

    private List<ChartSeries> _series = [];

    private readonly string[] _xAxisLabels = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    public string Interval { get; set; } = "Year";

    public int Year
    { 
        get;
        set 
        { 
            field = value;
            ProcessTradeStatistics();
        } 
    }

    public List<int> Years { get; set; } = [];

    public bool IsFetching { get; set; }
    public string Version { get; set; } = string.Empty;
    public WalletInfo? WalletInfo { get; set; }
    public List<TradeStatistic> FilteredTradeStatistics { get { return FilterTradeStatistics(); } }
    public List<TradeStatistic> TradeStatistics { get; private set { field = value; PageCount = TradeStatistics.Count / PageSize; } } = [];

    public string PreferredCurrency { get; set; } = string.Empty;
    public string CurrentMarketPrice { get; set; } = string.Empty;

    public int PageSize { get; set; } = 20;
    public int CurrentPage { get; set; }
    public int PageCount { get; set; }

    public bool ShowNotice { get; set; }

    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;
    [Inject]
    public IJSRuntime JS { get; set; } = default!;
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;
    [Inject]
    public TradeStatisticsSingleton TradeStatisticsSingleton { get; set; } = default!;

    public List<TradeStatistic> FilterTradeStatistics()
    {
        return TradeStatistics
            .OrderByDescending(x => x.Date)
            .Skip(CurrentPage * PageSize)
            .Take(PageSize)
            .ToList();
    }

    public void NextPage()
    {
        if (CurrentPage < PageCount) 
        {
            CurrentPage++;
        }
    }

    public void PreviousPage()
    {
        if (CurrentPage > 0)
        {
            CurrentPage--;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        while (true)
        {
            try
            {
                IsFetching = true;
                StateHasChanged();

                await BalanceSingleton.InitializedTCS.Task;

                PreferredCurrency = await LocalStorage.GetItemAsStringAsync("preferredCurrency") ?? CurrencyCultureInfo.FallbackCurrency;

                ShowNotice = !AppPreferences.Get<bool>(AppPreferences.InitialNoticeAcknowledged);

                try
                {
                    CurrentMarketPrice = BalanceSingleton.MarketPriceInfoDictionary[PreferredCurrency].ToString("0.00");
                }
                catch (KeyNotFoundException)
                {
                    CurrentMarketPrice = BalanceSingleton.MarketPriceInfoDictionary[CurrencyCultureInfo.FallbackCurrency].ToString("0.00");
                    PreferredCurrency = CurrencyCultureInfo.FallbackCurrency;
                }

                break;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            await Task.Delay(5_000);
        }

        TradeStatistics = TradeStatisticsSingleton.TradeStatistics;
        
        ProcessTradeStatistics();

        WalletInfo = BalanceSingleton.WalletInfo;
        IsFetching = false;

        BalanceSingleton.OnBalanceFetch += HandleBalanceFetch;
        TradeStatisticsSingleton.OnTradeStatisticsFetch += HandleTradeStatisticsFetch;

        await base.OnInitializedAsync();
    }

    private void AcknowledgeNotice()
    {
        AppPreferences.Set(AppPreferences.InitialNoticeAcknowledged, true);
    }

    private async void HandleBalanceFetch(bool isFetching)
    {
        await InvokeAsync(() => {
            WalletInfo = BalanceSingleton.WalletInfo;
            StateHasChanged();
        });
    }

    private async void HandleTradeStatisticsFetch(bool isFetching)
    {
        await InvokeAsync(() => {
            TradeStatistics = TradeStatisticsSingleton.TradeStatistics;
            ProcessTradeStatistics();

            StateHasChanged();
        });
    }

    // UpdateChart
    public void ProcessTradeStatistics()
    {
        if (TradeStatistics.Count ==  0) 
            return;

        if (Years.Count == 0) 
        {
            Years = TradeStatistics
                .OrderBy(x => x.Date)
                // ToDateTime() temporary
                .GroupBy(x => x.Date.ToDateTime().Year)
                .Select(x => x.Key)
                .ToList();

            Year = Years.LastOrDefault();
        }

        var volumePerMonth = new double[12];

        for (int i = 0; i < volumePerMonth.Length; i++)
        {
            volumePerMonth[i] = (double)TradeStatistics
                .Where(x => x.Date.ToDateTime().Month == i + 1 && x.Date.ToDateTime().Year == Year)
                .Aggregate(0m, (x, y) => x + ((ulong)y.Amount).ToMonero());
        }

        _series = new List<ChartSeries>()
        {
            new ChartSeries() { Name = "Volume in XMR", Data = volumePerMonth },
        };
    }

    public void Dispose()
    {
        BalanceSingleton.OnBalanceFetch -= HandleBalanceFetch;
        TradeStatisticsSingleton.OnTradeStatisticsFetch -= HandleTradeStatisticsFetch;
    }
}
