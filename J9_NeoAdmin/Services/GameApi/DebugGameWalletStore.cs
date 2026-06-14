using System.Collections.Concurrent;

namespace J9_NeoAdmin.Services.GameApi;

/// <summary>
/// 开发环境下 MS/XH 游戏钱包的内存模拟，避免查余额固定返回值、下分不扣款导致重复回收入账。
/// </summary>
internal static class DebugGameWalletStore
{
    private static readonly ConcurrentDictionary<string, decimal> Balances = new(StringComparer.Ordinal);

    private static string BuildKey(string playerName, string apiCode)
    {
        return $"{playerName.Trim()}::{apiCode.Trim()}";
    }

    public static decimal GetBalance(string playerName, string apiCode)
    {
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(apiCode))
        {
            return 0;
        }

        return Balances.TryGetValue(BuildKey(playerName, apiCode), out var balance) ? balance : 0;
    }

    public static bool TryDeposit(string playerName, string apiCode, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(apiCode) || amount <= 0)
        {
            return false;
        }

        var key = BuildKey(playerName, apiCode);
        Balances.AddOrUpdate(key, amount, (_, current) => current + amount);
        return true;
    }

    public static bool TryWithdraw(string playerName, string apiCode, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(apiCode) || amount <= 0)
        {
            return false;
        }

        var key = BuildKey(playerName, apiCode);
        if (!Balances.TryGetValue(key, out var current) || current < amount)
        {
            return false;
        }

        var next = current - amount;
        if (next <= 0)
        {
            Balances.TryRemove(key, out _);
        }
        else
        {
            Balances[key] = next;
        }

        return true;
    }
}
