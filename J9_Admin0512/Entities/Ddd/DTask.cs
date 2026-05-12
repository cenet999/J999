using FreeSql.DataAnnotations;

namespace J9_Admin.Entities;

/// <summary>
/// 任务配置
/// </summary>
[Table(Name = "ddd_task")]
public class DTask : EntityModified
{
    /// <summary>
    /// 任务名称，如"登录游戏"
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 任务描述
    /// </summary>
    [Column(StringLength = -1)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 任务类型：如 Login, Bet, Recharge, CheckIn
    /// </summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// 目标值（需要达到的数值），如 1、500 等
    /// </summary>
    public int TargetValue { get; set; }

    /// <summary>
    /// 完成该任务奖励的金币
    /// </summary>
    public decimal RewardAmount { get; set; }

    /// <summary>
    /// 完成该任务增加的活跃度
    /// </summary>
    public int ActivityPoint { get; set; }

    /// <summary>
    /// 任务图标
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// 跳转路径
    /// </summary>
    public string JumpPath { get; set; } = string.Empty;

    /// <summary>
    /// 状态
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 排序
    /// </summary>
    public int Sort { get; set; }
}
