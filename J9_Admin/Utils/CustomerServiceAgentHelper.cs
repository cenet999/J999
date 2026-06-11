/// <summary>
/// 平台客服代理：固定为 Id 最小的 DAgent，不沿会员归属代理或上级链查找。
/// </summary>
public static class CustomerServiceAgentHelper
{
    /// <summary>
    /// 获取平台客服代理（Id 最小）。
    /// </summary>
    public static Task<DAgent?> GetCustomerServiceAgentAsync(IFreeSql fsql) =>
        fsql.Select<DAgent>().OrderBy(a => a.Id).ToOneAsync();

    /// <summary>
    /// 判断 Telegram ChatId 是否已绑定到指定代理。
    /// </summary>
    public static bool HasTelegramChatBinding(string? telegramChatIdsCsv, long chatId)
    {
        if (string.IsNullOrWhiteSpace(telegramChatIdsCsv))
            return false;

        var targetChatId = chatId.ToString();
        return telegramChatIdsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(id => string.Equals(id, targetChatId, StringComparison.Ordinal));
    }
}
