using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace J9_NeoAdmin.API;

/// <summary>
/// 轮播接口
/// </summary>
[ApiController]
[Route("api/banner")]
[Tags("轮播图系统")]
public class BannerService : BaseService
{
    public BannerService(IFreeSql freeSql, FreeScheduler.Scheduler scheduler, ILogger<BannerService> logger, IConfiguration configuration, IWebHostEnvironment webHostEnvironment, NeoAdmin.Blazor.Core.Identity.NeoAdminAuthService authService)
        : base(freeSql, scheduler, logger, configuration, webHostEnvironment, authService)
    {
    }

    /// <summary>
    /// 获取轮播图
    /// </summary>
    [HttpGet($"@{nameof(GetBanners)}")]
    [AllowAnonymous]
    public async Task<ApiResult> GetBanners()
    {
        var banners = await _fsql.Select<DBanner>()
            .Where(x => x.IsEnabled)
            .OrderByDescending(x => x.Sort)
            .OrderByDescending(x => x.CreatedTime)
            .ToListAsync();

        return ApiResult.Success.SetData(banners);
    }
}
