using Microsoft.AspNetCore.Mvc;
using J9_Admin.Entities;
using J9_Admin.Utils;

namespace J9_Admin.API;

/// <summary>
/// 活动中心
/// </summary>
[ApiController]
[Route("api/event")]
[Tags("活动中心系统")]
public class EventService : BaseService
{
    /// <summary>
    /// 宝箱档位倍率
    /// </summary>
    private const double ActivityChestStepRatio = 1.5;

    /// <summary>
    /// 宝箱目标积分表
    /// </summary>
    private static readonly IReadOnlyDictionary<int, decimal> ActivityChestRewardByTarget = BuildActivityChestRewardByTarget();

    /// <summary>
    /// 仅 App 可领取的积分宝箱档位
    /// </summary>
    private static readonly HashSet<int> AppOnlyActivityChestTargets = new() { 45, 68, 102, 153 };

    private static Dictionary<int, decimal> BuildActivityChestRewardByTarget()
    {
        var rewards = new[] { 2m, 5m, 12m, 28m, 62m, 135m };
        var map = new Dictionary<int, decimal>();
        var target = 20;
        foreach (var reward in rewards)
        {
            map[target] = reward;
            target = (int)Math.Round(target * ActivityChestStepRatio);
        }

        return map;
    }

    public EventService(FreeSqlCloud freeSqlCloud, FreeScheduler.Scheduler scheduler, ILogger<EventService> logger, AdminContext adminContext, IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        : base(freeSqlCloud, scheduler, logger, adminContext, configuration, webHostEnvironment)
    {
    }

    private bool IsWebRequest()
    {
        var clientPlatform = HttpContext.Request.Headers["X-Client-Platform"].FirstOrDefault()?.Trim().ToLowerInvariant();
        if (clientPlatform == "web")
        {
            return true;
        }

        var origin = HttpContext.Request.Headers["Origin"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        var referer = HttpContext.Request.Headers["Referer"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(referer))
        {
            return true;
        }

        var userAgent = HttpContext.Request.Headers["User-Agent"].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(userAgent)
            && userAgent.Contains("Mozilla/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取签到状态
    /// </summary>
    [HttpGet($"@{nameof(GetCheckInStatus)}")]
    public async Task<ApiResult> GetCheckInStatus()
    {
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        var member = await _fsql.Select<DMember>().Where(m => m.Id == currentUserId).ToOneAsync();
        if (member == null)
        {
            return ApiResult.Error.SetMessage("会员未找到");
        }

        bool isTodayChecked = member.LastCheckInDate.HasValue && member.LastCheckInDate.Value.Date == DateTime.Now.Date;
        int streak = member.ContinuousCheckInDays;
        
        // 如果昨天和今天都没签到，连续天数在展示时应该视为 0 吗？
        // 实际上后端的 PlayerCheckIn 处理了这个逻辑（只在签到时判断断签并重置）。
        // 为UI展示准确，如果今天的前一天也没签到，并且今天没签到，通常展示的连续签到会断掉。
        if (!isTodayChecked && member.LastCheckInDate.HasValue && member.LastCheckInDate.Value.Date < DateTime.Now.Date.AddDays(-1))
        {
            streak = 0;
        }

        // 以今天为中心，生成前后各3天共7天的窗口
        var today = DateTime.Now.Date;
        string[] weekNames = { "日", "一", "二", "三", "四", "五", "六" };

        DateTime? endDate = member.LastCheckInDate?.Date;
        DateTime? startDate = endDate?.AddDays(-streak + 1);

        int[] rewardValues = { 20, 20, 20, 20, 20, 20, 20 };

        var checkInDays = new List<object>();
        for (int offset = -3; offset <= 3; offset++)
        {
            DateTime targetDate = today.AddDays(offset);
            int idx = offset + 3; // 0~6
            int reward = rewardValues[idx];
            string rewardStr = idx == 6 ? "大奖" : $"+{reward}";

            bool isToday = (offset == 0);
            bool checkedIn = false;

            if (endDate.HasValue && startDate.HasValue)
            {
                if (targetDate >= startDate.Value && targetDate <= endDate.Value)
                {
                    checkedIn = true;
                }
            }

            string dayOfWeek = weekNames[(int)targetDate.DayOfWeek];

            checkInDays.Add(new {
                day = dayOfWeek,
                date = targetDate.ToString("MM/dd"),
                reward = rewardStr,
                @checked = checkedIn,
                isToday = isToday
            });
        }

        return ApiResult.Success.SetData(new {
            activityPoint = member.ActivityPoint,
            continuousDays = streak,
            isTodayChecked = isTodayChecked,
            today = today.ToString("yyyy-MM-dd"),
            todayWeek = weekNames[(int)today.DayOfWeek],
            checkInDays = checkInDays
        });
    }

    /// <summary>
    /// 获取月签到记录
    /// </summary>
    [HttpGet($"@{nameof(GetMonthlyCheckIn)}")]
    public async Task<ApiResult> GetMonthlyCheckIn(int? year, int? month)
    {
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        var member = await _fsql.Select<DMember>().Where(m => m.Id == currentUserId).ToOneAsync();
        if (member == null)
        {
            return ApiResult.Error.SetMessage("会员未找到");
        }

        var targetYear = year ?? DateTime.Now.Year;
        var targetMonth = month ?? DateTime.Now.Month;
        var startOfMonth = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Local);
        var startNextMonth = startOfMonth.AddMonths(1);
        var monthStartUnix = TimeHelper.LocalToUnix(startOfMonth);
        var nextMonthUnix = TimeHelper.LocalToUnix(startNextMonth);

        // 从交易记录中查询该月所有签到记录
        var checkInRows = await _fsql.Select<DTransAction>()
            .Where(t => t.DMemberId == currentUserId
                && t.TransactionType == TransactionType.CheckIn
                && t.Status == TransactionStatus.Success
                && t.TransactionTime >= monthStartUnix
                && t.TransactionTime < nextMonthUnix)
            .ToListAsync(t => new
            {
                t.TransactionTime,
                t.ActualAmount
            });

        var checkedDates = checkInRows
            .Select(r => TimeHelper.UnixToLocal(r.TransactionTime).ToString("yyyy-MM-dd"))
            .Distinct()
            .ToList();

        bool isTodayChecked = member.LastCheckInDate.HasValue && member.LastCheckInDate.Value.Date == DateTime.Now.Date;
        int streak = member.ContinuousCheckInDays;
        if (!isTodayChecked && member.LastCheckInDate.HasValue && member.LastCheckInDate.Value.Date < DateTime.Now.Date.AddDays(-1))
        {
            streak = 0;
        }

        return ApiResult.Success.SetData(new
        {
            year = targetYear,
            month = targetMonth,
            checkedDates = checkedDates,
            totalCheckedDays = checkedDates.Count,
            continuousDays = streak,
            isTodayChecked = isTodayChecked,
            activityPoint = member.ActivityPoint,
        });
    }

    /// <summary>
    /// 获取月任务活跃度
    /// </summary>
    [HttpGet($"@{nameof(GetMonthlyTaskActivity)}")]
    public async Task<ApiResult> GetMonthlyTaskActivity(int? year, int? month)
    {
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        var targetYear = year ?? DateTime.Now.Year;
        var targetMonth = month ?? DateTime.Now.Month;
        var startOfMonth = new DateTime(targetYear, targetMonth, 1);
        var endExclusive = startOfMonth.AddMonths(1);

        var memberTasks = await _fsql.Select<DMemberTask>()
            .Include(mt => mt.DTask)
            .Where(mt => mt.DMemberId == currentUserId && mt.TaskDate >= startOfMonth && mt.TaskDate < endExclusive)
            .ToListAsync();

        var dayTotals = new Dictionary<string, int>();
        foreach (var mt in memberTasks)
        {
            if (mt.DTask == null || !mt.DTask.IsEnabled)
            {
                continue;
            }

            var status = mt.Status;
            if (mt.CurrentValue >= mt.DTask.TargetValue && status == 0)
            {
                status = 1;
            }

            if (status != 1 && status != 2)
            {
                continue;
            }

            var key = mt.TaskDate.Date.ToString("yyyy-MM-dd");
            dayTotals[key] = dayTotals.GetValueOrDefault(key) + mt.DTask.ActivityPoint;
        }

        var days = dayTotals
            .OrderBy(kv => kv.Key)
            .Select(kv => new { date = kv.Key, taskActivityPoint = kv.Value })
            .ToList();

        return ApiResult.Success.SetData(new
        {
            year = targetYear,
            month = targetMonth,
            days,
            monthTotal = dayTotals.Values.Sum(),
            activeDays = dayTotals.Count,
        });
    }

    /// <summary>
    /// 获取限时活动
    /// </summary>
    [HttpGet($"@{nameof(GetTimeLimitedEvents)}")]
    public async Task<ApiResult> GetTimeLimitedEvents()
    {
        var events = await _fsql.Select<DEvent>()
            .Where(x => x.IsEnabled && x.StartTime <= DateTime.Now && x.EndTime >= DateTime.Now)
            .OrderByDescending(x => x.Sort)
            .ToListAsync();

        IEnumerable<object> result = events.Select(e => {
            var ts = e.EndTime - DateTime.Now;
            string timeLeft = ts.Days > 0 ? $"{ts.Days}天{ts.Hours}小时" : $"{ts.Hours}小时{ts.Minutes}分钟";
            int total = e.Count > 0 ? e.Count : 10;
            // 暂无用户进度表，暂时返回 0；后续可接入 DMemberEventProgress 等
            int progress = 0;
            return (object)new {
                name = e.Title,
                desc = e.Summary,
                image = e.BannerUrl,
                progress = progress,
                total = total,
                timeLeft = timeLeft,
            };
        });

        return ApiResult.Success.SetData(result);
    }

    /// <summary>
    /// 获取每日任务
    /// </summary>
    [HttpGet($"@{nameof(GetDailyTasks)}")]
    public async Task<ApiResult> GetDailyTasks()
    {
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        var today = DateTime.Now.Date;

        // 获取所有启用的任务
        var allTasks = await _fsql.Select<DTask>()
            .Where(t => t.IsEnabled)
            .OrderBy(t => t.Sort)
            .ToListAsync();

        // 获取该用户的今日进度
        var memberTasks = await _fsql.Select<DMemberTask>()
            .Where(mt => mt.DMemberId == currentUserId && mt.TaskDate == today)
            .ToListAsync();

        var resultTasks = new List<object>();
        int totalActivityPoint = 0;

        foreach (var task in allTasks)
        {
            var memberTask = memberTasks.FirstOrDefault(mt => mt.DTaskId == task.Id);
            int currentValue = memberTask?.CurrentValue ?? 0;
            int status = memberTask?.Status ?? 0; // 0:去完成, 1:可领取, 2:已领取

            if (currentValue >= task.TargetValue && status == 0)
            {
                status = 1;
            }

            // 已领取或可领取（完成）的任务都计入积分，用于领取对应档位奖励
            if (status == 2 || status == 1)
            {
                totalActivityPoint += task.ActivityPoint;
            }

            resultTasks.Add(new
            {
                id = task.Id.ToString(),
                title = task.Title,
                description = task.Description,
                icon = task.Icon,
                jumpPath = task.JumpPath,
                rewardAmount = task.RewardAmount,
                activityPoint = task.ActivityPoint,
                currentValue = currentValue,
                targetValue = task.TargetValue,
                status = status
            });
        }

        // 获取已领取的宝箱
        var claimedChests = await _fsql.Select<DMemberChest>()
            .Where(mc => mc.DMemberId == currentUserId && mc.ChestDate == today)
            .ToListAsync(mc => mc.ActivityPointTarget);

        return ApiResult.Success.SetData(new
        {
            tasks = resultTasks,
            totalActivityPoint = totalActivityPoint,
            claimedChests = claimedChests
        });
    }

    /// <summary>
    /// 领取任务奖励
    /// </summary>
    [HttpPost($"@{nameof(ClaimDailyTask)}")]
    public async Task<ApiResult> ClaimDailyTask([FromBody] string taskIdStr)
    {
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null)
            return ApiResult.Error.SetMessage("未登录或登录已过期");

        if (!long.TryParse(taskIdStr, out var taskId))
            return ApiResult.Error.SetMessage("无效的任务ID");

        var today = DateTime.Now.Date;

        using var uow = _fsql.CreateUnitOfWork();
        try
        {
            var task = await uow.Orm.Select<DTask>().Where(t => t.Id == taskId && t.IsEnabled).ToOneAsync();
            if (task == null) return ApiResult.Error.SetMessage("任务不存在或已失效");

            var memberTask = await uow.Orm.Select<DMemberTask>()
                .Where(mt => mt.DMemberId == currentUserId && mt.DTaskId == taskId && mt.TaskDate == today)
                .ForUpdate(true)
                .ToOneAsync();

            if (memberTask == null || memberTask.CurrentValue < task.TargetValue)
            {
                uow.Rollback();
                return ApiResult.Error.SetMessage("任务未完成，无法领取");
            }

            if (memberTask.Status == 2)
            {
                uow.Rollback();
                return ApiResult.Error.SetMessage("该任务奖励已领取");
            }

            memberTask.Status = 2; // 设置为已领取
            await uow.GetRepository<DMemberTask>().UpdateAsync(memberTask);

            // 加钱和加积分
            var member = await uow.Orm.Select<DMember>().Where(m => m.Id == currentUserId).ForUpdate(true).ToOneAsync();
            if (member != null)
            {
                var rewardDesc = "";
                if (task.RewardAmount > 0)
                {
                    member.CreditAmount += task.RewardAmount;
                    rewardDesc += $"{task.RewardAmount} 元 ";
                }
                if (task.ActivityPoint > 0)
                {
                    member.ActivityPoint += task.ActivityPoint;
                    rewardDesc += $"{task.ActivityPoint} 积分";
                }

                await uow.GetRepository<DMember>().UpdateAsync(member);

                var transAction = new DTransAction()
                {
                    DMemberId = member.Id,
                    DAgentId = member.DAgentId,
                    TransactionType = TransactionType.Activity, // 或 CheckIn
                    BeforeAmount = member.CreditAmount - task.RewardAmount,
                    AfterAmount = member.CreditAmount,
                    BetAmount = 0,
                    ActualAmount = task.RewardAmount,
                    CurrencyCode = "CNY",
                    SerialNumber = Guid.NewGuid().ToString("N"),
                    TransactionTime = TimeHelper.UtcUnix(),
                    Status = TransactionStatus.Success,
                    Description = $"领取每日任务[{task.Title}]奖励 {rewardDesc.Trim()}",
                };
                await uow.GetRepository<DTransAction>().InsertAsync(transAction);
            }

            uow.Commit();
            return ApiResult.Success.SetData(new { newBalance = member?.CreditAmount ?? 0 }).SetMessage("领取成功");
        }
        catch (Exception ex)
        {
            uow.Rollback();
            return ApiResult.Error.SetMessage($"领取出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 领取活动宝箱
    /// </summary>
    [HttpPost($"@{nameof(ClaimActivityChest)}")]
    public async Task<ApiResult> ClaimActivityChest([FromBody] int targetPoint)
    {
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null)
            return ApiResult.Error.SetMessage("未登录或登录已过期");

        if (AppOnlyActivityChestTargets.Contains(targetPoint) && IsWebRequest())
            return ApiResult.Error.SetMessage("该积分宝箱仅支持在 App 内领取");

        var today = DateTime.Now.Date;

        using var uow = _fsql.CreateUnitOfWork();
        try
        {
            // 校验积分是否足够（已领取 status=2 或可领取 status=1 的任务都计入，完成即算积分）
            var completedTasks = await uow.Orm.Select<DMemberTask>()
                .Include(mt => mt.DTask)
                .Where(mt => mt.DMemberId == currentUserId && mt.TaskDate == today && (mt.Status == 2 || mt.Status == 1))
                .ToListAsync();

            int totalPoints = completedTasks.Sum(mt => mt.DTask?.ActivityPoint ?? 0);

            if (totalPoints < targetPoint)
                return ApiResult.Error.SetMessage($"当前积分不足（需要 {targetPoint}，当前 {totalPoints}）");

            // 检查是否已经领取过这个档位的宝箱
            bool alreadyClaimed = await uow.Orm.Select<DMemberChest>()
                .Where(mc => mc.DMemberId == currentUserId && mc.ChestDate == today && mc.ActivityPointTarget == targetPoint)
                .AnyAsync();

            if (alreadyClaimed)
                return ApiResult.Error.SetMessage("该积分宝箱今日已领取");

            if (!ActivityChestRewardByTarget.TryGetValue(targetPoint, out var chestReward))
                return ApiResult.Error.SetMessage("无效的积分宝箱档位");

            var mc = new DMemberChest
            {
                DMemberId = currentUserId.Value,
                ActivityPointTarget = targetPoint,
                RewardAmount = chestReward,
                ChestDate = today
            };
            await uow.GetRepository<DMemberChest>().InsertAsync(mc);

            var member = await uow.Orm.Select<DMember>().Where(m => m.Id == currentUserId).ForUpdate(true).ToOneAsync();
            if (member != null && chestReward > 0)
            {
                member.CreditAmount += chestReward;
                await uow.GetRepository<DMember>().UpdateAsync(member);

                var transAction = new DTransAction()
                {
                    DMemberId = member.Id,
                    DAgentId = member.DAgentId,
                    TransactionType = TransactionType.Activity,
                    BeforeAmount = member.CreditAmount - chestReward,
                    AfterAmount = member.CreditAmount,
                    BetAmount = 0,
                    ActualAmount = chestReward,
                    CurrencyCode = "CNY",
                    SerialNumber = Guid.NewGuid().ToString("N"),
                    TransactionTime = TimeHelper.UtcUnix(),
                    Status = TransactionStatus.Success,
                    Description = $"领取积分[{targetPoint}]宝箱奖励 {chestReward} 元",
                };
                await uow.GetRepository<DTransAction>().InsertAsync(transAction);
            }

            uow.Commit();
            return ApiResult.Success.SetData(new { newBalance = member?.CreditAmount ?? 0 }).SetMessage($"成功领取 {chestReward} 元奖励");
        }
        catch (Exception ex)
        {
            uow.Rollback();
            return ApiResult.Error.SetMessage($"领取宝箱出错: {ex.Message}");
        }
    }
}
