using System.ComponentModel.DataAnnotations;
using AdminBlazor.Infrastructure.Encrypt;
using BootstrapBlazor.Components;
using FreeScheduler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using J9_Admin.Utils;
using RestSharp;

namespace J9_Admin.API;

/// <summary>
/// 消息接口
/// </summary>
[ApiController]
[Route("api/message")]
[Tags("消息系统")]
public class MessageService : BaseService
{
    private static readonly TimeSpan MessageWindow = TimeSpan.FromHours(24);
    private const string FirstConsultationAutoReply = """
排队中，请稍候。
为保持连接，请勿关闭当前页面。
等待期间，您仍可继续咨询相关服务问题。
""";

    private readonly TGMessageApi _TGMessageApi;
    public MessageService(FreeSqlCloud freeSqlCloud, Scheduler scheduler, ILogger<MessageService> logger, AdminContext adminContext, IConfiguration configuration, TGMessageApi TGMessageApi, IWebHostEnvironment webHostEnvironment)
        : base(freeSqlCloud, scheduler, logger, adminContext, configuration, webHostEnvironment)
    {
        _TGMessageApi = TGMessageApi ?? throw new ArgumentNullException(nameof(TGMessageApi));
    }

    /// <summary>
    /// 获取消息列表
    /// </summary>
    [HttpGet($"@{nameof(GetMessages)}")]
    public async Task<ApiResult> GetMessages()
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        var windowStart = DateTime.Now.Subtract(MessageWindow);

        var messages = await _fsql.Select<DMessage>()
            .Where(m => (m.DMemberId == userId || m.DMemberId == null) && m.SentAt >= windowStart)
            .OrderByDescending(m => m.SentAt)
            .Limit(30)
            .ToListAsync();
        // 系统消息不计未读，强制返回已读状态
        foreach (var m in messages)
        {
            if (m.SenderRole == MessageSenderRole.System)
                m.Status = MessageStatus.已读;
        }
        return ApiResult.Success.SetData(messages);
    }

    /// <summary>
    /// 标记消息已读
    /// </summary>
    [HttpPost($"@{nameof(MarkAsRead)}")]
    public async Task<ApiResult> MarkAsRead(long id)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        await _fsql.Update<DMessage>()
            .Set(m => m.Status, MessageStatus.已读)
            .Where(m => m.Id == id && m.DMemberId == userId && m.Status == MessageStatus.未读)
            .ExecuteAffrowsAsync();

        return ApiResult.Success.SetMessage("标记已读成功");
    }

    /// <summary>
    /// 全部标记已读
    /// </summary>
    [HttpPost($"@{nameof(MarkAllAsRead)}")]
    public async Task<ApiResult> MarkAllAsRead()
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        await _fsql.Update<DMessage>()
            .Set(m => m.Status, MessageStatus.已读)
            .Where(m => m.DMemberId == userId && m.Status == MessageStatus.未读)
            .ExecuteAffrowsAsync();

        return ApiResult.Success.SetMessage("标记已读成功");
    }

    /// <summary>
    /// 发送消息
    /// </summary>
    [HttpPost($"@{nameof(SendMessage)}")]
    public async Task<ApiResult> SendMessage(string content)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }
        var member = await _fsql.Select<DMember>().Include(m => m.DAgent).Where(m => m.Id == userId.Value).ToOneAsync();
        var now = DateTime.Now;
        var windowStart = now.Subtract(MessageWindow);

        var hasHistory = await _fsql.Select<DMessage>()
            .Where(m => m.DMemberId == userId.Value && m.SentAt >= windowStart)
            .AnyAsync();

        if (!hasHistory)
        {
            var systemMessage = new DMessage()
            {
                DMemberId = userId.Value,
                Content = FirstConsultationAutoReply,
                SentAt = now,
                SenderRole = MessageSenderRole.System,
                Status = MessageStatus.已读,
                SenderIp = IpHelper.GetClientIpAddress(HttpContext),
            };

            await _fsql.Insert(systemMessage).ExecuteAffrowsAsync();
        }

        var message = new DMessage()
        {
            DMemberId = userId.Value,
            Content = content,
            SentAt = now,
            SenderRole = MessageSenderRole.Customer,
            Status = MessageStatus.未读,
            SenderIp = IpHelper.GetClientIpAddress(HttpContext),
        };

        await _fsql.Insert(message).ExecuteAffrowsAsync();

        var agent = member.DAgent;
        if (agent != null && !string.IsNullOrWhiteSpace(agent.TelegramChatId))
        {
            var ip = IpHelper.GetClientIpAddress(HttpContext);
            var html = $"""
<b>您有一条新的客户消息</b>
会员：<code>{TGMessageApi.EscapeHtml(member.Username)}</code>
内容：{TGMessageApi.EscapeHtml(content)}
时间：{now:yyyy-MM-dd HH:mm:ss}
IP：<code>{TGMessageApi.EscapeHtml(ip)}</code>
──────────────
<i>请直接「回复」本消息以回复该客户（将同步到 App 消息中心）。</i>
""";
            var configuredChatIds = agent.TelegramChatId
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(id => long.TryParse(id, out var chatId) ? chatId : 0)
                .Where(chatId => chatId != 0)
                .Distinct()
                .ToList();

            var sentList = new List<(long ChatId, int MessageId)>();
            foreach (var chatId in configuredChatIds)
            {
                var telegramMessageId = await _TGMessageApi.SendHtmlMessageAndGetMessageIdAsync(chatId, html);
                if (telegramMessageId.HasValue)
                {
                    sentList.Add((chatId, telegramMessageId.Value));
                }
            }

            if (configuredChatIds.Count > 1)
            {
                _logger.LogInformation(
                    $"会员 {userId.Value} 的代理配置了多个 Telegram 会话，已循环发送，配置数量={configuredChatIds.Count}，成功数量={sentList.Count}");
            }

            if (sentList.Count > 0)
            {
                var (chatId, tgMsgId) = sentList[0];
                await _fsql.Update<DMessage>()
                    .Set(m => m.TgChatId, chatId)
                    .Set(m => m.TgTelegramMessageId, tgMsgId)
                    .Where(m => m.Id == message.Id)
                    .ExecuteAffrowsAsync();
            }
        }
        else
        {
            _logger.LogInformation("会员 {MemberId} 的代理未配置 TelegramChatId，跳过 TG 通知", userId.Value);
        }

        return ApiResult.Success.SetMessage("消息发送成功");
    }

    /// <summary>
    /// 删除消息
    /// </summary>
    /// <param name="id">消息ID</param>
    [HttpPost($"@{nameof(DeleteMessage)}")]
    public async Task<ApiResult> DeleteMessage(long id)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        var affected = await _fsql.Delete<DMessage>().Where(m => m.Id == id && m.DMemberId == userId && m.SenderRole != MessageSenderRole.System)
            .ExecuteAffrowsAsync();

        if (affected == 0)
        {
            return ApiResult.Error.SetMessage("消息不存在或无权删除");
        }

        return ApiResult.Success.SetMessage("删除成功");
    }


}
