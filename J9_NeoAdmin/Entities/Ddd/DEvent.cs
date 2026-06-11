using FreeSql.DataAnnotations;


/// <summary>
/// 活动
/// </summary>
[Table(Name = "ddd_event")]
public partial class DEvent : EntityModified
{
    /// <summary>
    /// 活动名称
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 活动描述
    /// </summary>
    [Column(StringLength = -1)]
    public string Summary { get; set; }

    /// <summary>
    /// 发放数量
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// 活动时间
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 展示结束时间
    /// </summary>
    public DateTime EndTime { get; set; } = DateTime.Now.AddDays(120);

    /// <summary>
    /// 活动类型
    /// </summary>
    public string Type { get; set; } = "Promotion";

    /// <summary>
    /// 活动状态
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 排序
    /// </summary>
    public int Sort { get; set; }

    /// <summary>
    /// 图片地址
    /// </summary>
    [Column(StringLength = -1)]
    public string BannerUrl { get; set; }
}

public partial class DEvent
{
    /// <summary>
    /// 代理
    /// </summary>
    [Navigate(nameof(DAgentId))]
    public DAgent DAgent { get; set; }

    /// <summary>
    /// 代理ID
    /// </summary>
    public long DAgentId { get; set; }
}