using FreeSql.DataAnnotations;

/// <summary>
/// IP白名单
/// </summary>
[Table(Name = "ip_whitelist")]
public partial class IpWhitelist : EntityModified
{
    /// <summary>
    /// IP地址
    /// </summary>
    [Column(StringLength = 300)]
    public string IpAddress { get; set; }

    /// <summary>
    /// 描述/备注
    /// </summary>
    [Column(StringLength = 500)]
    public string Description { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 最后访问时间
    /// </summary>
    public DateTime? LastAccessTime { get; set; }

    /// <summary>
    /// 访问次数
    /// </summary>
    public int AccessCount { get; set; }
}
