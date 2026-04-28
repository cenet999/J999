using System.ComponentModel;
using FreeSql.DataAnnotations;

/// <summary>
/// 客服会话消息 - 隶属于某个会话 DChat 的一条消息
/// 设计目标：字段清晰、注释完整，涵盖文本与多媒体消息、已读状态与撤回编辑
/// </summary>
[Table(Name = "ddd_message")]
[Index("idx_msg_sent_at", nameof(SentAt))]
[Index("idx_msg_tg_reply", $"{nameof(TgChatId)},{nameof(TgTelegramMessageId)}")]
public partial class DMessage : EntityModified
{
    /// <summary>
    /// 发送方角色
    /// </summary>
    public MessageSenderRole SenderRole { get; set; }

    /// <summary>
    /// 文本内容
    /// </summary>
    [Column(StringLength = -1)]
    public string? Content { get; set; }

    /// <summary>
    /// 发送时间
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 消息状态
    /// </summary>
    public MessageStatus Status { get; set; } = MessageStatus.未读;

    /// <summary>
    /// 发送方IP
    /// </summary>
    [Column(StringLength = 64)]
    public string? SenderIp { get; set; }

    /// <summary>
    /// Telegram 私聊或群组的 ChatId（客户消息推送 TG 后写入，用于客服「回复该条 TG 消息」关联；多会话推送时仅保留第一条）
    /// </summary>
    public long? TgChatId { get; set; }

    /// <summary>
    /// Bot 在该 TG 会话中发出的通知消息的 MessageId
    /// </summary>
    public int? TgTelegramMessageId { get; set; }

}
public partial class DMessage{
    /// <summary>
    /// 会员ID
    /// </summary>
    public long? DMemberId { get; set; }

    /// <summary>
    /// 会员
    /// </summary>
    [Navigate(nameof(DMemberId))]
    public DMember DMember { get; set; }

}

/// <summary>
/// 消息发送方角色
/// </summary>
public enum MessageSenderRole
{
    /// <summary>
    /// 客户 (前端对应 0)
    /// </summary>
    Customer = 0,
    /// <summary>
    /// 客服 (前端对应 1)
    /// </summary>
    Agent = 1,
    /// <summary>
    /// 系统 (前端对应 2)
    /// </summary>
    System = 2
}


/// <summary>
/// 消息状态
/// </summary>
public enum MessageStatus
{
    /// <summary>
    /// 消息未读
    /// </summary>
    [Description("未读")]
    未读 = 0,

    /// <summary>
    /// 消息已被阅读
    /// </summary>
    [Description("已读")]
    已读 = 1,

    /// <summary>
    /// 消息已得到回复
    /// </summary>
    [Description("已回复")]
    已回复 = 2,

    /// <summary>
    /// 消息已被发送方撤回
    /// </summary>
    [Description("已撤回")]
    已撤回 = 3
}


