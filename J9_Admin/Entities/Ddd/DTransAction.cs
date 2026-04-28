using System.Transactions;
using FreeSql.DataAnnotations;


/// <summary>
/// 交易记录
/// </summary>
[Table(Name = "ddd_transaction")]
public partial class DTransAction : EntityModified
{
    /// <summary>
    /// 交易类型
    /// </summary>
    public TransactionType TransactionType { get; set; }

    /// <summary>
    /// 交易前金额
    /// </summary>
    public decimal BeforeAmount { get; set; }
    /// <summary>
    /// 交易后金额
    /// </summary>
    public decimal AfterAmount { get; set; }

    /// <summary>
    /// 投注金额
    /// </summary>
    public decimal BetAmount { get; set; }

    /// <summary>
    /// 赢/输金额
    /// </summary>
    public decimal ActualAmount { get; set; }

    /// <summary>
    /// 货币代码
    /// </summary>
    public string CurrencyCode { get; set; }

    /// <summary>
    /// 记录唯一编号
    /// </summary>
    public string SerialNumber { get; set; }

    /// <summary>
    /// 游戏局号
    /// </summary>
    public string GameRound { get; set; }

    /// <summary>
    /// 体育游戏详细数据
    /// </summary>
    [Column(StringLength = -1)]
    public string Data { get; set; } = "";

    /// <summary>
    /// 交易时间（Unix 时间戳，UTC，秒；与 XH/MS 注单 <c>betTime</c> 一致）
    /// </summary>
    public long TransactionTime { get; set; }

    /// <summary>
    /// 交易状态
    /// </summary>
    public TransactionStatus Status { get; set; }

    /// <summary>
    /// 交易描述
    /// </summary>
    [Column(StringLength = -1)]
    public string Description { get; set; } = "";

    /// <summary>
    /// 是否已反水
    /// </summary>
    public bool IsRebate { get; set; } = false;

}

public partial class DTransAction
{
    /// <summary>
    /// 会员ID
    /// </summary>
    public long DMemberId { get; set; }

    /// <summary>
    /// 会员
    /// </summary>
    public DMember DMember { get; set; }

    /// <summary>
    /// 游戏信息ID
    /// </summary>
    public long DGameId { get; set; }

    /// <summary>
    /// 游戏信息
    /// </summary>
    public DGame DGame { get; set; }

    /// <summary>
    /// 代理
    /// </summary>
    [Navigate(nameof(DAgentId))]
    public DAgent DAgent { get; set; }

    /// <summary>
    /// 代理ID
    /// </summary>
    public long DAgentId { get; set; }

    /// <summary>
    /// 关联交易
    /// </summary>
    [Navigate(nameof(RelatedTransActionId))]
    public DTransAction RelatedTransAction { get; set; }

    /// <summary>
    /// 关联交易Id
    /// </summary>
    public long? RelatedTransActionId { get; set; } = 0;

}

public enum TransactionType
{
    /// <summary>
    /// 上分
    /// </summary>
    TransferIn,
    /// <summary>
    /// 下分
    /// </summary>
    TransferOut,

    /// <summary>
    /// 投注
    /// </summary>
    Bet,
    /// <summary>
    /// 提现
    /// </summary>     
    Withdraw,
    /// <summary>
    /// 充值
    /// </summary>
    Recharge,

    /// <summary>
    /// 退款
    /// </summary>
    Refund,
    /// <summary>
    /// 返佣
    /// </summary>
    Commission,
    /// <summary>
    /// 活动
    /// </summary>
    Activity,
    /// <summary>
    /// 其他
    /// </summary>
    Other,
    /// <summary>
    /// 代理上分
    /// </summary>
    AgentTransferIn,
    /// <summary>
    /// 代理下分
    /// </summary>
    AgentTransferOut,
    /// <summary>
    /// 反水
    /// </summary>
    Rebate,
    /// <summary>
    /// 代理充值
    /// </summary>
    AgentRecharge,
    /// <summary>
    /// 登录
    /// </summary>
    Login,
    /// <summary>
    /// 签到
    /// </summary>
    CheckIn,
    /// <summary>
    /// 注册
    /// </summary>
    Register,
}

public enum TransactionStatus
{
    /// <summary>
    /// 成功
    /// </summary>
    Success,
    /// <summary>
    /// 失败
    /// </summary>
    Failed,
    /// <summary>
    /// 处理中
    /// </summary>
    Processing,
    /// <summary>
    /// 待处理
    /// </summary>
    Pending,
    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled,
    /// <summary>
    /// 已退款
    /// </summary>
    Refunded,
}