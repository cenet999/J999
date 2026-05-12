using FreeSql.DataAnnotations;


/// <summary>
/// 轮播图
/// </summary>
[Table(Name = "ddd_banner")]
public partial class DBanner : EntityModified
{
    /// <summary>
    /// 标题
    /// </summary>
    [Column(StringLength = 200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 副标题/描述
    /// </summary>
    [Column(StringLength = 500)]
    public string? Description { get; set; }

    /// <summary>
    /// 图片URL
    /// </summary>
    [Column(StringLength = 500)]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// 跳转链接
    /// </summary>
    [Column(StringLength = 500)]
    public string? LinkUrl { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    [Column(StringLength = 50)]
    public string? Tag { get; set; }

    /// <summary>
    /// 按钮文字
    /// </summary>
    [Column(StringLength = 50)]
    public string? CtaText { get; set; }

    /// <summary>
    /// 颜色主题 (比如 #7B5CFF,#FF5FA2)
    /// </summary>
    [Column(StringLength = 500)]
    public string? GradientColors { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 排序
    /// </summary>
    public int Sort { get; set; } = 0;
}
