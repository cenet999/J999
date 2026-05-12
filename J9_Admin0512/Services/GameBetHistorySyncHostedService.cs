using Microsoft.Extensions.Logging;

namespace J9_Admin.Services;

/// <summary>
/// 后台循环将 MS、XH 注单同步到本地库（每 1 小时一轮；登录名留空，单次全站拉取北京时间近 6 小时订单）。
/// </summary>
public sealed class GameBetHistorySyncHostedService : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GameBetHistorySyncHostedService> _logger;

    public GameBetHistorySyncHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<GameBetHistorySyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "全量注单同步后台服务已启动，首轮将在 {IntervalHours} 小时后执行，后续按相同间隔轮询",
            SyncInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SyncInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await RunOneRoundAsync(stoppingToken);
        }

        _logger.LogInformation("全量注单同步后台服务已停止");
    }

    private async Task RunOneRoundAsync(CancellationToken stoppingToken)
    {
        const string roundName = nameof(RunOneRoundAsync);
        using var scope = _scopeFactory.CreateScope();

        _logger.LogInformation(
            "[{Round}] 开始后台全量注单同步（MS、XH 均拉取北京时间近6小时订单）",
            roundName);

        try
        {
            var syncService = scope.ServiceProvider.GetRequiredService<GameBetHistorySyncService>();
            var outcome = await syncService.SyncMsAndXhAllAsync(stoppingToken);

            var level = outcome.BothSuccess ? LogLevel.Information : LogLevel.Warning;
            _logger.Log(level, "[{Round}] 全站注单同步 {Summary}", roundName, outcome.BuildSummaryLine());
            _logger.LogInformation(
                "[{Round}] 本轮结束：{Status}",
                roundName,
                outcome.BothSuccess ? "MS 与 XH 均成功" : "存在失败或部分失败");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Round}] 后台全量注单同步失败", roundName);
        }
    }
}
