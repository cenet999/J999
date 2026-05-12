namespace J9_Admin.API.DTOs;

/// <summary>
/// 单条邀请记录（被邀请人脱敏展示）
/// </summary>
public class InviteRecordItemDto
{
    public string DisplayName { get; set; } = "";
    public DateTime RegisteredAt { get; set; }
}

/// <summary>
/// 邀请排行榜一行（同代理下按直接邀请人数）
/// </summary>
public class InviteLeaderboardItemDto
{
    public int Rank { get; set; }
    public string DisplayName { get; set; } = "";
    public int InviteCount { get; set; }
    public bool IsCurrentUser { get; set; }
}

/// <summary>
/// 邀请中心聚合数据
/// </summary>
public class InviteCenterResponseDto
{
    /// <summary>
    /// 当前会员所属代理 Id，用于拼接带代理的注册链接（与注册接口 AgentId 一致）
    /// </summary>
    public long AgentId { get; set; }

    /// <summary>
    /// 当前会员所属代理名，用于注册链接 query：agentName（与注册接口 AgentName 一致）
    /// </summary>
    public string AgentName { get; set; } = "";

    public string InviteCode { get; set; } = "";
    public int TotalInvites { get; set; }
    public int TodayInvites { get; set; }
    /// <summary>
    /// 已通过「每日任务-邀请好友」领取的奖励合计（活动入账流水）
    /// </summary>
    public decimal TotalInviteTaskReward { get; set; }
    /// <summary>
    /// 在同代理邀请榜中的名次，0 表示尚无有效邀请或未上榜逻辑外
    /// </summary>
    public int MyRank { get; set; }
    public int MyInviteCount { get; set; }
    public List<InviteRecordItemDto> Records { get; set; } = new();
    public List<InviteLeaderboardItemDto> Leaderboard { get; set; } = new();
}
