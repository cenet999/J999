namespace J9_Admin.API.DTOs;

/// <summary>
/// 会员信息 DTO
/// </summary>
public class DMemberInfoResponse
{
    public long Id { get; set; }
    public string? Username { get; set; }
    public string? Nickname { get; set; }
    public string? Avatar { get; set; }
    public string? Telegram { get; set; }
    public decimal CreditAmount { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? CreatedTime { get; set; }
    public DateTime? UpdatedTime { get; set; }
    public long ParentId { get; set; }
    public long DAgentId { get; set; }

    /// <summary>
    /// 当前会员所属代理名（用于邀请链接等展示）
    /// </summary>
    public string? AgentName { get; set; }
    public string? USDTAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal RebateTotalAmount { get; set; }
    public decimal RebateAmount { get; set; }
    public decimal TodayBet { get; set; }
    public decimal TotalBet { get; set; }
    public string? WithdrawPassword { get; set; }
    public int ActivityPoint { get; set; }
    public string? InviteCode { get; set; }

    /// <summary>
    /// VIP等级（根据累计投注金额计算）
    /// </summary>
    public int VipLevel { get; set; }

    /// <summary>
    /// 根据累计投注金额计算VIP等级
    /// VIP0: 0, VIP1: 1万, VIP2: 5万, VIP3: 20万, VIP4: 50万, VIP5: 100万, VIP6: 500万
    /// </summary>
    public static int CalcVipLevel(decimal totalBet)
    {
        if (totalBet >= 5_000_000m) return 6;
        if (totalBet >= 1_000_000m) return 5;
        if (totalBet >= 500_000m) return 4;
        if (totalBet >= 200_000m) return 3;
        if (totalBet >= 50_000m) return 2;
        if (totalBet >= 10_000m) return 1;
        return 0;
    }
}
