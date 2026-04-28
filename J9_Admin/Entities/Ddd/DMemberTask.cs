using FreeSql.DataAnnotations;

namespace J9_Admin.Entities;

/// <summary>
/// 玩家任务进度
/// </summary>
[Table(Name = "ddd_member_task")]
public class DMemberTask : EntityModified
{
    /// <summary>
    /// 会员ID
    /// </summary>
    public long DMemberId { get; set; }

    /// <summary>
    /// 任务配置ID
    /// </summary>
    public long DTaskId { get; set; }

    /// <summary>
    /// 任务进度所属日期 (仅保留 Date)
    /// </summary>
    public DateTime TaskDate { get; set; }

    /// <summary>
    /// 当前进度值
    /// </summary>
    public int CurrentValue { get; set; }

    /// <summary>
    /// 状态：0=未完成，1=可领取，2=已领取
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 导航属性
    /// </summary>
    [Navigate(nameof(DMemberId))]
    public DMember DMember { get; set; }

    /// <summary>
    /// 任务导航属性
    /// </summary>
    [Navigate(nameof(DTaskId))]
    public DTask DTask { get; set; }
}
