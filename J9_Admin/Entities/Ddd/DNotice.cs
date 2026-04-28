using FreeSql.DataAnnotations;

/// <summary>
/// 平台公告
/// </summary>
[Table(Name = "ddd_notice")]
public partial class DNotice : EntityModified
{
    /// <summary>
    /// 标题
    /// </summary>
    [Column(StringLength = 200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 内容
    /// </summary>
    [Column(StringLength = -1)]
    public string? Content { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 排序
    /// </summary>
    public int Sort { get; set; } = 0;
}
