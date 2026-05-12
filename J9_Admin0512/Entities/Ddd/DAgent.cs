using FreeSql.DataAnnotations;

/// <summary>
/// 代理信息
/// </summary>
[Table(Name = "ddd_agent")]
public partial class DAgent : EntityModified
{
    /// <summary>
    /// 代理名（用于推广链接、注册时识别代理；非空时须全局唯一，见 Program 中部分唯一索引）
    /// </summary>
    [Column(StringLength = 100)]
    public string AgentName { get; set; } = "";

    /// <summary>
    /// 上级代理ID，0 表示顶级代理
    /// </summary>
    public long ParentId { get; set; }

    /// <summary>
    /// 代理类型
    /// </summary>
    public AgentType AgentType { get; set; } = AgentType.General;

    /// <summary>
    /// 代理状态
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Telegram ChatId
    /// </summary>
    [Column(StringLength = 100)]
    public string TelegramChatId { get; set; } = "";

    /// <summary>
    /// 代理域名
    /// </summary>
    [Column(StringLength = 200)]
    public string HomeUrl { get; set; }

    /// <summary>
    /// 服务器IP
    /// </summary>
    public string ServerIP { get; set; }

    /// <summary>
    /// 代理Logo
    /// </summary>
    [Column(StringLength = 200)]
    public string LogoUrl { get; set; }

    /// <summary>
    /// 登陆IP
    /// </summary>
    [Column(StringLength = 200)]
    public string IPWhiteList { get; set; } = "";

    /// <summary>
    /// 会员反水比例
    /// </summary>
    [Column(Scale = 4)]
    public decimal RebateRate { get; set; } = 0.0080m;

    /// <summary>
    /// 代理备注
    /// </summary>
    [Column(StringLength = -1)]
    public string Remark { get; set; }

    /// <summary>
    /// 公告内容
    /// </summary>
    [Column(StringLength = -1)]
    public string Announcements { get; set; }
}


/// <summary>
/// 代理类型枚举
/// </summary>
public enum AgentType
{
    /// <summary>
    /// 普通代理
    /// </summary>
    General = 0,

    /// <summary>
    /// 直属代理
    /// </summary>
    Direct = 1,

    /// <summary>
    /// 分销代理
    /// </summary>
    Distribution = 2,

    /// <summary>
    /// 高级代理
    /// </summary>
    Premium = 3,

    /// <summary>
    /// VIP代理
    /// </summary>
    VIP = 4
}
