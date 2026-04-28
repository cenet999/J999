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
                    // 欢迎消息
                    var msg = "Welcome to the 800800win Telegram bot!\n";
                    msg += "Please use the correct format:\n <code>/bind AgentId</code>\n";
                    msg += "After binding, you can get the information of your website (member registration, recharge, withdrawal, betting, rebate, etc.).\n";
                    msg += "https://api.800800.win/Ddd/DAgent to get agentId \n";
                    msg += "You can also use /help to get help.\n";
                    await DeviceHelper.SendTempMessageAsync(device, msg);

                    //翻译为中文再发送一遍
                    var msg_cn = "欢迎使用800800win Telegram机器人！\n\n";
                    msg_cn += "请使用正确的格式：\n <code>/bind AgentId</code>\n";
                    msg_cn += "绑定后，您可以获取到您网站的相关信息（会员注册、充值、提现、下注、返点等）。\n";
                    msg_cn += "https://api.800800.win/Ddd/DAgent 获取agentId \n";
                    msg_cn += "您也可以使用 /help 获取帮助。\n";
                    await DeviceHelper.SendTempMessageAsync(device, msg_cn);

                    return true;
                }

                if (messageText.StartsWith("/help"))
                {
                    _logger.LogInformation($"命中指令 /help: ChatId={chatId}, MessageId={messageId}");
                    // 英文和中文内容对比后，发现英文版有些地方可以更详细、与中文版保持一致，尤其是命令的解释和管理员联系方式的表达方式。
                    // 下面对中英文内容做了优化，使其表达更一致，且注释详细说明。

                    // 英文帮助信息
                    var msg = "Welcome to the 800800win Telegram bot!\n";
                    msg += "--------------------------------\n";
                    msg += "Examples:\n";
                    msg += "<code>/bind 1234567890</code> - Bind your agent ID\n";
                    msg += "<code>/unbind 1234567890</code> - Unbind your agent ID\n";
                    msg += "<code>/id</code> - Get your agent information\n";
                    msg += "<code>/ip 127.0.0.1</code> - Set IP whitelist\n";
                    msg += "--------------------------------\n";
                    msg += "If you have any questions, please contact the administrator: @yoyoyo241026\n";

                    await DeviceHelper.SendTempMessageAsync(device, msg);

                    // 中文帮助信息
                    var msg_cn = "欢迎使用800800win Telegram机器人！\n";
                    msg_cn += "--------------------------------\n";
                    msg_cn += "示例：\n";
                    msg_cn += "<code>/bind 1234567890</code> - 绑定代理ID\n";
                    msg_cn += "<code>/unbind 1234567890</code> - 解绑代理ID\n";
                    msg_cn += "<code>/id</code> - 获取代理信息\n";
                    msg_cn += "<code>/ip 127.0.0.1</code> - 设置IP白名单\n";
                    msg_cn += "--------------------------------\n";
                    msg_cn += "如果您有任何问题，请联系管理员：@yoyoyo241026\n";

                    await DeviceHelper.SendTempMessageAsync(device, msg_cn);

                    // 说明：
                    // 1. 英文和中文命令说明都加上了“-”和简短解释，便于新手理解。
                    // 2. 管理员联系方式格式统一，去掉了中文多余的句号。
                    // 3. 去掉了重复的 /id 命令，保持中英文一致。
                    // 4. 注释说明了优化点，便于维护。

                    return true;
                }

                // 处理绑定指令
                if (messageText.StartsWith("/bind"))
                {
                    _logger.LogInformation($"命中指令 /bind: ChatId={chatId}, MessageId={messageId}, Text={messageText}");
                    // 获取绑定参数
                    var bindParams = messageText.Split(' ');
                    if (bindParams.Length < 2)
                    {
                        await DeviceHelper.SendTempMessageAsync(device, "请使用正确的格式: /bind AgentId");
                        return true;
                    }

                    var agentId = long.Parse(bindParams[1]);
                    var agent = await _fsql.Select<DAgent>().Where(a => a.Id == agentId).ToOneAsync();

                    if (agent == null)
                    {
                        await DeviceHelper.SendTempMessageAsync(device, $"该代理不存在 {agentId}");
                        return true;
                    }

                    if (agent.TelegramChatId.Contains(message.Chat.Id.ToString()))
                    {
                        await DeviceHelper.SendTempMessageAsync(device, $"该代理已绑定您的Telegram账号 {agent.HomeUrl}");
                        return true;
                    }

                    agent.TelegramChatId = agent.TelegramChatId == "" ? message.Chat.Id.ToString() : agent.TelegramChatId + "," + message.Chat.Id.ToString();
                    await _fsql.Update<DAgent>().SetSource(agent).ExecuteAffrowsAsync();

                    await DeviceHelper.SendTempMessageAsync(device, $"成功绑定代理 {agent.HomeUrl}");
                    return true;

                }

                if (messageText.StartsWith("/unbind"))
                {
                    _logger.LogInformation($"命中指令 /unbind: ChatId={chatId}, MessageId={messageId}, Text={messageText}");
                    var agentId = long.Parse(messageText.Split(' ')[1]);
                    var agent = await _fsql.Select<DAgent>().Where(a => a.Id == agentId && a.TelegramChatId.Contains(message.Chat.Id.ToString())).ToOneAsync();
                    if (agent != null)
                    {
                        agent.TelegramChatId = agent.TelegramChatId.Replace(message.Chat.Id.ToString(), "");
                        await _fsql.Update<DAgent>().SetSource(agent).ExecuteAffrowsAsync();
                        await DeviceHelper.SendTempMessageAsync(device, $"成功解绑代理 {agent.HomeUrl}");
                        return true;
                    }
                    else
                    {
                        await DeviceHelper.SendTempMessageAsync(device, "该代理未绑定您的Telegram账号");
                        return true;
                    }
                }

                // 设置ip 白名单
                if (messageText.StartsWith("/ip"))
                {
                    _logger.LogInformation($"命中指令 /ip: ChatId={chatId}, MessageId={messageId}, Text={messageText}");
                    var agent = await _fsql.Select<DAgent>().Where(a => a.TelegramChatId.Contains(message.Chat.Id.ToString())).ToOneAsync();

                    if (agent == null)
                    {
                        await DeviceHelper.SendTempMessageAsync(device, "您的账号未绑定代理。");
                        return true;
                    }

                    agent.IPWhiteList = messageText.Split(' ')[1];
                    await _fsql.Update<DAgent>().SetSource(agent).ExecuteAffrowsAsync();
                    await DeviceHelper.SendTempMessageAsync(device, $"成功设置IP白名单 ({agent.IPWhiteList}) 为 {agent.HomeUrl}");
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
                        msg += $"• Game Points: {agent.GamePoints}\n";
                        msg += $"• USDT Address: <code>{agent.UsdtAddress}</code>\n";
                        msg += $"• Credit Discount: {agent.CreditDiscount}\n";
                        msg += $"• Agent Domain: {agent.HomeUrl}\n";
                        msg += $"• Server IP: {agent.ServerIP}\n";
                        msg += $"• IP Whitelist: {agent.IPWhiteList}\n";
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

            var member = await _fsql.Select<DMember>()
                .Include(m => m.DAgent)
                .Where(m => m.Id == original.DMemberId.Value)
                .ToOneAsync();

            if (member?.DAgent == null || !HasTelegramChatBinding(member.DAgent.TelegramChatId, chatId))
            {
                _logger.LogWarning(
                    $"Telegram 绑定校验失败，消息不写入 App: ChatId={chatId}, ReplyToMessageId={repliedToMessageId}, MemberId={original.DMemberId}, AgentId={member?.DAgent?.Id}");
                await DeviceHelper.SendTempMessageAsync(device, "⚠️ 该客户当前已不在此 Telegram 绑定下，未同步到 App。");
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

        private static bool HasTelegramChatBinding(string? telegramChatIdsCsv, long chatId)
        {
            if (string.IsNullOrWhiteSpace(telegramChatIdsCsv))
                return false;

            var targetChatId = chatId.ToString();
            return telegramChatIdsCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(id => string.Equals(id, targetChatId, StringComparison.Ordinal));
        }
    }
}
