using System.ComponentModel.DataAnnotations;
using AdminBlazor.Infrastructure.Encrypt;
using FreeScheduler;
using Microsoft.AspNetCore.Mvc;
using J9_Admin.Utils;

namespace J9_Admin.API;

/// <summary>
/// 通用API基类
/// </summary>
[ApiController]
public class BaseService : ControllerBase
{
    private static readonly string[] AvatarExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp"];

    #region 受保护的依赖注入字段 - Protected Dependency Injection Fields
    protected readonly FreeSqlCloud _freeSqlCloud;
    protected readonly Scheduler _scheduler;
    protected readonly ILogger<BaseService> _logger;
    protected readonly AdminContext _adminContext;
    protected readonly IWebHostEnvironment _webHostEnvironment;
    protected readonly IFreeSql _fsql;
    protected readonly IConfiguration _configuration;
    #endregion

    /// <summary>
    /// 基类构造注入
    /// </summary>
    /// <param name="freeSqlCloud">FreeSql云实例</param>
    /// <param name="scheduler">调度器实例</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="adminContext">管理员上下文</param>
    /// <param name="configuration">配置</param>
    protected BaseService(FreeSqlCloud freeSqlCloud, Scheduler scheduler, ILogger<BaseService> logger, AdminContext adminContext, IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
    {
        _freeSqlCloud = freeSqlCloud;
        _scheduler = scheduler;
        _logger = logger;
        _adminContext = adminContext;
        _fsql = adminContext.Orm;
        _configuration = configuration;
        _webHostEnvironment = webHostEnvironment;
    }

    #region 通用辅助方法 - Common Helper Methods

    /// <summary>
    /// 解析当前用户
    /// </summary>
    /// <param name="token">可选；仅内部特殊场景传入，一般为 null 以使用请求头。</param>
    /// <returns>用户ID，如果失败则返回null</returns>
    [NonAction]
    public async Task<long?> GetCurrentUserIdAsync(string? token = null)
    {
        const string methodName = nameof(GetCurrentUserIdAsync);

        try
        {
            // 从请求头中获取Authorization token
            var headerToken = HttpContext.Request.Headers["Authorization"].FirstOrDefault<string>();
            token = token ?? headerToken;

            // 开发模式下，如果没有token，返回第一个用户的ID
            if (_webHostEnvironment.IsDevelopment())
            {
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogInformation($"[{methodName}] 开发模式：未找到Authorization头信息，返回默认用户ID");
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
            else
            {
                // 生产模式下，如果没有token，返回null
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning($"[{methodName}] 未找到Authorization头信息");
                    return null;
                }
            }

            // 移除Bearer前缀（如果存在）
            if (token.StartsWith("Bearer "))
            {
                token = token.Substring(7);
            }

            // 开发模式下，如果token验证失败，也返回默认用户ID
            try
            {
                // 解密token
                var decryptedToken = DesEncrypt.Decrypt(token);

                // 分割token内容，格式应该是：userId|loginTime
                var parts = decryptedToken.Split('|');

                if (parts.Length != 2)
                {
                    if (_webHostEnvironment.IsDevelopment())
                    {
                        _logger.LogInformation($"[{methodName}] 开发模式：Token格式不正确，返回默认用户ID");
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
                        _logger.LogWarning($"[{methodName}] Token格式不正确，期望格式：userId|loginTime");
                    }
                    return null;
                }

                // 解析用户ID
                if (long.TryParse(parts[0], out var userId))
                {
                    // 验证用户是否存在于数据库中
                    var userExists = await _fsql.Select<DMember>()
                        .Where(m => m.Id == userId)
                        .AnyAsync();

                    if (userExists)
                    {
                        _logger.LogInformation($"[{methodName}] 成功获取当前用户ID：{userId}");
                        return userId;
                    }
                    else
                    {
                        if (_webHostEnvironment.IsDevelopment())
                        {
                            _logger.LogInformation($"[{methodName}] 开发模式：用户ID {userId} 在数据库中不存在，返回默认用户ID");
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
                            _logger.LogWarning($"[{methodName}] 用户ID {userId} 在数据库中不存在");
                        }
                        return null;
                    }
                }
                else
                {
                    if (_webHostEnvironment.IsDevelopment())
                    {
                        _logger.LogInformation($"[{methodName}] 开发模式：无法解析用户ID，返回默认用户ID");
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
                        _logger.LogWarning($"[{methodName}] 无法解析用户ID：{parts[0]}");
                    }
                    return null;
                }
            }
            catch (Exception tokenEx)
            {
                // token解密或解析失败
                if (_webHostEnvironment.IsDevelopment())
                {
                    _logger.LogInformation(tokenEx, $"[{methodName}] 开发模式：Token验证失败，返回默认用户ID");
                    var defaultUser = await _fsql.Select<DMember>()
                        .OrderBy(u => u.Id)
                        .FirstAsync();
                    if (defaultUser != null)
                    {
                        return defaultUser.Id;
                    }
                }
                // 非开发模式，让外层catch处理
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, $"[{methodName}] 获取当前用户ID时发生异常");
            // 开发模式下，异常也返回默认用户ID
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
                    // 如果获取默认用户也失败，返回null
                }
            }
            return null;
        }
    }

    /// <summary>
    /// 按账号查会员
    /// </summary>
    /// <param name="Username">会员账号</param>
    /// <returns>会员信息，如果未找到则返回null</returns>
    protected async Task<DMember?> FindMemberByAccountAsync(string Username)
    {
        return await _fsql.Select<DMember>().Include(m => m.DAgent)
            .Where(m => m.Username == Username)
            .ToOneAsync();
    }

    /// <summary>
    /// 从默认头像目录中随机取一个头像地址。
    /// </summary>
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



    /// <summary>
    /// 校验会员代理
    /// </summary>
    /// <param name="Username">会员账号</param>
    /// <returns>验证结果，包含会员和代理信息</returns>
    protected async Task<(ApiResult? ErrorResult, DMember? Member, DAgent? Agent)> ValidateMemberAndAgentAsync(string Username)
    {
        // 查找会员信息
        var member = await FindMemberByAccountAsync(Username);
        if (member == null)
        {
            return (ApiResult.Error.SetMessage("会员未找到"), null, null);
        }

        return (null, member, member.DAgent);
    }

    /// <summary>
    /// 校验会员代理
    /// </summary>
    /// <param name="memberId">会员ID</param>
    /// <returns>验证结果，包含会员和代理信息</returns>
    protected async Task<(ApiResult? ErrorResult, DMember? Member, DAgent? Agent)> ValidateMemberAndAgentAsync(long memberId)
    {
        // 查找会员信息
        var member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
        if (member == null)
        {
            return (ApiResult.Error.SetMessage("会员未找到"), null, null);
        }

        return (null, member, member.DAgent);
    }

    /// <summary>
    /// 记账并改余额
    /// </summary>
    /// <param name="member">会员信息</param>
    /// <param name="serialNumber">流水号</param>
    /// <param name="amount">交易金额（正数为增加，负数为减少）</param>
    /// <param name="transactionType">交易类型</param>
    /// <param name="description">交易描述</param>
    /// <param name="gameRound">游戏轮次（可选）</param>
    /// <param name="betAmount">投注金额（可选）</param>
    /// <returns>操作结果</returns>
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

            // 创建交易记录
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
                ActualAmount = Math.Abs(amount), // 使用绝对值
                CurrencyCode = "CNY",
                GameRound = gameRound,
                TransactionTime = TimeHelper.UtcUnix(),
                Status = TransactionStatus.Success,
                Description = description,
            };

            // 插入交易记录
            await uow.GetRepository<DTransAction>().InsertAsync(transaction);

            // 更新会员余额
            member.CreditAmount += amount;
            await uow.GetRepository<DMember>().UpdateAsync(member);

            // 提交事务
            uow.Commit();

            return ApiResult.Success.SetMessage("交易记录创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "创建交易记录时发生异常，会员ID: {MemberId}, 金额: {Amount}", member.Id, amount);
            return ApiResult.Error.SetMessage($"创建交易记录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新日任务进度
    /// </summary>
    protected async Task TryUpdateTaskProgressAsync(long memberId, string taskType, int incrementValue = 1)
    {
        try
        {
            var tasks = await _fsql.Select<J9_Admin.Entities.DTask>()
                .Where(t => t.IsEnabled && t.TaskType == taskType)
                .ToListAsync();

            if (!tasks.Any()) return;

            foreach (var task in tasks)
            {
                var memberTask = await _fsql.Select<J9_Admin.Entities.DMemberTask>()
                    .Where(t => t.DMemberId == memberId && t.DTaskId == task.Id && t.TaskDate.Date == DateTime.Today)
                    .FirstAsync();

                if (memberTask == null)
                {
                    memberTask = new J9_Admin.Entities.DMemberTask
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
                    await _fsql.Update<J9_Admin.Entities.DMemberTask>()
                        .SetSource(memberTask)
                        .UpdateColumns(x => new { x.CurrentValue, x.Status })
                        .ExecuteAffrowsAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"更新任务进度失败 [MemberId: {memberId}, TaskType: {taskType}]");
        }
    }

    #endregion
}

#region 请求模型类 - Request Model Classes


/// <summary>
/// 注册请求体
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// 手机号账号
    /// </summary>
    [Required]
    [MinLength(4)]
    [RegularExpression(@"^1[3-9]\d{9}$", ErrorMessage = "请输入有效的手机号码")]
    public string Username { get; set; }

    /// <summary>
    /// 登录密码
    /// </summary>
    [Required]
    [MinLength(4)]
    public string Password { get; set; }

    /// <summary>
    /// 设备指纹
    /// </summary>
    [Required]
    [MinLength(10)]
    public string BrowserFingerprint { get; set; }

    /// <summary>
    /// 代理编号。传 0 时由服务端替换为默认代理。若同时传 <see cref="AgentName"/>，以代理名为准。
    /// </summary>
    [Required]
    public long AgentId { get; set; }

    /// <summary>
    /// 代理名（与后台「代理管理」中配置一致）。非空时优先按名称解析代理，忽略 <see cref="AgentId"/>。
    /// </summary>
    [StringLength(100)]
    public string? AgentName { get; set; }

    /// <summary>
    /// 邀请码
    /// </summary>
    public string InviteCode { get; set; } = "";


}

/// <summary>
/// 上传头像请求
/// </summary>
public class UploadAvatarRequest
{
    /// <summary>
    /// 头像内容
    /// </summary>
    [Required(ErrorMessage = "头像数据不能为空")]
    public string Avatar { get; set; } = string.Empty;
}



#endregion
