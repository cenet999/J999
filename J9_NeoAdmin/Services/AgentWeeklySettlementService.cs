using System.Globalization;
using J9_NeoAdmin.Utils;

namespace J9_NeoAdmin.Services;

/// <summary>
/// 代理周结算统计服务
/// </summary>
public class AgentWeeklySettlementService
{
    public const decimal SourceAgentRate = 0.008m;
    public const decimal ParentAgentRate = 0.005m;
    public const decimal GrandAgentRate = 0.002m;

    // 不同游戏类型使用不同系数，最终返利比例 = 基础比例 × 游戏系数。
    private static readonly IReadOnlyDictionary<GameType, decimal> GameTypeRateMultipliers = new Dictionary<GameType, decimal>
    {
        [GameType.Live] = 0.2m,
        [GameType.Sports] = 0.2m,
        [GameType.Electronic] = 1m,
        [GameType.Fishing] = 1m,
        [GameType.Lottery] = 1m,
        [GameType.Card] = 1m,
        [GameType.Other] = 1m,
    };

    public const string RuleVersion = "weekly-agent-rebate-v2";

    private readonly IFreeSql _fsql;

    public AgentWeeklySettlementService(IFreeSql freeSql)
    {
        _fsql = freeSql;
    }

    public async Task<GenerateAgentWeeklySettlementResult> GenerateAsync(DateTime weekStartDate, IReadOnlyCollection<long>? sourceAgentIds = null)
    {
        var weekStart = NormalizeWeekStart(weekStartDate);
        var weekEnd = weekStart.AddDays(7);
        var fromUnix = TimeHelper.LocalToUnix(weekStart);
        var toUnix = TimeHelper.LocalToUnix(weekEnd);
        var weekKey = BuildWeekKey(weekStart);
        var orm = _fsql;

        var agents = await orm.Select<DAgent>().ToListAsync();
        var agentMap = agents.ToDictionary(a => a.Id);
        var scopedAgentIds = sourceAgentIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? [];

        var transactionQuery = orm.Select<DTransAction>()
            .Include(t => t.DMember)
            .Include(t => t.DGame)
            .Where(t => t.Status == TransactionStatus.Success)
            .Where(t => t.TransactionType == TransactionType.Bet)
            .Where(t => t.TransactionTime >= fromUnix && t.TransactionTime < toUnix);

        var regenScope = await ResolveWeekRegenScopeAsync(orm, weekStart, scopedAgentIds);
        if (regenScope.ShouldSkipWeek)
        {
            return new GenerateAgentWeeklySettlementResult
            {
                WeekStartDate = weekStart,
                WeekEndDate = weekEnd,
                WeekKey = weekKey
            };
        }

        if (regenScope.SourceAgentIds is { Count: > 0 })
            transactionQuery = transactionQuery.Where(t => regenScope.SourceAgentIds.Contains(t.DAgentId));

        var transactions = await transactionQuery
            .ToListAsync();

        var rows = transactions
            .GroupBy(t => new { t.DMemberId, t.DAgentId })
            .Select(g => BuildRow(g.ToList(), agentMap, weekStart, weekEnd, weekKey, fromUnix, toUnix))
            .Where(r => r != null)
            .Cast<DAgentWeeklySettlement>()
            .ToList();

        using var uow = orm.CreateUnitOfWork();

        var deleteQuery = uow.Orm.Delete<DAgentWeeklySettlement>()
            .Where(x => x.WeekStartDate == weekStart)
            .Where(x => x.Status == AgentSettlementStatus.Draft);
        if (regenScope.SourceAgentIds is { Count: > 0 })
            deleteQuery = deleteQuery.Where(x => regenScope.SourceAgentIds.Contains(x.SourceAgentId));
        await deleteQuery.ExecuteAffrowsAsync();

        if (rows.Count > 0)
            await uow.Orm.Insert(rows).ExecuteAffrowsAsync();

        uow.Commit();

        return new GenerateAgentWeeklySettlementResult
        {
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd,
            WeekKey = weekKey,
            RowCount = rows.Count,
            BetTransactionCount = rows.Sum(x => x.BetTransactionCount),
            TurnoverAmount = rows.Sum(x => x.TurnoverAmount),
            TotalRebateAmount = rows.Sum(x => x.TotalRebateAmount)
        };
    }

    public static DateTime NormalizeWeekStart(DateTime date)
    {
        var local = date.Date;
        var offset = ((int)local.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return local.AddDays(-offset);
    }

    public static string BuildWeekKey(DateTime weekStartDate)
    {
        var weekStart = NormalizeWeekStart(weekStartDate);
        var week = ISOWeek.GetWeekOfYear(weekStart);
        var year = ISOWeek.GetYear(weekStart);
        return $"{year}-W{week:00}";
    }

    /// <summary>
    /// 按当周实际投注流水，按游戏类型占比与系数累计加权，生成比例说明。
    /// </summary>
    public async Task<Dictionary<(long MemberId, long AgentId, DateTime WeekStart), string>> BuildRebateRateDetailsFromTransactionsAsync(
        IReadOnlyList<DAgentWeeklySettlement> settlements)
    {
        var result = new Dictionary<(long MemberId, long AgentId, DateTime WeekStart), string>();
        if (settlements.Count == 0)
            return result;

        foreach (var weekGroup in settlements.GroupBy(s => s.WeekStartDate))
        {
            var weekStart = weekGroup.Key;
            var weekEnd = weekStart.AddDays(7);
            var fromUnix = TimeHelper.LocalToUnix(weekStart);
            var toUnix = TimeHelper.LocalToUnix(weekEnd);

            var agentIds = weekGroup.Select(s => s.SourceAgentId).Distinct().ToList();
            var memberIds = weekGroup.Select(s => s.DMemberId).Distinct().ToList();

            var transactions = await _fsql.Select<DTransAction>()
                .Include(t => t.DGame)
                .Where(t => t.Status == TransactionStatus.Success)
                .Where(t => t.TransactionType == TransactionType.Bet)
                .Where(t => t.TransactionTime >= fromUnix && t.TransactionTime < toUnix)
                .Where(t => agentIds.Contains(t.DAgentId))
                .Where(t => memberIds.Contains(t.DMemberId))
                .ToListAsync();

            var txByKey = transactions
                .GroupBy(t => (t.DMemberId, t.DAgentId))
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var settlement in weekGroup)
            {
                var key = (settlement.DMemberId, settlement.SourceAgentId);
                var groupRows = txByKey.GetValueOrDefault(key) ?? [];
                result[(settlement.DMemberId, settlement.SourceAgentId, weekStart)] = BuildRebateRateDetail(groupRows);
            }
        }

        return result;
    }

    private sealed class WeekRegenScope
    {
        public bool ShouldSkipWeek { get; init; }
        public List<long>? SourceAgentIds { get; init; }
    }

    /// <summary>
    /// 解析本周可重算的直属代理范围：范围内若某代理已有确认/付款/作废快照，则跳过该代理，不影响同级其它代理。
    /// 未传范围（admin 全量）时，只要当周存在任意锁定记录就整周禁止重算。
    /// </summary>
    private static async Task<WeekRegenScope> ResolveWeekRegenScopeAsync(
        IFreeSql orm,
        DateTime weekStart,
        List<long> scopedAgentIds)
    {
        if (scopedAgentIds.Count == 0)
        {
            var anyLocked = await orm.Select<DAgentWeeklySettlement>()
                .Where(x => x.WeekStartDate == weekStart)
                .Where(x => x.Status != AgentSettlementStatus.Draft)
                .AnyAsync();
            if (anyLocked)
                throw new InvalidOperationException($"{BuildWeekKey(weekStart)} 已存在确认、付款或作废数据，不能重算。");

            return new WeekRegenScope();
        }

        var lockedAgentIds = await orm.Select<DAgentWeeklySettlement>()
            .Where(x => x.WeekStartDate == weekStart)
            .Where(x => x.Status != AgentSettlementStatus.Draft)
            .Where(x => scopedAgentIds.Contains(x.SourceAgentId))
            .Distinct()
            .ToListAsync(x => x.SourceAgentId);

        var regeneratableAgentIds = scopedAgentIds
            .Where(id => id > 0 && !lockedAgentIds.Contains(id))
            .Distinct()
            .ToList();
        if (regeneratableAgentIds.Count == 0)
            return new WeekRegenScope { ShouldSkipWeek = true };

        return new WeekRegenScope { SourceAgentIds = regeneratableAgentIds };
    }

    private static DAgentWeeklySettlement? BuildRow(
        List<DTransAction> groupRows,
        Dictionary<long, DAgent> agentMap,
        DateTime weekStart,
        DateTime weekEnd,
        string weekKey,
        long fromUnix,
        long toUnix)
    {
        var first = groupRows.FirstOrDefault();
        if (first == null || !agentMap.TryGetValue(first.DAgentId, out var sourceAgent))
            return null;

        var parentAgent = sourceAgent.ParentId > 0 && agentMap.TryGetValue(sourceAgent.ParentId, out var p) ? p : null;
        var grandAgent = parentAgent?.ParentId > 0 && agentMap.TryGetValue(parentAgent.ParentId, out var g) ? g : null;
        var turnover = groupRows.Sum(t => t.BetAmount);
        var validBet = groupRows.Sum(t => t.ValidBetAmount);
        var sourceRebate = RoundMoney(groupRows.Sum(t => t.BetAmount * SourceAgentRate * GetGameTypeMultiplier(t.DGame?.GameType)));
        var parentRebate = parentAgent == null
            ? 0
            : RoundMoney(groupRows.Sum(t => t.BetAmount * ParentAgentRate * GetGameTypeMultiplier(t.DGame?.GameType)));
        var grandRebate = grandAgent == null
            ? 0
            : RoundMoney(groupRows.Sum(t => t.BetAmount * GrandAgentRate * GetGameTypeMultiplier(t.DGame?.GameType)));

        return new DAgentWeeklySettlement
        {
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd,
            WeekKey = weekKey,
            DMemberId = first.DMemberId,
            MemberName = first.DMember?.Username ?? $"会员ID:{first.DMemberId}",
            SourceAgentId = sourceAgent.Id,
            SourceAgentName = GetAgentName(sourceAgent),
            ParentAgentId = parentAgent?.Id ?? 0,
            ParentAgentName = parentAgent == null ? "" : GetAgentName(parentAgent),
            GrandAgentId = grandAgent?.Id ?? 0,
            GrandAgentName = grandAgent == null ? "" : GetAgentName(grandAgent),
            TurnoverAmount = RoundMoney(turnover),
            ValidBetAmount = RoundMoney(validBet),
            BetTransactionCount = groupRows.Count,
            SourceRate = SourceAgentRate,
            ParentRate = ParentAgentRate,
            GrandRate = GrandAgentRate,
            SourceRebateAmount = sourceRebate,
            ParentRebateAmount = parentRebate,
            GrandRebateAmount = grandRebate,
            TotalRebateAmount = sourceRebate + parentRebate + grandRebate,
            FromUnixTime = fromUnix,
            ToUnixTime = toUnix,
            RuleVersion = RuleVersion,
            RebateRateDetail = BuildRebateRateDetail(groupRows),
            Status = AgentSettlementStatus.Draft
        };
    }

    public static string BuildRebateRateDetail(List<DTransAction> groupRows)
    {
        if (groupRows.Count == 0)
            return "";

        var turnover = groupRows.Sum(t => t.BetAmount);
        if (turnover <= 0)
            return "";

        var segments = groupRows
            .GroupBy(t => t.DGame?.GameType ?? GameType.Other)
            .Select(g => new
            {
                GameType = g.Key,
                Turnover = g.Sum(t => t.BetAmount),
                Multiplier = GetGameTypeMultiplier(g.Key)
            })
            .Where(x => x.Turnover > 0)
            .OrderByDescending(x => x.Turnover)
            .ToList();

        var weightedMultiplier = segments.Sum(x => x.Turnover * x.Multiplier) / turnover;
        var segmentFormula = string.Join("+", segments.Select(x =>
            $"{DescribeGameType(x.GameType)}{(x.Turnover / turnover * 100m):0.#}%×{x.Multiplier:0.#}"));

        if (segments.All(x => x.Multiplier >= 1m))
            return "W=1|全额系数";

        return $"W={weightedMultiplier.ToString("0.####", CultureInfo.InvariantCulture)}|{segmentFormula}";
    }

    private static string DescribeGameType(GameType? gameType) =>
        gameType switch
        {
            GameType.Live => "真人",
            GameType.Fishing => "捕鱼",
            GameType.Electronic => "电子",
            GameType.Lottery => "彩票",
            GameType.Sports => "体育",
            GameType.Card => "棋牌",
            GameType.Other => "电竞",
            _ => "其他"
        };

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal GetGameTypeMultiplier(GameType? gameType)
    {
        if (gameType == null)
            return 1m;

        return GameTypeRateMultipliers.TryGetValue(gameType.Value, out var multiplier) ? multiplier : 1m;
    }

    private static string GetAgentName(DAgent agent) =>
        string.IsNullOrWhiteSpace(agent.AgentName) ? $"ID:{agent.Id}" : agent.AgentName;
}

public class GenerateAgentWeeklySettlementResult
{
    public DateTime WeekStartDate { get; set; }
    public DateTime WeekEndDate { get; set; }
    public string WeekKey { get; set; } = "";
    public int RowCount { get; set; }
    public int BetTransactionCount { get; set; }
    public decimal TurnoverAmount { get; set; }
    public decimal TotalRebateAmount { get; set; }
}
