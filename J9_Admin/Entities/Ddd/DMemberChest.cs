using FreeSql.DataAnnotations;

namespace J9_Admin.Entities;

/// <summary>
/// 玩家活跃度宝箱领取记录
/// </summary>
[Table(Name = "ddd_member_chest")]
public class DMemberChest : EntityCreated
{
    /// <summary>
    /// 会员ID
    /// </summary>
    public long DMemberId { get; set; }

    /// <summary>
    /// 活跃度档位（20, 40, 60, 80, 100）
    /// </summary>
    public int ActivityPointTarget { get; set; }

    /// <summary>
    /// 领取的奖励金
    /// </summary>
    public decimal RewardAmount { get; set; }

    /// <summary>
    /// 领取日期 (仅保留 Date)
    /// </summary>
    public DateTime ChestDate { get; set; }

    /// <summary>
    /// 会员导航属性
    /// </summary>
    [Navigate(nameof(DMemberId))]
    public DMember DMember { get; set; }
}
