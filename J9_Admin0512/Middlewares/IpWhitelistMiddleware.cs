using FreeSql;
using System.Net;
using System.Net.Mime;
using System.Text.Encodings.Web;
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
            await RejectAsync(context, clientIpv4);
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

    private static async Task RejectAsync(HttpContext context, string clientIpv4)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = MediaTypeNames.Text.Html + "; charset=utf-8";

        var encodedIp = HtmlEncoder.Default.Encode(clientIpv4);
        await context.Response.WriteAsync($$"""
<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>403 Forbidden</title>
  <style>
    :root {
      color-scheme: light;
      --bg: #f7f8fb;
      --panel: #ffffff;
      --text: #1f2937;
      --muted: #6b7280;
      --line: #e5e7eb;
      --accent: #2563eb;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 32px 16px;
      background: var(--bg);
      color: var(--text);
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
      line-height: 1.7;
    }
    main {
      width: min(680px, 100%);
      padding: 36px;
      border: 1px solid var(--line);
      border-radius: 12px;
      background: var(--panel);
      box-shadow: 0 18px 50px rgba(15, 23, 42, 0.08);
    }
    .code {
      margin: 0 0 18px;
      color: var(--accent);
      font-size: 15px;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }
    h1 {
      margin: 0 0 20px;
      font-size: clamp(26px, 4vw, 38px);
      line-height: 1.2;
    }
    p {
      margin: 0 0 14px;
      font-size: 17px;
    }
    .note {
      margin-top: 26px;
      padding-top: 18px;
      border-top: 1px solid var(--line);
      color: var(--muted);
      font-size: 14px;
    }
  </style>
</head>
<body>
  <main>
    <p class="code">403 Forbidden</p>
    <h1>你访问的页面暂时进不去</h1>
    <p>但有些孩子，回家的路也还没有找到。</p>
    <p>如果你看见过疑似走失儿童，请第一时间联系警方或当地救助机构。</p>
    <p class="note">当前客户端 IP：{{encodedIp}}</p>
  </main>
</body>
</html>
""");
    }
}
