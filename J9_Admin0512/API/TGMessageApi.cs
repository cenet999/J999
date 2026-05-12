using J9_Admin.TelegramBot;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace J9_Admin.API
{
    /// <summary>
    /// TG 消息发送
    /// </summary>
    public class TGMessageApi
    {
        private readonly ILogger<TGMessageApi> _logger;
        private readonly IConfiguration _configuration;

        public TGMessageApi(ILogger<TGMessageApi> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// HTML 模式下转义用户输入，避免破坏标签或注入
        /// </summary>
        public static string EscapeHtml(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        /// <summary>
        /// 多会话发消息
        /// </summary>
        /// <param name="chatId">聊天ID，支持单个ID或逗号分隔的多个ID</param>
        /// <param name="message">消息内容</param>
        /// <returns>是否发送成功</returns>
        public async Task<bool> SendMessageAsync(string chatId, string message)
        {
            if (string.IsNullOrWhiteSpace(chatId))
                return false;

            // 将字符串按逗号分割，解析为长整型；非法片段跳过（与 MessageService 一致）
            var anyOk = false;
            foreach (var raw in chatId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!long.TryParse(raw, out var id) || id == 0)
                    continue;
                if (await SendMessageAsync(id, message))
                    anyOk = true;
            }

            return anyOk;
        }

        /// <summary>
        /// 单会话发消息
        /// </summary>
        /// <param name="chatId">聊天ID</param>
        /// <param name="message">消息内容</param>
        /// <returns>是否发送成功</returns>
        public async Task<bool> SendMessageAsync(long chatId, string message)
        {
            var id = await SendHtmlMessageAndGetMessageIdAsync(chatId, message);
            return id.HasValue;
        }

        /// <summary>
        /// 发送 HTML 文本并返回 Telegram 侧 MessageId（用于「回复该消息」关联）
        /// </summary>
        public async Task<int?> SendHtmlMessageAndGetMessageIdAsync(long chatId, string htmlMessage)
        {
            try
            {
                if (chatId == 0)
                    return null;

                var botToken = _configuration["TelegramBot:ApiKey"];
                if (string.IsNullOrEmpty(botToken))
                {
                    _logger.LogInformation("未配置 TelegramBot:ApiKey，跳过发送");
                    return null;
                }

                var botClient = new TelegramBotClient(botToken);
                var sent = await botClient.SendTextMessageAsync(chatId, htmlMessage, parseMode: ParseMode.Html);
                _logger.LogInformation("成功向聊天 {ChatId} 发送消息 htmlMessage={htmlMessage}", chatId, htmlMessage);
                return sent.MessageId;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "向聊天 {ChatId} 发送消息失败: {Message}", chatId, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 向逗号分隔的多个 ChatId 发送同一条 HTML，并返回每个会话的 (ChatId, MessageId)
        /// </summary>
        public async Task<List<(long ChatId, int MessageId)>> SendHtmlToManyChatsAsync(string? telegramChatIdsCsv, string htmlMessage)
        {
            var result = new List<(long ChatId, int MessageId)>();
            if (string.IsNullOrWhiteSpace(telegramChatIdsCsv))
                return result;

            foreach (var raw in telegramChatIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!long.TryParse(raw, out var chatId) || chatId == 0)
                    continue;
                var mid = await SendHtmlMessageAndGetMessageIdAsync(chatId, htmlMessage);
                if (mid.HasValue)
                    result.Add((chatId, mid.Value));
            }

            return result;
        }
    }
}