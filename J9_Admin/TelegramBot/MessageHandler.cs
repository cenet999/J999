using TelegramBotBase.Args;
using TelegramBotBase.Interfaces;
using Telegram.Bot.Types.Enums;
using FreeSql;
using TelegramBotBase.Base;
using TelegramBotBase.Form;
using Microsoft.VisualBasic;
using System.Text.RegularExpressions;
using System.Data;
using System.Text.Json;
using J9_Admin.API;
using J9_Admin.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace J9_Admin.TelegramBot
{
    /// <summary>
    /// 消息处理服务 - 统一处理各种类型的消息
    /// </summary>
    public class MessageHandler
    {
        private readonly ILogger<MessageHandler> _logger;
        private readonly IConfiguration _configuration;
        private readonly FreeSqlCloud _fsql;

        /// <summary>
        /// 构造函数 - 通过依赖注入初始化服务
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="configuration">配置服务</param>
        /// <param name="fsql">FreeSql数据库实例</param>
        public MessageHandler(ILogger<MessageHandler> logger, IConfiguration configuration, FreeSqlCloud fsql)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _fsql = fsql ?? throw new ArgumentNullException(nameof(fsql));
        }

        /// <summary>
        /// 处理普通消息
        /// </summary>
        /// <param name="device">Telegram设备会话</param>
        /// <param name="messageResult">消息结果对象</param>
        /// <returns>处理结果</returns>
        public async Task<bool> HandleMessageAsync(IDeviceSession device, MessageResult messageResult)
        {
            try
            {
                var message = messageResult.Message;
                var messageText = message.Text ?? "";
                var chatId = message.Chat.Id;
                var messageId = message.MessageId;

                _logger.LogInformation(
                    $"收到 Telegram 消息: ChatId={chatId}, MessageId={messageId}, HasReplyTo={message.ReplyToMessage != null}, Text={messageText}");

                // 判断是否在私聊中
                if (chatId.ToString().StartsWith("-"))
                {
                    // await DeviceHelper.SendTempMessageAsync(device, "Please use in private chat");
                    _logger.LogInformation($"忽略群聊消息: ChatId={chatId}, MessageId={messageId}");
                    return false;
                }

                // 客服「回复」本 Bot 发出的客户消息通知时，将回复写入站内对应会员的会话
                if (message.ReplyToMessage != null &&
                    await TryInsertAgentReplyFromTelegramAsync(device, message))
                {
                    _logger.LogInformation($"消息按客服回复流程处理完成: ChatId={chatId}, MessageId={messageId}");
                    return true;
                }

                if (messageText.StartsWith("/start"))
                {
                    _logger.LogInformation($"命中指令 /start: ChatId={chatId}, MessageId={messageId}");
                    var msg = "Welcome to the 800800win Telegram bot!\n";
                    msg += "Use /help to see available commands.\n";
                    msg += "Telegram ChatId must be configured in the admin panel.\n";
                    await DeviceHelper.SendTempMessageAsync(device, msg);

                    var msg_cn = "欢迎使用800800win Telegram机器人！\n\n";
                    msg_cn += "发送 /help 查看可用命令。\n";
                    msg_cn += "Telegram ChatId 请在管理后台代理页面手动配置。\n";
                    await DeviceHelper.SendTempMessageAsync(device, msg_cn);

                    return true;
                }

                if (messageText.StartsWith("/help"))
                {
                    _logger.LogInformation($"命中指令 /help: ChatId={chatId}, MessageId={messageId}");

                    var msg = "Welcome to the 800800win Telegram bot!\n";
                    msg += "--------------------------------\n";
                    msg += "Examples:\n";
                    msg += "<code>/id</code> - Get your agent information\n";
                    msg += "<code>/ip 127.0.0.1</code> - Set login IP\n";
                    msg += "--------------------------------\n";
                    msg += "ChatId must be configured in admin panel first.\n";
                    msg += "If you have any questions, please contact the administrator: @yoyoyo241026\n";

                    await DeviceHelper.SendTempMessageAsync(device, msg);

                    var msg_cn = "欢迎使用800800win Telegram机器人！\n";
                    msg_cn += "--------------------------------\n";
                    msg_cn += "示例：\n";
                    msg_cn += "<code>/id</code> - 获取代理信息\n";
                    msg_cn += "<code>/ip 127.0.0.1</code> - 设置登录IP\n";
                    msg_cn += "--------------------------------\n";
                    msg_cn += "请先在管理后台配置 Telegram ChatId。\n";
                    msg_cn += "如果您有任何问题，请联系管理员：@yoyoyo241026\n";

                    await DeviceHelper.SendTempMessageAsync(device, msg_cn);

                    return true;
                }

                // 设置ip 白名单
                if (messageText.StartsWith("/ip"))
                {
                    _logger.LogInformation($"命中指令 /ip: ChatId={chatId}, MessageId={messageId}, Text={messageText}");
                    var agent = await _fsql.Select<DAgent>().Where(a => a.TelegramChatId.Contains(message.Chat.Id.ToString())).ToOneAsync();

                    if (agent == null)
                    {
                        await DeviceHelper.SendTempMessageAsync(device, "未找到与您 ChatId 匹配的代理，请在管理后台配置 Telegram ChatId。");
                        return true;
                    }

                    agent.LoginIp = messageText.Split(' ')[1];
                    await _fsql.Update<DAgent>().SetSource(agent).ExecuteAffrowsAsync();
                    await DeviceHelper.SendTempMessageAsync(device, $"成功设置登录IP ({agent.LoginIp}) 为 {agent.HomeUrl}");
                    return true;

                }


                // 查询自己的id
                if (messageText.StartsWith("/id") || messageText.StartsWith("/info") || messageText.StartsWith("/me"))
                {
                    _logger.LogInformation($"命中指令 /id|/info|/me: ChatId={chatId}, MessageId={messageId}, Text={messageText}");
                    var agent = await _fsql.Select<DAgent>().Where(a => a.TelegramChatId.Contains(message.Chat.Id.ToString())).ToOneAsync();

                    // 构建用户信息提示消息
                    var msg = "🔧 User Information\n\n";
                    msg += $"Username: {message.From.Username}\n";
                    msg += $"Name: {message.From.FirstName} {message.From.LastName}\n";
                    msg += $"Current Chat ID: {message.Chat.Id}\n\n";

                    if (agent != null)
                    {
                        // 详细输出代理信息，字段参考DAgent.cs
                        msg += $"--------------------------------\n";
                        msg += $"• Agent ID: {agent.Id}\n";
                        msg += $"• Agent Type: {agent.AgentType}\n";
                        msg += $"• Status: {(agent.IsEnabled ? "Enabled" : "Disabled")}\n";
                        msg += $"• USDT Address: <code>{_configuration["Payment:UsdtAddress"]}</code>\n";
                        msg += $"• Agent Domain: {agent.HomeUrl}\n";
                        msg += $"• Server IP: {agent.ServerIP}\n";
                        msg += $"• Login IP: {agent.LoginIp}\n";
                        msg += $"• Rebate Rate: {agent.RebateRate}\n";
                        msg += $"• Remark: {agent.Remark}\n";
                        msg += $"--------------------------------\n";
                    }

                    await DeviceHelper.SendTempMessageAsync(device, msg);
                    return true;
                }

                _logger.LogInformation($"消息未命中任何处理分支: ChatId={chatId}, MessageId={messageId}, Text={messageText}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"处理消息失败: ChatId={messageResult?.Message?.Chat?.Id}, MessageId={messageResult?.Message?.MessageId}, Error={ex.Message}");
                await DeviceHelper.SendTempMessageAsync(device, "处理消息失败，请稍后再试");
                return false;
            }
        }

        /// <summary>
        /// 若当前消息是对本 Bot 推送的客户通知的回复，则插入一条 Agent 角色的 <see cref="DMessage"/> 并标记原客户消息为已回复
        /// </summary>
        private async Task<bool> TryInsertAgentReplyFromTelegramAsync(IDeviceSession device, Telegram.Bot.Types.Message message)
        {
            var replyText = message.Text ?? message.Caption;
            var chatId = message.Chat.Id;
            var repliedToMessageId = message.ReplyToMessage?.MessageId;

            _logger.LogInformation(
                $"开始处理 Telegram 回复同步: ChatId={chatId}, ReplyToMessageId={repliedToMessageId}, SenderUserId={message.From?.Id}");

            if (string.IsNullOrWhiteSpace(replyText))
            {
                _logger.LogInformation($"忽略空回复内容: ChatId={chatId}, ReplyToMessageId={repliedToMessageId}");
                return false;
            }

            var trimmed = replyText.Trim();
            if (trimmed.StartsWith('/'))
            {
                _logger.LogInformation($"忽略命令型回复内容: ChatId={chatId}, ReplyToMessageId={repliedToMessageId}, Content={trimmed}");
                return false;
            }

            if (repliedToMessageId == null)
            {
                _logger.LogWarning($"回复消息缺少 ReplyToMessageId，无法同步: ChatId={chatId}");
                return false;
            }

            var original = await _fsql.Select<DMessage>()
                .Where(m => m.TgChatId == chatId && m.TgTelegramMessageId == repliedToMessageId.Value && m.SenderRole == MessageSenderRole.Customer)
                .ToOneAsync();

            if (original == null || original.DMemberId == null)
            {
                _logger.LogInformation(
                    $"未找到可同步的原始客户消息: ChatId={chatId}, ReplyToMessageId={repliedToMessageId}, HasOriginal={original != null}");
                return false;
            }

            var serviceAgent = await CustomerServiceAgentHelper.GetCustomerServiceAgentAsync(_fsql);

            if (serviceAgent == null || !CustomerServiceAgentHelper.HasTelegramChatBinding(serviceAgent.TelegramChatId, chatId))
            {
                _logger.LogWarning(
                    $"Telegram 绑定校验失败，消息不写入 App: ChatId={chatId}, ReplyToMessageId={repliedToMessageId}, MemberId={original.DMemberId}, ServiceAgentId={serviceAgent?.Id}");
                await DeviceHelper.SendTempMessageAsync(device, "⚠️ 当前 Telegram 未绑定平台客服代理，未同步到 App。");
                return true;
            }

            var reply = new DMessage
            {
                DMemberId = original.DMemberId,
                Content = replyText.Trim(),
                SentAt = DateTime.Now,
                SenderRole = MessageSenderRole.Agent,
                Status = MessageStatus.未读,
                SenderIp = "Telegram",
            };

            await _fsql.Insert(reply).ExecuteAffrowsAsync();

            await _fsql.Update<DMessage>()
                .Set(m => m.Status, MessageStatus.已回复)
                .Where(m => m.Id == original.Id)
                .ExecuteAffrowsAsync();

            _logger.LogInformation(
                $"Telegram 回复同步成功: ChatId={chatId}, ReplyToMessageId={repliedToMessageId}, OriginalMessageId={original.Id}, NewReplyMessageId={reply.Id}, MemberId={original.DMemberId}");

            await DeviceHelper.SendTempMessageAsync(device, "✅ 已同步到 App 消息中心（客户可见）。");
            return true;
        }

        /// <summary>
        /// 站点真正进入已启动状态后，可选向配置的 Telegram 会话发送成功提示；失败不阻断启动。
        /// </summary>
        public static void RegisterWebsiteInitializedTelegramNotification(WebApplication app, string environmentName)
        {
            app.Lifetime.ApplicationStarted.Register(() =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var chatIds = app.Configuration["TelegramBot:StartupNotifyChatIds"];
                        if (string.IsNullOrWhiteSpace(chatIds))
                            return;

                        using var scope = app.Services.CreateScope();
                        var tg = scope.ServiceProvider.GetRequiredService<TGMessageApi>();
                        var apiDomain = app.Configuration["APIDomain"] ?? "";
                        var machine = Environment.MachineName;
                        var html =
                            $"<b>J9 管理后台</b> 网站初始化成功\n\n" +
                            $"环境：<code>{TGMessageApi.EscapeHtml(environmentName)}</code>\n" +
                            $"机器：<code>{TGMessageApi.EscapeHtml(machine)}</code>\n" +
                            $"时间：<code>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</code>\n" +
                            $"API：<code>{TGMessageApi.EscapeHtml(apiDomain)}</code>";

                        var ok = await tg.SendMessageAsync(chatIds, html);
                        if (ok)
                            Log.Information("已向 Telegram 发送网站初始化成功通知");
                        else
                            Log.Information("网站初始化成功通知未送达（检查 TelegramBot:ApiKey 与 StartupNotifyChatIds）");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "发送网站初始化成功 Telegram 通知时出错");
                    }
                });
            });
        }
    }
}
