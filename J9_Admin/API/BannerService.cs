using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace J9_Admin.API;

/// <summary>
/// 轮播接口
/// </summary>
[ApiController]
[Route("api/banner")]
[Tags("轮播图系统")]
public class BannerService : BaseService
{
    public BannerService(FreeSqlCloud freeSqlCloud, FreeScheduler.Scheduler scheduler, ILogger<BannerService> logger, AdminContext adminContext, IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        : base(freeSqlCloud, scheduler, logger, adminContext, configuration, webHostEnvironment)
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
