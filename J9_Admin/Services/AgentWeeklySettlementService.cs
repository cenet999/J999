using System.Globalization;
using AdminBlazor.Services;
using J9_Admin.Utils;

namespace J9_Admin.Services;

/// <summary>
/// 代理周结算统计服务
/// </summary>
public class AgentWeeklySettlementService
{
    public const decimal SourceAgentRate = 0.008m;
    public const decimal ParentAgentRate = 0.005m;
    public const decimal GrandAgentRate = 0.002m;
    public const string RuleVersion = "weekly-agent-rebate-v1";

    private readonly AdminContext _adminContext;

    public AgentWeeklySettlementService(AdminContext adminContext)
    {
        _adminContext = adminContext ?? throw new ArgumentNullException(nameof(adminContext));
    }

    public async Task<GenerateAgentWeeklySettlementResult> GenerateAsync(DateTime weekStartDate, IReadOnlyCollection<long>? sourceAgentIds = null)
    {
        var weekStart = NormalizeWeekStart(weekStartDate);
        var weekEnd = weekStart.AddDays(7);
        var fromUnix = TimeHelper.LocalToUnix(weekStart);
        var toUnix = TimeHelper.LocalToUnix(weekEnd);
        var weekKey = BuildWeekKey(weekStart);
        var orm = _adminContext.Orm;

        var agents = await orm.Select<DAgent>().ToListAsync();
        var agentMap = agents.ToDictionary(a => a.Id);
        var scopedAgentIds = sourceAgentIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? [];

        var transactionQuery = orm.Select<DTransAction>()
            .Include(t => t.DMember)
            .Where(t => t.Status == TransactionStatus.Success)
            .Where(t => t.TransactionType == TransactionType.Bet)
            .Where(t => t.TransactionTime >= fromUnix && t.TransactionTime < toUnix);

        if (scopedAgentIds.Count > 0)
            transactionQuery = transactionQuery.Where(t => scopedAgentIds.Contains(t.DAgentId));

        var transactions = await transactionQuery
            .ToListAsync();

        var rows = transactions
            .GroupBy(t => new { t.DMemberId, t.DAgentId })
            .Select(g => BuildRow(g.ToList(), agentMap, weekStart, weekEnd, weekKey, fromUnix, toUnix))
            .Where(r => r != null)
            .Cast<DAgentWeeklySettlement>()
            .ToList();

        using var uow = orm.CreateUnitOfWork();
        var lockedCount = await uow.Orm.Select<DAgentWeeklySettlement>()
            .Where(x => x.WeekStartDate == weekStart)
            .WhereIf(scopedAgentIds.Count > 0, x => scopedAgentIds.Contains(x.SourceAgentId))
            .Where(x => x.Status != AgentSettlementStatus.Draft)
            .CountAsync();

        if (lockedCount > 0)
            throw new InvalidOperationException($"{weekKey} 已存在确认、付款或作废数据，不能重算。");

        await uow.Orm.Delete<DAgentWeeklySettlement>()
            .Where(x => x.WeekStartDate == weekStart)
            .WhereIf(scopedAgentIds.Count > 0, x => scopedAgentIds.Contains(x.SourceAgentId))
            .ExecuteAffrowsAsync();

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
        var sourceRebate = RoundMoney(turnover * SourceAgentRate);
        var parentRebate = parentAgent == null ? 0 : RoundMoney(turnover * ParentAgentRate);
        var grandRebate = grandAgent == null ? 0 : RoundMoney(turnover * GrandAgentRate);

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
            Status = AgentSettlementStatus.Draft
        };
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

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
