using FreeSql;
using System.Net;
using J9_Admin.Entities;
using J9_Admin.Utils;
using Microsoft.AspNetCore.Http;

namespace J9_Admin.Middlewares;

/// <summary>
/// 基于数据库 ip_whitelist 表的 IP 白名单中间件。
/// </summary>
public class IpWhitelistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpWhitelistMiddleware> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public IpWhitelistMiddleware(
        RequestDelegate next,
        ILogger<IpWhitelistMiddleware> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _next = next;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldSkipWhitelist(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var clientIp = NormalizeIp(IpHelper.GetClientIpAddress(context, _logger));

        if (string.IsNullOrWhiteSpace(clientIp) || clientIp == "unknown")
        {
            _logger.LogWarning("IP白名单校验失败：无法识别客户端IP，请求路径：{Path}", context.Request.Path);
            await RejectAsync(context, "IP address not detected.");
            return;
        }

        if (IsLoopback(clientIp))
        {
            await _next(context);
            return;
        }

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var fsql = scope.ServiceProvider.GetRequiredService<FreeSqlCloud>();

        var enabledEntries = await fsql.Select<IpWhitelist>()
            .Where(x => x.IsEnabled)
            .ToListAsync();

        if (enabledEntries.Count == 0)
        {
            _logger.LogWarning("IP白名单已启用，但数据库中没有启用的白名单记录，请求默认放行。路径：{Path}", context.Request.Path);
            await _next(context);
            return;
        }

        var matchedEntry = enabledEntries.FirstOrDefault(x => NormalizeIp(x.IpAddress) == clientIp);
        if (matchedEntry == null)
        {
            var clientIpv4 = ToIpv4Display(clientIp);
            _logger.LogWarning("IP白名单拦截：IP={ClientIp}，路径={Path}", clientIp, context.Request.Path);
            await RejectAsync(context, $"""
有个程序员去面试，面试官问：

“如果你不在白名单中，会看到什么？”

程序员想了想说：

“看不到未来，只能看到 403。”

面试官又问：

“那你有什么优点？”

程序员说：

“我最大的优点，就是就算被拒绝了，我也会把报错信息返回成 JSON，结构还特别标准。”

客户端IPv4：{clientIpv4}
""");
            return;
        }

        try
        {
            matchedEntry.LastAccessTime = DateTime.Now;
            matchedEntry.AccessCount += 1;
            await fsql.Update<IpWhitelist>()
                .SetSource(matchedEntry)
                .Where(x => x.Id == matchedEntry.Id)
                .ExecuteAffrowsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IP白名单命中后更新访问统计失败：IP={ClientIp}", clientIp);
        }

        await _next(context);
    }

    private static bool ShouldSkipWhitelist(PathString path)
    {
        return path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/profile", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoopback(string ip)
    {
        return ip == "127.0.0.1" || ip == "::1";
    }

    private static string NormalizeIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return string.Empty;
        }

        var trimmed = ip.Trim();
        if (!IPAddress.TryParse(trimmed, out var parsedIp))
        {
            return trimmed;
        }

        if (parsedIp.IsIPv4MappedToIPv6)
        {
            parsedIp = parsedIp.MapToIPv4();
        }

        return parsedIp.ToString();
    }

    private static string ToIpv4Display(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return "unknown";
        }

        var normalizedIp = NormalizeIp(ip);
        if (normalizedIp == "::1")
        {
            return "127.0.0.1";
        }

        return normalizedIp;
    }

    private static async Task RejectAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new
        {
            code = 403,
            message
        });
    }
}
