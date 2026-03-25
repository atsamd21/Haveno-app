using HavenoSharp.Services;
using HavenoSharp.Models;
using Manta.Models;

namespace Manta.Singletons;

public class TradeStatisticsSingleton
{
    private int _delay = 5_000;
    private CancellationTokenSource _cancellationTokenSource = new();
    private readonly IServiceProvider _serviceProvider;

    public event Action<bool>? OnTradeStatisticsFetch;
    public List<TradeStatistic> TradeStatistics { get; private set; } = [];
    public Task? FetchTradeStatisticsTask {get; private set; }

    public TaskCompletionSource<bool> InitializedTCS { get; private set; } = new();

    public TradeStatisticsSingleton(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        FetchTradeStatisticsTask = Task.Run(FetchTradeStatisticsAsync);
    }

    public async Task FetchTradeStatisticsAsync()
    {
        while (true)
        {
            try
            {
                await PauseTokenSource.WaitWhilePausedAsync();

                OnTradeStatisticsFetch?.Invoke(true);

                using var scope = _serviceProvider.CreateScope();
                var tradeStatisticsService = _serviceProvider.GetRequiredService<IHavenoTradeStatisticsService>();

                // Really should be a way to fetch stats after a certain date
                TradeStatistics = await tradeStatisticsService.GetTradeStatisticsAsync(cancellationToken: _cancellationTokenSource.Token);

                if (!InitializedTCS.Task.IsCompleted)
                {
                    InitializedTCS.SetResult(true);
#if RELEASE
                    _delay = 60_000;
#endif
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                OnTradeStatisticsFetch?.Invoke(false);
            }

            await Task.Delay(_delay, _cancellationTokenSource.Token);
        }

    }
}
