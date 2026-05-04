using FreeSql.DataAnnotations;

/// <summary>
/// 代理周结算统计
/// </summary>
[Table(Name = "ddd_agent_weekly_settlement")]
[Index("idx_agent_weekly_member_agent", $"{nameof(WeekStartDate)},{nameof(DMemberId)},{nameof(SourceAgentId)}", IsUnique = true)]
[Index("idx_agent_weekly_agent", $"{nameof(WeekStartDate)},{nameof(SourceAgentId)},{nameof(ParentAgentId)},{nameof(GrandAgentId)}")]
public class DAgentWeeklySettlement : EntityModified
{
    /// <summary>
    /// 周开始日期（周一，本地时间）
    /// </summary>
    public DateTime WeekStartDate { get; set; }

    /// <summary>
    /// 周结束日期（下周一，本地时间，不包含）
    /// </summary>
    public DateTime WeekEndDate { get; set; }

    /// <summary>
    /// 周标识，例如 2026-W18
    /// </summary>
    [Column(StringLength = 20)]
    public string WeekKey { get; set; } = "";

    /// <summary>
    /// 会员ID
    /// </summary>
    public long DMemberId { get; set; }

    /// <summary>
    /// 会员名快照
    /// </summary>
    [Column(StringLength = 100)]
    public string MemberName { get; set; } = "";

    /// <summary>
    /// 会员流水归属代理ID
    /// </summary>
    public long SourceAgentId { get; set; }

    /// <summary>
    /// 会员流水归属代理名快照
    /// </summary>
    [Column(StringLength = 100)]
    public string SourceAgentName { get; set; } = "";

    /// <summary>
    /// 父代理ID
    /// </summary>
    public long ParentAgentId { get; set; }

    /// <summary>
    /// 父代理名快照
    /// </summary>
    [Column(StringLength = 100)]
    public string ParentAgentName { get; set; } = "";

    /// <summary>
    /// 爷代理ID
    /// </summary>
    public long GrandAgentId { get; set; }

    /// <summary>
    /// 爷代理名快照
    /// </summary>
    [Column(StringLength = 100)]
    public string GrandAgentName { get; set; } = "";

    /// <summary>
    /// 投注流水合计
    /// </summary>
    public decimal TurnoverAmount { get; set; }

    /// <summary>
    /// 有效投注额合计
    /// </summary>
    public decimal ValidBetAmount { get; set; }

    /// <summary>
    /// 投注笔数
    /// </summary>
    public int BetTransactionCount { get; set; }

    /// <summary>
    /// 归属代理返利比例
    /// </summary>
    [Column(Scale = 4)]
    public decimal SourceRate { get; set; } = 0.008m;

    /// <summary>
    /// 父代理返利比例
    /// </summary>
    [Column(Scale = 4)]
    public decimal ParentRate { get; set; } = 0.005m;

    /// <summary>
    /// 爷代理返利比例
    /// </summary>
    [Column(Scale = 4)]
    public decimal GrandRate { get; set; } = 0.002m;

    /// <summary>
    /// 归属代理返利金额
    /// </summary>
    public decimal SourceRebateAmount { get; set; }

    /// <summary>
    /// 父代理返利金额
    /// </summary>
    public decimal ParentRebateAmount { get; set; }

    /// <summary>
    /// 爷代理返利金额
    /// </summary>
    public decimal GrandRebateAmount { get; set; }

    /// <summary>
    /// 返利总额
    /// </summary>
    public decimal TotalRebateAmount { get; set; }

    /// <summary>
    /// 原始流水起始时间戳（含）
    /// </summary>
    public long FromUnixTime { get; set; }

    /// <summary>
    /// 原始流水结束时间戳（不含）
    /// </summary>
    public long ToUnixTime { get; set; }

    /// <summary>
    /// 规则版本
    /// </summary>
    [Column(StringLength = 50)]
    public string RuleVersion { get; set; } = "weekly-agent-rebate-v1";

    /// <summary>
    /// 结算状态
    /// </summary>
    public AgentSettlementStatus Status { get; set; } = AgentSettlementStatus.Draft;
}

public enum AgentSettlementStatus
{
    /// <summary>
    /// 草稿，可重算
    /// </summary>
    Draft = 0,

    /// <summary>
    /// 已确认
    /// </summary>
    Confirmed = 1,

    /// <summary>
    /// 已付款
    /// </summary>
    Paid = 2,

    /// <summary>
    /// 已作废
    /// </summary>
    Voided = 3
}
