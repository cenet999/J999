using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace J9_Admin.API;

/// <summary>
/// 公告接口
/// </summary>
[ApiController]
[Route("api/notice")]
[Tags("公告系统")]
public class NoticeService : BaseService
{
    public NoticeService(FreeSqlCloud freeSqlCloud, FreeScheduler.Scheduler scheduler, ILogger<NoticeService> logger, AdminContext adminContext, IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        : base(freeSqlCloud, scheduler, logger, adminContext, configuration, webHostEnvironment)
    {
    }

    /// <summary>
    /// 获取公告列表
    /// </summary>
    [HttpGet($"@{nameof(GetNotices)}")]
    [AllowAnonymous]
    public async Task<ApiResult> GetNotices()
    {
        var notices = await _fsql.Select<DNotice>()
            .Where(x => x.IsEnabled)
            .OrderByDescending(x => x.Sort)
            .OrderByDescending(x => x.CreatedTime)
            .ToListAsync(x => new
            {
                x.Id,
                x.Title,
                x.Content,
                x.CreatedTime,
                x.ModifiedTime
            });

        return ApiResult.Success.SetData(notices);
    }
}
