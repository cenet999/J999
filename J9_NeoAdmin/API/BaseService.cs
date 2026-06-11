using FreeScheduler;
using J9_NeoAdmin.Utils;
using Microsoft.AspNetCore.Mvc;
using NeoAdmin.Blazor.Core.Identity;

namespace J9_NeoAdmin.API;

/// <summary>
/// 通用API基类
/// </summary>
[ApiController]
public class BaseService : ControllerBase
{
    private static readonly string[] AvatarExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp"];

    protected readonly IFreeSql _fsql;
    protected readonly Scheduler _scheduler;
    protected readonly ILogger<BaseService> _logger;
    protected readonly IWebHostEnvironment _webHostEnvironment;
    protected readonly IConfiguration _configuration;
    protected readonly NeoAdminAuthService _authService;

    protected BaseService(
        IFreeSql freeSql,
        Scheduler scheduler,
        ILogger<BaseService> logger,
        IConfiguration configuration,
        IWebHostEnvironment webHostEnvironment,
        NeoAdminAuthService authService)
    {
        _fsql = freeSql;
        _scheduler = scheduler;
        _logger = logger;
        _configuration = configuration;
        _webHostEnvironment = webHostEnvironment;
        _authService = authService;
    }

    [NonAction]
    public async Task<long?> GetCurrentUserIdAsync(string? token = null)
    {
        const string methodName = nameof(GetCurrentUserIdAsync);

        try
        {
            var headerToken = HttpContext.Request.Headers["Authorization"].FirstOrDefault<string>();
            token = token ?? headerToken;

            if (_webHostEnvironment.IsDevelopment())
            {
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogInformation("[{Method}] 开发模式：未找到Authorization头信息，返回默认用户ID", methodName);
                    var defaultUser = await _fsql.Select<DMember>()
                        .OrderBy(u => u.Id)
                        .FirstAsync();
                    if (defaultUser != null)
                    {
                        return defaultUser.Id;
                    }

                    return null;
                }
            }
            else if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("[{Method}] 未找到Authorization头信息", methodName);
                return null;
            }

            if (token.StartsWith("Bearer "))
            {
                token = token.Substring(7);
            }

            var checkResult = await _authService.CheckAsync(token);
            if (checkResult.Succeeded && checkResult.Data != null)
            {
                _logger.LogInformation("[{Method}] 成功获取当前用户ID：{UserId}", methodName, checkResult.Data.Id);
                return checkResult.Data.Id;
            }

            if (_webHostEnvironment.IsDevelopment())
            {
                _logger.LogInformation("[{Method}] 开发模式：Token验证失败，返回默认用户ID", methodName);
                var defaultUser = await _fsql.Select<DMember>()
                    .OrderBy(u => u.Id)
                    .FirstAsync();
                if (defaultUser != null)
                {
                    return defaultUser.Id;
                }
            }
            else
            {
                _logger.LogWarning("[{Method}] Token验证失败：{Message}", methodName, checkResult.Message);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "[{Method}] 获取当前用户ID时发生异常", methodName);
            if (_webHostEnvironment.IsDevelopment())
            {
                try
                {
                    var defaultUser = await _fsql.Select<DMember>()
                        .OrderBy(u => u.Id)
                        .FirstAsync();
                    if (defaultUser != null)
                    {
                        return defaultUser.Id;
                    }
                }
                catch
                {
                }
            }

            return null;
        }
    }

    protected async Task<DMember?> FindMemberByAccountAsync(string Username)
    {
        return await _fsql.Select<DMember>().Include(m => m.DAgent)
            .Where(m => m.Username == Username)
            .ToOneAsync();
    }

    protected string GetRandomDefaultAvatarUrl()
    {
        const string defaultAvatarDirectory = "qq_classic_35_avatars";
        var avatarsDir = Path.Combine(_webHostEnvironment.WebRootPath, defaultAvatarDirectory);
        if (!Directory.Exists(avatarsDir))
        {
            _logger.LogWarning("默认头像目录不存在：{AvatarDir}", avatarsDir);
            return string.Empty;
        }

        var avatarFiles = Directory.GetFiles(avatarsDir)
            .Where(file => AvatarExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .ToArray();

        if (avatarFiles.Length == 0)
        {
            _logger.LogWarning("默认头像目录没有可用文件：{AvatarDir}", avatarsDir);
            return string.Empty;
        }

        var fileName = avatarFiles[Random.Shared.Next(avatarFiles.Length)];
        var avatarPath = $"/{defaultAvatarDirectory}/{fileName}";
        var apiDomain = _configuration["APIDomain"]?.Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(apiDomain) ? avatarPath : $"{apiDomain}{avatarPath}";
    }

    protected async Task<(ApiResult? ErrorResult, DMember? Member, DAgent? Agent)> ValidateMemberAndAgentAsync(string Username)
    {
        var member = await FindMemberByAccountAsync(Username);
        if (member == null)
        {
            return (ApiResult.Error.SetMessage("会员未找到"), null, null);
        }

        return (null, member, member.DAgent);
    }

    protected async Task<(ApiResult? ErrorResult, DMember? Member, DAgent? Agent)> ValidateMemberAndAgentAsync(long memberId)
    {
        var member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
        if (member == null)
        {
            return (ApiResult.Error.SetMessage("会员未找到"), null, null);
        }

        return (null, member, member.DAgent);
    }

    protected async Task<ApiResult> CreateTransactionAsync(
        DMember member,
        string serialNumber,
        decimal amount,
        TransactionType transactionType,
        string description,
        string gameRound = "",
        decimal betAmount = 0,
        long dGameId = 0)
    {
        try
        {
            using var uow = _fsql.CreateUnitOfWork();

            var transaction = new DTransAction()
            {
                SerialNumber = serialNumber,
                DMemberId = member.Id,
                DAgentId = member.DAgentId,
                DGameId = dGameId,
                TransactionType = transactionType,
                BeforeAmount = member.CreditAmount,
                AfterAmount = member.CreditAmount + amount,
                BetAmount = betAmount,
                ActualAmount = Math.Abs(amount),
                CurrencyCode = "CNY",
                GameRound = gameRound,
                TransactionTime = TimeHelper.UtcUnix(),
                Status = TransactionStatus.Success,
                Description = description,
            };

            await uow.GetRepository<DTransAction>().InsertAsync(transaction);

            member.CreditAmount += amount;
            await uow.GetRepository<DMember>().UpdateAsync(member);

            uow.Commit();

            return ApiResult.Success.SetMessage("交易记录创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "创建交易记录时发生异常，会员ID: {MemberId}, 金额: {Amount}", member.Id, amount);
            return ApiResult.Error.SetMessage($"创建交易记录失败: {ex.Message}");
        }
    }

    protected async Task TryUpdateTaskProgressAsync(long memberId, string taskType, int incrementValue = 1)
    {
        try
        {
            var tasks = await _fsql.Select<Entities.DTask>()
                .Where(t => t.IsEnabled && t.TaskType == taskType)
                .ToListAsync();

            if (!tasks.Any())
            {
                return;
            }

            foreach (var task in tasks)
            {
                var memberTask = await _fsql.Select<Entities.DMemberTask>()
                    .Where(t => t.DMemberId == memberId && t.DTaskId == task.Id && t.TaskDate.Date == DateTime.Today)
                    .FirstAsync();

                if (memberTask == null)
                {
                    memberTask = new Entities.DMemberTask
                    {
                        DMemberId = memberId,
                        DTaskId = task.Id,
                        TaskDate = DateTime.Today,
                        CurrentValue = incrementValue,
                        Status = incrementValue >= task.TargetValue ? 1 : 0
                    };
                    await _fsql.Insert(memberTask).ExecuteAffrowsAsync();
                }
                else if (memberTask.Status == 0)
                {
                    memberTask.CurrentValue += incrementValue;
                    if (memberTask.CurrentValue >= task.TargetValue)
                    {
                        memberTask.Status = 1;
                    }

                    await _fsql.Update<Entities.DMemberTask>()
                        .SetSource(memberTask)
                        .UpdateColumns(x => new { x.CurrentValue, x.Status })
                        .ExecuteAffrowsAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新任务进度失败 [MemberId: {MemberId}, TaskType: {TaskType}]", memberId, taskType);
        }
    }
}
