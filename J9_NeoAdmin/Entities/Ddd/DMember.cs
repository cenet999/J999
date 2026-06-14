using FreeSql.DataAnnotations;


/// <summary>
/// 会员
/// </summary>
[Table(Name = "SysUser")]
[Index("idx_member_username", nameof(Username), IsUnique = true)]
[Index("idx_member_invite_code", nameof(InviteCode), IsUnique = true)]
public partial class DMember : SysUser
{
    /// <summary>
    /// 父ID
    /// </summary>
    public long ParentId { get; set; }

    /// <summary>
    /// 浏览器指纹
    /// </summary>
    public string BrowserFingerprint { get; set; } = "";

    /// <summary>
    /// 邀请码
    /// </summary>
    public string InviteCode { get; set; } = "";

    /// <summary>
    /// 账户余额（人民币，元）
    /// </summary>
    public decimal CreditAmount { get; set; } = 0;

    /// <summary>
    /// 反水开关
    /// </summary>
    public bool IsRebateSwitch { get; set; } = true;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedTime { get; set; }

    /// <summary>
    /// 数据同步时间（PostgreSQL 历史字段；未同步时为 default）
    /// </summary>
    public DateTime SyncTime { get; set; }

    /// <summary>
    /// 会员Telegram
    /// </summary>
    public string Telegram { get; set; }

    /// <summary>
    /// 邮箱地址
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// 真实姓名
    /// </summary>
    public string RealName { get; set; } = "";

    /// <summary>
    /// 取款密码
    /// </summary>
    public string WithdrawPassword { get; set; }

    /// <summary>
    /// USDT地址
    /// </summary>
    public string USDTAddress { get; set; }

    /// <summary>
    /// 银行名称
    /// </summary>
    public string BankName { get; set; } = "";

    /// <summary>
    /// 银行账号
    /// </summary>
    public string BankAccount { get; set; } = "";

    /// <summary>
    /// 支付宝账号
    /// </summary>
    public string AlipayAccount { get; set; } = "";

    /// <summary>
    /// 注册IP
    /// </summary>
    public string RegisterIp { get; set; } = "";

    /// <summary>
    /// 头像
    /// </summary>
    public string Avatar { get; set; } = "";

    /// <summary>
    /// 最后签到日期
    /// </summary>
    public DateTime? LastCheckInDate { get; set; }

    /// <summary>
    /// 连续签到天数
    /// </summary>
    public int ContinuousCheckInDays { get; set; } = 0;

    /// <summary>
    /// 活跃度/积分
    /// </summary>
    public int ActivityPoint { get; set; } = 0;

}


public partial class DMember
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







