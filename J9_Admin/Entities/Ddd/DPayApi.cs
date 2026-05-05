using FreeSql.DataAnnotations;


/// <summary>
/// 支付API
/// </summary>
[Table(Name = "ddd_pay_api")]
public partial class DPayApi : EntityModified
{

    /// <summary>
    /// IP标识
    /// </summary>
    public ServerIP IP { get; set; }


    /// <summary>
    /// 支付方式名称
    /// </summary>
    public string PayMethodName { get; set; }

    /// <summary>
    /// 通道编码
    /// </summary>
    public string ChannelCode { get; set; }

    /// <summary>
    /// 支付方式
    /// </summary>
    public PayMethod PayMethod { get; set; }

    /// <summary>
    /// 是否用户输入
    /// </summary>
    public bool IsUserInput { get; set; } = true;

    /// <summary>
    /// 默认值
    /// </summary>
    public string DefaultValue { get; set; } = "100,500,1000,3000,5000,10000";

    /// <summary>
    /// 最小金额
    /// </summary>
    public decimal MinAmount { get; set; }=10;
    
    /// <summary>
    /// 最大金额
    /// </summary>
    public decimal MaxAmount { get; set; }=20000;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 排序
    /// </summary>
    public int Sort { get; set; }

    /// <summary>
    /// 成功率
    /// </summary>
    public decimal SuccessRate { get; set; } = 50;



}

public enum PayMethod
{

    /// <summary>
    /// 支付宝
    /// </summary>
    Alipay,
    
    /// <summary>
    /// 微信
    /// </summary>
    Wechat,
    
    /// <summary>
    /// 银行卡
    /// </summary>
    BankCard,

    /// <summary>
    /// USDT
    /// </summary>
    USDT,
    
    /// <summary>
    /// 其他
    /// </summary>
    Other,
    
    
}

public enum ServerIP
{
    USDT = 0,

    POPO = 1,
}
