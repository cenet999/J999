using System.Globalization;
using J9_Admin.Services.GameApi;
using J9_Admin.Utils;

namespace J9_Admin.Services;

/// <summary>
/// 会员注单落库：MS、XH 均同步北京时间近 6 小时注单，供 HTTP 同步接口与后台定时任务共用。
/// </summary>
public class GameBetHistorySyncService
{
    private const string MsBetSyncTimeFormat = "yyyy-MM-dd HH:mm:ss";
    private static readonly TimeSpan SyncWindow = TimeSpan.FromHours(6);

    private static readonly CultureInfo ChinaCulture = CultureInfo.GetCultureInfo("zh-CN");

    private readonly MSGameApi _msGameApi;
    private readonly XHGameApi _xhGameApi;

    public GameBetHistorySyncService(MSGameApi msGameApi, XHGameApi xhGameApi)
    {
        _msGameApi = msGameApi ?? throw new ArgumentNullException(nameof(msGameApi));
        _xhGameApi = xhGameApi ?? throw new ArgumentNullException(nameof(xhGameApi));
    }

    /// <summary>
    /// 按会员登录名同步 MS、XH 注单到本地（时间窗口与会员端「同步注单」一致）。
    /// </summary>
    public Task<GameBetHistorySyncOutcome> SyncMsAndXhForUsernameAsync(
        string syncUsername,
        CancellationToken cancellationToken = default)
        => SyncInternalAsync(syncUsername, cancellationToken);

    /// <summary>
    /// 全量同步：登录名留空（传给 MS/XH 为 <c>null</c>），各拉时间窗内全部注单并落库。
    /// </summary>
    public Task<GameBetHistorySyncOutcome> SyncMsAndXhAllAsync(CancellationToken cancellationToken = default)
        => SyncInternalAsync(null, cancellationToken);

    private async Task<GameBetHistorySyncOutcome> SyncInternalAsync(string? syncUsername, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? apiFilter = string.IsNullOrWhiteSpace(syncUsername) ? null : syncUsername.Trim();

        var now = TimeHelper.BeijingNow();
        var from = now.Add(-SyncWindow).ToString(MsBetSyncTimeFormat, ChinaCulture);
        var to = now.ToString(MsBetSyncTimeFormat, ChinaCulture);

        var msResult = await _msGameApi.SyncBetHistoryToDatabaseAsync(apiFilter, from, to);

        var xhResult = await _xhGameApi.SyncBetHistoryToDatabaseAsync(apiFilter, from, to);

        return new GameBetHistorySyncOutcome(msResult, xhResult, from, to);
    }
}

/// <summary>
/// 单次 MS+XH 同步结果（含 XH 请求时间窗，便于日志）。
/// </summary>
public sealed class GameBetHistorySyncOutcome
{
    public GameBetHistorySyncOutcome(
        MSBetHistorySyncResult ms,
        XHBetHistorySyncResult xh,
        string xhFrom,
        string xhTo)
    {
        Ms = ms;
        Xh = xh;
        XhFrom = xhFrom;
        XhTo = xhTo;
    }

    public MSBetHistorySyncResult Ms { get; }

    public XHBetHistorySyncResult Xh { get; }

    public string XhFrom { get; }

    public string XhTo { get; }

    public bool BothSuccess => Ms.Success && Xh.Success;

    public string BuildSummaryLine()
    {
        string MsLine() => Ms.Success
            ? $"MS：拉取 {Ms.RemoteFetched} 条，新 {Ms.Inserted}，更 {Ms.Updated}"
              + (Ms.SkippedNoSerial > 0 ? $"，跳过无单号 {Ms.SkippedNoSerial}" : "")
            : $"MS 失败：{Ms.Message ?? "未知错误"}";

        string XhLine() => Xh.Success
            ? $"XH：拉取 {Xh.RemoteFetched} 条，新 {Xh.Inserted}，更 {Xh.Updated}"
              + (Xh.SkippedNoSerial > 0 ? $"，跳过无单号 {Xh.SkippedNoSerial}" : "")
            : $"XH 失败：{Xh.Message ?? "未知错误"}（{XhFrom}~{XhTo}）";

        return $"{MsLine()} · {XhLine()}";
    }
}
