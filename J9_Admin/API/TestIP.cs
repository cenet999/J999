using FreeScheduler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using J9_Admin.Utils;

namespace J9_Admin.API;

/// <summary>
/// IP 调试接口
/// </summary>
[ApiController]
[Route("api/test-ip")]
[Tags("调试")]
public class TestIP : BaseService
{
    public TestIP(
        FreeSqlCloud freeSqlCloud,
        Scheduler scheduler,
        ILogger<TestIP> logger,
        AdminContext adminContext,
        IConfiguration configuration,
        IWebHostEnvironment webHostEnvironment)
        : base(freeSqlCloud, scheduler, logger, adminContext, configuration, webHostEnvironment)
    {
    }

    /// <summary>
    /// 查看当前请求拿到的 IP 信息
    /// </summary>
    [HttpGet($"@{nameof(Get)}")]
    [AllowAnonymous]
    public object Get()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var resolvedIp = IpHelper.GetClientIpAddress(HttpContext, _logger);

        return new
        {
            message = "用于查看当前请求实际拿到的 IP 信息",
            resolvedIp,
            remoteIp,
            headers = new
            {
                xForwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault(),
                xRealIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault(),
                cfConnectingIp = HttpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault(),
                xOriginalForwardedFor = HttpContext.Request.Headers["X-Original-Forwarded-For"].FirstOrDefault()
            },
            request = new
            {
                scheme = HttpContext.Request.Scheme,
                host = HttpContext.Request.Host.ToString(),
                path = HttpContext.Request.Path.ToString(),
                queryString = HttpContext.Request.QueryString.ToString(),
                userAgent = HttpContext.Request.Headers["User-Agent"].FirstOrDefault()
            }
        };
    }
}
