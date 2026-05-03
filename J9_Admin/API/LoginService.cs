using System.ComponentModel.DataAnnotations;
using AdminBlazor.Infrastructure.Encrypt;
using BootstrapBlazor.Components;
using FreeScheduler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using J9_Admin.Utils;
using RestSharp;
using J9_Admin.API.DTOs;

namespace J9_Admin.API;

/// <summary>
/// 会员登录接口
/// </summary>
[ApiController]
[Route("api/login")]
[Tags("会员系统")]
public class LoginService : BaseService
{
    private readonly TGMessageApi _TGMessageApi;
    public LoginService(FreeSqlCloud freeSqlCloud, Scheduler scheduler, ILogger<LoginService> logger, AdminContext adminContext, IConfiguration configuration, TGMessageApi TGMessageApi, IWebHostEnvironment webHostEnvironment)
        : base(freeSqlCloud, scheduler, logger, adminContext, configuration, webHostEnvironment)
    {
        _TGMessageApi = TGMessageApi ?? throw new ArgumentNullException(nameof(TGMessageApi));
    }

    /// <summary>
    /// 会员注册
    /// </summary>
    [HttpPost($"@{nameof(Register)}")]
    [AllowAnonymous]
    public async Task<ApiResult> Register([FromBody] RegisterRequest request)
    {
        
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
            return ApiResult.Error.SetMessage(string.Join(", ", validationResults.Select(x => x.ErrorMessage)));

        using var uow = _fsql.CreateUnitOfWork();

        try
        {
            DAgent? dAgent = null;
            var nameKey = request.AgentName?.Trim();
            if (!string.IsNullOrEmpty(nameKey))
            {
                var enabledByName = await uow.Orm.Select<DAgent>()
                    .Where(a => a.AgentName == nameKey && a.IsEnabled)
                    .ToListAsync();
                if (enabledByName.Count > 1)
                    return ApiResult.Error.SetMessage("代理名重复，无法识别推广渠道，请联系客服");
                dAgent = enabledByName.Count == 1 ? enabledByName[0] : null;
            }
            else if (request.AgentId > 0)
            {
                dAgent = await uow.Orm.Select<DAgent>()
                    .Where(a => a.Id == request.AgentId && a.IsEnabled)
                    .ToOneAsync();
            }
            else
            {
                dAgent = await uow.Orm.Select<DAgent>().OrderBy(a=>a.Id).ToOneAsync();
            }

            if (dAgent == null)
            {
                if (!string.IsNullOrEmpty(nameKey))
                    return ApiResult.Error.SetMessage("代理名不存在或已停用");
                if (request.AgentId > 0)
                    return ApiResult.Error.SetMessage("代理不存在或已停用");
                return ApiResult.Error.SetMessage("系统默认代理配置异常，请联系客服");
            }

            // 检查会员账号是否已存在（库表 Username 全局唯一，与 DAgentId 无关）
            var usernameTaken = await uow.Orm.Select<DMember>()
                .Where(a => a.Username == request.Username)
                .AnyAsync();
            if (usernameTaken)
            {
                return ApiResult.Error.SetMessage("该手机号已被注册");
            }

            // 验证IP注册限制
            var registerIp = IpHelper.GetClientIpAddress(HttpContext);
            var existingIpMember = await uow.Orm.Select<DMember>()
                .Where(a => a.RegisterIp == registerIp && a.CreatedTime >= DateTime.Now.AddMinutes(-10))
                .AnyAsync();
#if !DEBUG
            if (existingIpMember)
            {
                // 业务逻辑验证失败，不需要回滚
                _logger.LogWarning("该IP地址已注册账号，10分钟内不允许重复注册: {RegisterIp}", registerIp);
                return ApiResult.Error.SetMessage("该IP地址已注册账号，10分钟内不允许重复注册");
            }
#endif

            // 查找邀请人信息
            DMember? inviter = null;
            if (!string.IsNullOrEmpty(request.InviteCode))
            {
                inviter = await uow.Orm.Select<DMember>().Where(a => a.InviteCode == request.InviteCode).FirstAsync();
                // 邀请码不存在时静默忽略，不提示用户，按无邀请人注册
            }

            // 创建会员
            var newMember = await uow.GetRepository<DMember>().InsertOrUpdateAsync(new DMember
            {
                Username = request.Username,
                Nickname = request.Username,
                Password = request.Password,
                BrowserFingerprint = request.BrowserFingerprint,
                RegisterIp = registerIp,
                CreatedTime = DateTime.Now,
                IsEnabled = true,
                ParentId = inviter?.Id ?? 0,
                InviteCode = new Random().Next(1000, 10000).ToString(),
                DAgentId = dAgent.Id,
                CreditAmount = 2,
                Avatar = GetRandomDefaultAvatarUrl(),
                Telegram = "",
                LoginTime = DateTime.Now,
                UpdatedTime = DateTime.Now,
                WithdrawPassword = "123456",
                // 注册时默认使用所属代理的 USDT 收款地址，若代理未配置则落回空串
                USDTAddress = dAgent.UsdtAddress ?? "usdt00000000",
            });

            // 分配角色
            var sysRole = await uow.Orm.Select<SysRole>().Where(a => a.Name == "普通用户").ToOneAsync();
            await uow.GetRepository<SysRoleUser>().InsertOrUpdateAsync(new SysRoleUser
            {
                RoleId = sysRole.Id,
                UserId = newMember.Id
            });

            _logger.LogInformation("会员 {Username} 注册成功", request.Username);

            // 提交事务
            uow.Commit();

            // 邀请人完成任务：给邀请人更新 Invite 任务进度
            if (inviter != null)
            {
                _ = TryUpdateTaskProgressAsync(inviter.Id, "Invite", 1);
            }

            // 注册成功后，给代理发送 Telegram 通知（发到代理绑定的 ChatId，不是会员资料里的 Telegram 昵称）
            var agent = await _fsql.Select<DAgent>().Where(a => a.Id == newMember.DAgentId).ToOneAsync();
            if (agent != null && !string.IsNullOrWhiteSpace(agent.TelegramChatId))
            {
                var html = $"""
有新用户注册：
账号：{TGMessageApi.EscapeHtml(request.Username)}
注册时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}
IP：{TGMessageApi.EscapeHtml(registerIp)}
请及时关注。
""";
                await _TGMessageApi.SendMessageAsync(agent.TelegramChatId, html);
            }
            else if (agent == null)
            {
                _logger.LogInformation("注册成功但会员 DAgentId={AgentId} 未找到代理记录，跳过 Telegram 通知", newMember.DAgentId);
            }
            else
            {
                _logger.LogInformation("注册成功但代理 Id={AgentId} 未配置 TelegramChatId，跳过 Telegram 通知", agent.Id);
            }

            return ApiResult.Success.SetData(newMember);
        }
        catch (Exception ex)
        {
            // 回滚事务
            uow.Rollback();

            _logger.LogInformation(ex, "会员 {Username} 注册失败，错误：{Error}", request.Username, ex.Message);
            var detail = ex.Message ?? "";
            if (detail.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
            {
                if (detail.Contains("Username", StringComparison.OrdinalIgnoreCase))
                    return ApiResult.Error.SetMessage("该手机号已被注册");
                if (detail.Contains("InviteCode", StringComparison.OrdinalIgnoreCase))
                    return ApiResult.Error.SetMessage("注册繁忙，请稍后再试");
            }
            return ApiResult.Error.SetMessage($"注册失败: {detail}");
        }
    }

    private static ConcurrentDictionary<string, int> limit = new ConcurrentDictionary<string, int>();
    private string? removeLimit;

    /// <summary>
    /// 会员登录
    /// </summary>
    [HttpPost($"@{nameof(Login)}")]
    [AllowAnonymous]
    public async Task<ApiResult> Login(string Username, string Password)
    {
        using var uow = _fsql.CreateUnitOfWork();

        try
        {

            string ip = IpHelper.GetClientIpAddress(HttpContext);

            if (limit.TryGetValue(ip, out int count) && count >= 5)
            {
                // 将中文提示翻译为英文，提示用户操作频率过高
                return ApiResult.Error.SetMessage($"{ip} 操作频率过高，请稍后再试...");
            }
            SysUserLoginLog log = new SysUserLoginLog()
            {
                LoginTime = DateTime.Now,
                Username = Username,
                Ip = ip,
                Browser = "API",
                UserAgent = (string)HttpContext.Request.Headers["User-Agent"]
            };

            var user = await uow.Orm.Select<DMember>()
                .Where(a => a.Username == Username && a.Password == Password)
                .OrderByDescending(a => a.CreatedTime)?.FirstAsync();

            if (user == null || user.Password != Password)
            {
                limit.AddOrUpdate(ip, ++count, (Func<string, int, int>)((_, __) => count));
                if (removeLimit == null)
                    _scheduler.AddTempTask(TimeSpan.FromSeconds(60L), (Action)(() =>
                    {
                        removeLimit = null;
                        limit.TryRemove(ip, out int _);
                    }));
                log.Type = SysUserLoginLog.LogType.登陆失败;
                await uow.GetRepository<SysUserLoginLog>().InsertAsync(log);
                uow.Commit();
                _logger.LogWarning("登录失败，会员账号：{Username}", Username);
                // 登录失败，会员账号或密码错误，返回错误信息，并附带当前IP的限制次数
                return ApiResult.Error.SetMessage($"登录失败，会员账号或密码错误，当前限制次数：{limit[ip]}");
            }

            // 更新登录时间
            user.LoginTime = DateTime.Now;
            log.Type = SysUserLoginLog.LogType.登陆成功;
            await uow.GetRepository<DMember>().InsertOrUpdateAsync(user);
            await uow.GetRepository<SysUserLoginLog>().InsertAsync(log);
            uow.Commit();

            // 触发每日登录任务进度更新
            _ = TryUpdateTaskProgressAsync(user.Id, "Login");

            // 登录成功后，给代理发送 Telegram 通知（发到代理绑定的 ChatId，不是会员资料里的 Telegram 昵称）
            var agent = await _fsql.Select<DAgent>().Where(a => a.Id == user.DAgentId).ToOneAsync();
            if (agent != null && !string.IsNullOrWhiteSpace(agent.TelegramChatId))
            {
                var html = $"""
用户登录：
账号：{TGMessageApi.EscapeHtml(user.Username)}
登录时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}
IP：{TGMessageApi.EscapeHtml(ip)}
请及时关注。
""";
                await _TGMessageApi.SendMessageAsync(agent.TelegramChatId, html);
            }
            else if (agent == null)
            {
                _logger.LogInformation("登录成功但会员 DAgentId={AgentId} 未找到代理记录，跳过 Telegram 通知", user.DAgentId);
            }
            else
            {
                _logger.LogInformation("登录成功但代理 Id={AgentId} 未配置 TelegramChatId，跳过 Telegram 通知", agent.Id);
            }

            var token = DesEncrypt.Encrypt(user.Id + "|" + user.LoginTime.ToString("yyyy-MM-dd HH:mm:ss"));
            return ApiResult.Success.SetData(token);
        }
        catch (Exception ex)
        {
            // 回滚事务
            uow.Rollback();

            _logger.LogInformation(ex, "会员 {Username} 登录失败，错误：{Error}", Username, ex.Message);
            return ApiResult.Error.SetMessage($"登录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 退出登录
    /// </summary>
    [HttpGet($"@{nameof(Logout)}")]
    public Task<ApiResult> Logout(string tenantId = "main")
    {
        HttpContext?.Response.Cookies.Delete("AdminBlazor_login_" + tenantId);
        return Task.FromResult<ApiResult>(ApiResult.Success.SetData(true));
    }

    /// <summary>
    /// 检查登录状态
    /// </summary>
    [HttpGet($"@{nameof(Check)}")]
    public async Task<ApiResult> Check()
    {
        try
        {
            _logger.LogInformation("Check login status");
            var userId = await GetCurrentUserIdAsync();

            if (userId == null)
                return ApiResult.Error.SetCode(401).SetMessage("未登录或登录已过期");

            var member = await _fsql.Select<DMember>().Include(a => a.DAgent).Where(a => a.Id == userId).FirstAsync();

            if (member == null)
                return ApiResult.Error.SetCode(401).SetMessage("用户不存在");

            // 如果邀请码为空，随机生成一个4位数字并保存
            if (string.IsNullOrEmpty(member.InviteCode))
            {
                member.InviteCode = new Random().Next(1000, 10000).ToString();
                await _fsql.Update<DMember>().Set(a => a.InviteCode, member.InviteCode).Where(a => a.Id == member.Id).ExecuteAffrowsAsync();
            }

            // 获取今日的投注金额
            var todayBet = await _fsql.Select<DTransAction>().Where(a => a.DMemberId == userId && a.CreatedTime >= DateTime.Today && a.CreatedTime < DateTime.Today.AddDays(1)).SumAsync(a => a.BetAmount);
            var totalBet = await _fsql.Select<DTransAction>().Where(a => a.DMemberId == userId).SumAsync(a => a.BetAmount);

            // 与 TransActionService.PlayerRebate 一致：仅统计成功投注且未返水，按有效投注额合计后再乘返水比例
            var rebateTotalAmount = await _fsql.Select<DTransAction>()
                .Where(a => a.DMemberId == userId
                    && a.TransactionType == TransactionType.Bet
                    && a.Status == TransactionStatus.Success
                    && a.IsRebate == false)
                .SumAsync(a => a.ValidBetAmount);
            var rebateRate = member.DAgent?.RebateRate ?? 0;
            var rebateAmount = rebateTotalAmount * rebateRate;


            var response = new DMemberInfoResponse
            {
                Id = member.Id,
                Username = member.Username,
                Nickname = member.Nickname,
                Avatar = member.Avatar,
                Telegram = member.Telegram,
                CreditAmount = member.CreditAmount,
                ActivityPoint = member.ActivityPoint,
                IsEnabled = member.IsEnabled,
                CreatedTime = member.CreatedTime,
                UpdatedTime = member.UpdatedTime,
                ParentId = member.ParentId,
                DAgentId = member.DAgentId,
                AgentName = member.DAgent?.AgentName,
                USDTAddress = member.USDTAddress,
                PhoneNumber = member.PhoneNumber,
                RebateTotalAmount = rebateTotalAmount,
                RebateAmount = rebateAmount,
                TodayBet = todayBet,
                TotalBet = totalBet,
                WithdrawPassword = member.WithdrawPassword,
                InviteCode = member.InviteCode,
                VipLevel = DMemberInfoResponse.CalcVipLevel(totalBet),
            };

            return ApiResult.Success.SetData(response);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "登录信息验证失败:" + ex.Message);
            return ApiResult.Error.SetMessage("登录信息验证失败:" + ex.Message);
        }
    }

    /// <summary>
    /// 修改登录密码
    /// </summary>
    [HttpPost($"@{nameof(ChangePassword)}")]
    public async Task<ApiResult> ChangePassword(string OldPassword, string NewPassword)
    {
        if (string.IsNullOrWhiteSpace(OldPassword))
        {
            return ApiResult.Error.SetMessage("请输入原密码");
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            return ApiResult.Error.SetMessage("请输入新密码");
        }

        // 先获取当前用户ID
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        using var uow = _fsql.CreateUnitOfWork();

        try
        {
            var member = await uow.Orm.Select<DMember>().Where(m => m.Id == currentUserId).ToOneAsync();
            if (member == null)
            {
                return ApiResult.Error.SetMessage("会员未找到");
            }

            if (member.Password != OldPassword)
            {
                _logger.LogWarning("会员 {MemberId} 修改登录密码失败：原密码不正确", currentUserId);
                return ApiResult.Error.SetMessage("原密码不正确");
            }

            member.Password = NewPassword;
            member.UpdatedTime = DateTime.Now;
            await uow.GetRepository<DMember>().InsertOrUpdateAsync(member);

            _logger.LogInformation("会员 {MemberId} 登录密码修改成功", currentUserId);

            uow.Commit();

            return ApiResult.Success.SetMessage("登录密码修改成功");
        }
        catch (Exception ex)
        {
            uow.Rollback();

            _logger.LogInformation(ex, "会员 {MemberId} 登录密码修改失败，错误：{Error}", currentUserId, ex.Message);
            return ApiResult.Error.SetMessage($"登录密码修改失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 修改提现密码
    /// </summary>
    [HttpPost($"@{nameof(ChangeWithdrawPassword)}")]
    public async Task<ApiResult> ChangeWithdrawPassword(string LoginPassword, string NewWithdrawPassword)
    {
        if (string.IsNullOrWhiteSpace(LoginPassword))
        {
            return ApiResult.Error.SetMessage("请输入登录密码");
        }

        if (string.IsNullOrWhiteSpace(NewWithdrawPassword))
        {
            return ApiResult.Error.SetMessage("请输入新的提现密码");
        }

        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        using var uow = _fsql.CreateUnitOfWork();

        try
        {
            var member = await uow.Orm.Select<DMember>().Where(m => m.Id == currentUserId).ToOneAsync();
            if (member == null)
            {
                return ApiResult.Error.SetMessage("会员未找到");
            }

            if (member.Password != LoginPassword)
            {
                _logger.LogWarning("会员 {MemberId} 修改提现密码失败：登录密码不正确", currentUserId);
                return ApiResult.Error.SetMessage("登录密码不正确");
            }

            member.WithdrawPassword = NewWithdrawPassword;
            member.UpdatedTime = DateTime.Now;
            await uow.GetRepository<DMember>().InsertOrUpdateAsync(member);

            _logger.LogInformation("会员 {MemberId} 提现密码修改成功", currentUserId);

            uow.Commit();

            return ApiResult.Success.SetMessage("提现密码修改成功");
        }
        catch (Exception ex)
        {
            uow.Rollback();

            _logger.LogInformation(ex, "会员 {MemberId} 提现密码修改失败，错误：{Error}", currentUserId, ex.Message);
            return ApiResult.Error.SetMessage($"提现密码修改失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 上传头像
    /// </summary>
    [HttpPost($"@{nameof(UploadAvatar)}")]
    public async Task<ApiResult> UploadAvatar([FromBody] UploadAvatarRequest request)
    {
        const string methodName = nameof(UploadAvatar);

        // 验证请求参数
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
            return ApiResult.Error.SetMessage(string.Join(", ", validationResults.Select(x => x.ErrorMessage)));

        // 先获取当前用户ID
        var currentUserId = await GetCurrentUserIdAsync();

        if (currentUserId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        using var uow = _fsql.CreateUnitOfWork();

        try
        {
            var member = await uow.Orm.Select<DMember>().Where(m => m.Id == currentUserId).ToOneAsync();
            if (member == null)
            {
                return ApiResult.Error.SetMessage("会员未找到");
            }

            string avatarUrl = request.Avatar;

            // 如果 Avatar 是 base64 数据，则保存到项目目录
            if (request.Avatar.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // 解析 base64 数据
                    var base64Data = request.Avatar.Split(',')[1];
                    var imageData = Convert.FromBase64String(base64Data);

                    // 获取文件扩展名
                    var header = request.Avatar.Split(',')[0];
                    var extension = "jpg"; // 默认扩展名
                    if (header.Contains("png", StringComparison.OrdinalIgnoreCase))
                        extension = "png";
                    else if (header.Contains("jpeg", StringComparison.OrdinalIgnoreCase) || header.Contains("jpg", StringComparison.OrdinalIgnoreCase))
                        extension = "jpg";
                    else if (header.Contains("gif", StringComparison.OrdinalIgnoreCase))
                        extension = "gif";
                    else if (header.Contains("webp", StringComparison.OrdinalIgnoreCase))
                        extension = "webp";

                    // 创建 avatars 目录（如果不存在）
                    var avatarsDir = Path.Combine(_webHostEnvironment.WebRootPath, "avatars");
                    if (!Directory.Exists(avatarsDir))
                    {
                        Directory.CreateDirectory(avatarsDir);
                    }

                    // 生成文件名：userId_timestamp.extension
                    var fileName = $"{currentUserId}_{DateTime.Now:yyyyMMddHHmmss}.{extension}";
                    var filePath = Path.Combine(avatarsDir, fileName);

                    // 保存文件
                    await System.IO.File.WriteAllBytesAsync(filePath, imageData);

                    // 生成访问URL
                    avatarUrl = $"/avatars/{fileName}";

                    _logger.LogInformation($"[{methodName}] 会员 {currentUserId} 头像已保存到：{filePath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[{methodName}] 保存头像文件失败，会员ID：{currentUserId}，错误：{ex.Message}");
                    return ApiResult.Error.SetMessage($"保存头像文件失败: {ex.Message}");
                }
            }

            member.Avatar = avatarUrl;
            await uow.GetRepository<DMember>().InsertOrUpdateAsync(member);

            _logger.LogInformation("会员 {MemberId} 头像上传成功", currentUserId);

            // 提交事务
            uow.Commit();

            return ApiResult.Success.SetData(new { avatar = avatarUrl }).SetMessage("头像上传成功");
        }
        catch (Exception ex)
        {
            // 回滚事务
            uow.Rollback();

            _logger.LogInformation(ex, "会员 {MemberId} 头像上传失败，错误：{Error}", currentUserId, ex.Message);
            return ApiResult.Error.SetMessage($"头像上传失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新会员资料
    /// </summary>
    [HttpPost($"@{nameof(UpdateMemberInfo)}")]
    public async Task<ApiResult> UpdateMemberInfo(string Telegram, string USDTAddress, string PhoneNumber, string WithdrawPassword)
    {
        // 先获取当前用户ID
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        using var uow = _fsql.CreateUnitOfWork();

        try
        {
            // 然后使用用户ID进行查询
            var member = await uow.Orm.Select<DMember>().Where(m => m.Id == currentUserId).ToOneAsync();
            if (member == null)
            {
                // 验证失败，不需要回滚，直接返回
                return ApiResult.Error.SetMessage("会员未找到");
            }

            member.Telegram = Telegram;
            member.USDTAddress = USDTAddress;
            member.PhoneNumber = PhoneNumber;
            // 仅在前端显式传入新的提现密码时才更新，避免空字符串覆盖已有密码。
            if (!string.IsNullOrWhiteSpace(WithdrawPassword))
            {
                member.WithdrawPassword = WithdrawPassword;
            }
            member.UpdatedTime = DateTime.Now;
            await uow.GetRepository<DMember>().InsertOrUpdateAsync(member);

            _logger.LogInformation("会员 {MemberId} 信息更新成功", currentUserId);

            // 提交事务
            uow.Commit();

            return ApiResult.Success.SetMessage("会员信息更新成功");
        }
        catch (Exception ex)
        {
            // 回滚事务
            uow.Rollback();

            _logger.LogInformation(ex, "会员 {MemberId} 信息更新失败，错误：{Error}", currentUserId, ex.Message);
            return ApiResult.Error.SetMessage($"会员信息更新失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取余额
    /// </summary>
    [HttpGet($"@{nameof(GetBalance)}")]
    public async Task<ApiResult> GetBalance()
    {
        // 先获取当前用户ID
        var currentUserId = await GetCurrentUserIdAsync();
        _logger.LogInformation("currentUserId: {CurrentUserId}", currentUserId);

        if (currentUserId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        // 然后使用用户ID进行查询
        var member = await _fsql.Select<DMember>().Where(m => m.Id == currentUserId).ToOneAsync();
        if (member == null)
        {
            return ApiResult.Error.SetMessage("会员未找到");
        }
        return ApiResult.Success.SetData(member.CreditAmount);
    }

    /// <summary>
    /// 申请代理
    /// </summary>
    [HttpPost($"@{nameof(ApplyAgent)}")]
    public async Task<ApiResult> ApplyAgent(string HomeUrl, string UsdtAddress, string ServerIP)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
            return ApiResult.Error.SetMessage("未登录或登录已过期");

        using var uow = _fsql.CreateUnitOfWork();

        try
        {
            var member = await uow.Orm.Select<DMember>().Where(a => a.Id == userId).ToOneAsync();
            if (member == null)
            {
                return ApiResult.Error.SetMessage("会员不存在");
            }

            var agent = await uow.Orm.Select<DAgent>().Where(a => a.Id == member.DAgentId).ToOneAsync();

            if (agent == null)
            {
                agent = new DAgent();
                agent.CreatedTime = DateTime.Now;
                agent.IsEnabled = false;
                agent.HomeUrl = HomeUrl;
                agent.ServerIP = ServerIP;
                agent.ParentId = 0;
                agent.AgentType = AgentType.General;
                agent.TelegramChatId = "";
                agent.UsdtAddress = UsdtAddress;

                // 创建代理记录
                agent = await uow.GetRepository<DAgent>().InsertOrUpdateAsync(agent);

                // 更新会员的代理ID
                member.DAgentId = agent.Id;
                await uow.GetRepository<DMember>().InsertOrUpdateAsync(member);

                _logger.LogInformation("会员 {MemberId} 申请代理成功", userId);

                // 提交事务
                uow.Commit();

                return ApiResult.Success.SetData(agent);
            }

            // 提交事务
            uow.Commit();

            return ApiResult.Success.SetData(agent);
        }
        catch (Exception ex)
        {
            // 回滚事务
            uow.Rollback();

            _logger.LogInformation(ex, "会员 {MemberId} 申请代理失败，错误：{Error}", userId, ex.Message);
            return ApiResult.Error.SetMessage($"申请代理失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 重置密码
    /// </summary>
    [HttpPost($"@{nameof(ResetPassword)}")]
    [AllowAnonymous]
    public async Task<ApiResult> ResetPassword(string HomeUrl, string Username, string BrowserFingerprint, string newPassword)
    {
        using var uow = _fsql.CreateUnitOfWork();

        try
        {
            var member = await uow.Orm.Select<DMember>().Include(a => a.DAgent).Where(a => a.Username == Username && a.BrowserFingerprint == BrowserFingerprint && a.DAgent.HomeUrl == HomeUrl).ToOneAsync();
            if (member == null)
            {
                // 验证失败，不需要回滚，直接返回
                return ApiResult.Error.SetMessage("会员或浏览器指纹不存在");
            }

            member.Password = newPassword;
            await uow.GetRepository<DMember>().InsertOrUpdateAsync(member);

            _logger.LogInformation("会员 {Username} 找回密码成功", Username);

            // 提交事务
            uow.Commit();

            return ApiResult.Success.SetMessage("密码修改成功");
        }
        catch (Exception ex)
        {
            // 回滚事务
            uow.Rollback();

            _logger.LogInformation(ex, "会员 {Username} 找回密码失败，错误：{Error}", Username, ex.Message);
            return ApiResult.Error.SetMessage($"密码找回失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 会员签到
    /// </summary>
    [HttpPost($"@{nameof(PlayerCheckIn)}")]
    [AllowAnonymous]
    public async Task<ApiResult> PlayerCheckIn()
    {
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        using var uow = _fsql.CreateUnitOfWork();

        try
        {
            var member = await uow.Orm.Select<DMember>().Where(m => m.Id == currentUserId).ToOneAsync();
            if (member == null)
            {
                return ApiResult.Error.SetMessage("会员不存在");
            }

            // 检查今日是否已签到
            if (member.LastCheckInDate.HasValue && member.LastCheckInDate.Value.Date == DateTime.Now.Date)
            {
                return ApiResult.Error.SetMessage("今日已签到，请明日再来");
            }

            // 更新连续签到天数
            if (member.LastCheckInDate.HasValue && member.LastCheckInDate.Value.Date == DateTime.Now.Date.AddDays(-1))
            {
                member.ContinuousCheckInDays += 1;
            }
            else
            {
                member.ContinuousCheckInDays = 1;
            }

            member.LastCheckInDate = DateTime.Now;

            // 固定奖励 20 积分积分
            int pointBonus = 20;

            // 增加积分积分
            member.ActivityPoint += pointBonus;

            // 写入交易记录
            var transAction = new DTransAction()
            {
                DMemberId = member.Id,
                DAgentId = member.DAgentId,
                TransactionType = TransactionType.CheckIn,
                BeforeAmount = member.CreditAmount, // 余额不变
                AfterAmount = member.CreditAmount,  // 余额不变
                BetAmount = 0,
                ActualAmount = 0,
                CurrencyCode = "CNY",
                SerialNumber = Guid.NewGuid().ToString("N"),
                GameRound = "",
                TransactionTime = TimeHelper.UtcUnix(),
                Status = TransactionStatus.Success,
                Description = $"会员完成每日签到，奖励 {pointBonus} 积分积分，已连续签到 {member.ContinuousCheckInDays} 天",
            };

            await uow.GetRepository<DMember>().InsertOrUpdateAsync(member);
            await uow.GetRepository<DTransAction>().InsertOrUpdateAsync(transAction);

            uow.Commit();

            // 触发每日签到任务进度更新
            _ = TryUpdateTaskProgressAsync(member.Id, "CheckIn");

            return ApiResult.Success.SetData(new {
                bonusPoints = pointBonus,
                continuousDays = member.ContinuousCheckInDays,
                newPoints = member.ActivityPoint
            }).SetMessage("签到成功");
        }
        catch (Exception ex)
        {
            uow.Rollback();
            _logger.LogError(ex, "会员 {MemberId} 签到失败", currentUserId);
            return ApiResult.Error.SetMessage($"签到出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取代理信息
    /// </summary>
    /// <param name="agentId">代理ID</param>
    [HttpGet($"@{nameof(GetAgentInfo)}")]
    [AllowAnonymous]
    public async Task<ApiResult> GetAgentInfo(long agentId = 0)
    {
        if (agentId == 0)
            return ApiResult.Error.SetMessage("代理ID为必填项");

        var agent = await _fsql.Select<DAgent>().Where(m => m.Id == agentId).ToOneAsync();

        if (agent == null)
            return ApiResult.Error.SetMessage("代理未找到");

        if (agent.IsEnabled == false)
            return ApiResult.Error.SetMessage("代理未启用");

        return ApiResult.Success.SetData(agent);
    }

    /// <summary>
    /// 按域名查代理
    /// </summary>
    /// <param name="domain">站点域名</param>
    [HttpGet($"@{nameof(GetAgentInfo2)}")]
    [AllowAnonymous]
    public async Task<ApiResult> GetAgentInfo2(string domain = "")
    {
        if (string.IsNullOrEmpty(domain))
            return ApiResult.Error.SetMessage("域名为必填项");

        var agent = await _fsql.Select<DAgent>().Where(m => m.HomeUrl.Contains(domain)).OrderByDescending(m => m.ModifiedTime).ToOneAsync();

        if (agent == null)
            return ApiResult.Error.SetMessage("代理未找到");

        if (agent.IsEnabled == false)
            return ApiResult.Error.SetMessage("代理未启用");

        return ApiResult.Success.SetData(agent);
    }

    /// <summary>
    /// 获取邀请中心
    /// </summary>
    [HttpGet($"@{nameof(GetInviteCenter)}")]
    public async Task<ApiResult> GetInviteCenter()
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
            return ApiResult.Error.SetCode(401).SetMessage("未登录或登录已过期");

        var member = await _fsql.Select<DMember>().Where(a => a.Id == userId).FirstAsync();
        if (member == null)
            return ApiResult.Error.SetCode(401).SetMessage("用户不存在");

        if (string.IsNullOrEmpty(member.InviteCode))
        {
            member.InviteCode = new Random().Next(1000, 10000).ToString();
            await _fsql.Update<DMember>().Set(a => a.InviteCode, member.InviteCode).Where(a => a.Id == member.Id).ExecuteAffrowsAsync();
        }

        var agentId = member.DAgentId;
        var agentRow = await _fsql.Select<DAgent>().Where(a => a.Id == agentId).ToOneAsync();
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var invitees = await _fsql.Select<DMember>()
            .Where(m => m.ParentId == userId && m.DAgentId == agentId)
            .OrderByDescending(m => m.CreatedTime)
            .Take(100)
            .ToListAsync();

        var records = new List<InviteRecordItemDto>();
        foreach (var m in invitees)
        {
            records.Add(new InviteRecordItemDto
            {
                DisplayName = MaskMemberDisplayName(m.Username, m.Nickname),
                RegisteredAt = m.CreatedTime ?? default,
            });
        }

        var totalInvites = await _fsql.Select<DMember>()
            .Where(m => m.ParentId == userId && m.DAgentId == agentId)
            .CountAsync();

        var todayInvites = await _fsql.Select<DMember>()
            .Where(m => m.ParentId == userId && m.DAgentId == agentId && m.CreatedTime >= today && m.CreatedTime < tomorrow)
            .CountAsync();

        var totalInviteTaskReward = await _fsql.Select<DTransAction>()
            .Where(a => a.DMemberId == userId
                && a.TransactionType == TransactionType.Activity
                && a.Description != null
                && a.Description.Contains("邀请好友"))
            .SumAsync(a => a.ActualAmount);

        var parentIds = await _fsql.Select<DMember>()
            .Where(m => m.ParentId > 0 && m.DAgentId == agentId)
            .ToListAsync(m => m.ParentId);

        var grouped = parentIds
            .GroupBy(id => id)
            .Select(g => (InviterId: g.Key, Cnt: g.Count()))
            .OrderByDescending(x => x.Cnt)
            .ToList();

        var myInviteCount = grouped.Where(x => x.InviterId == userId).Select(x => x.Cnt).FirstOrDefault();
        var myRank = 0;
        if (myInviteCount > 0)
            myRank = 1 + grouped.Count(x => x.Cnt > myInviteCount);

        var topRows = grouped.Take(20).ToList();
        var topIds = topRows.Select(x => x.InviterId).ToList();
        var inviterMembers = topIds.Count == 0
            ? new List<DMember>()
            : await _fsql.Select<DMember>().Where(m => topIds.Contains(m.Id)).ToListAsync();
        var inviterMap = inviterMembers.ToDictionary(m => m.Id);

        var leaderboard = new List<InviteLeaderboardItemDto>();
        var rank = 1;
        foreach (var row in topRows)
        {
            inviterMap.TryGetValue(row.InviterId, out var inv);
            leaderboard.Add(new InviteLeaderboardItemDto
            {
                Rank = rank++,
                DisplayName = inv == null ? "***" : MaskMemberDisplayName(inv.Username, inv.Nickname),
                InviteCount = row.Cnt,
                IsCurrentUser = row.InviterId == userId,
            });
        }

        var dto = new InviteCenterResponseDto
        {
            AgentId = agentId,
            AgentName = agentRow?.AgentName?.Trim() ?? "",
            InviteCode = member.InviteCode,
            TotalInvites = (int)totalInvites,
            TodayInvites = (int)todayInvites,
            TotalInviteTaskReward = totalInviteTaskReward,
            MyRank = myRank,
            MyInviteCount = myInviteCount,
            Records = records,
            Leaderboard = leaderboard,
        };

        return ApiResult.Success.SetData(dto);
    }

    /// <summary>
    /// 会员展示名脱敏（优先昵称）
    /// </summary>
    private static string MaskMemberDisplayName(string? username, string? nickname)
    {
        var raw = !string.IsNullOrWhiteSpace(nickname) ? nickname.Trim() : (username ?? "").Trim();
        if (raw.Length == 0) return "***";
        if (raw.Length <= 2) return raw[0] + "*";
        return raw.Substring(0, 2) + "***";
    }

    /// <summary>
    /// 初始化数据
    /// </summary>
    [HttpPost($"@{nameof(InitDbData)}")]
    [AllowAnonymous]
    public async Task<ApiResult> InitDbData()
    {
        var agentCount = await _fsql.Select<DAgent>().CountAsync();
        if (agentCount == 0)
        {
            using var uow = _fsql.CreateUnitOfWork();

            //初始化数据
            DAgent dAgent = new DAgent()
            {
                ParentId = 0,
                AgentType = AgentType.Direct,
                HomeUrl = "test.com", // 代理主页域名
                IsEnabled = true, // 启用状态
                GamePoints = 10000, // 初始游戏分
                UsdtAddress = "", // USDT地址，可后续配置
                TelegramChatId = "", // Telegram聊天ID，可后续配置
                CreditDiscount = 0.20m, // 默认分折扣 20%
                ServerIP = "", // 服务器IP，可后续配置
                LogoUrl = "", // Logo URL，可后续配置
                IPWhiteList = "", // IP白名单，默认为空
                RebateRate = 0.0080m, // 默认反水比例 0.8%
                Remark = "系统初始化默认代理", // 备注
                CreatedTime = DateTime.Now, // 创建时间
                ModifiedTime = DateTime.Now, // 修改时间
            };


            dAgent = await uow.GetRepository<DAgent>().InsertOrUpdateAsync(dAgent);

            DMember dMember = await uow.Orm.Select<DMember>().Where(m => m.Username == "admin").ToOneAsync();
            if (dMember != null)
            {
                dMember.DAgentId = dAgent.Id;
                await uow.GetRepository<DMember>().InsertOrUpdateAsync(dMember);
            }
            uow.Commit();

        }

        var taskCount = await _fsql.Select<J9_Admin.Entities.DTask>().CountAsync();
        if (taskCount == 0)
        {
            using var uow = _fsql.CreateUnitOfWork();
            var tasks = new List<J9_Admin.Entities.DTask>
            {
                new J9_Admin.Entities.DTask { Title = "每日登录", TaskType = "Login", TargetValue = 1, RewardAmount = 1m, ActivityPoint = 20, Description = "每天首次登录系统领取", Icon = "flame", JumpPath = "", IsEnabled = true, Sort = 1, CreatedTime = DateTime.Now },
                new J9_Admin.Entities.DTask { Title = "每日签到", TaskType = "CheckIn", TargetValue = 1, RewardAmount = 1m, ActivityPoint = 20, Description = "完成每日签到打卡", Icon = "calendar-check", JumpPath = "", IsEnabled = true, Sort = 2, CreatedTime = DateTime.Now },
                new J9_Admin.Entities.DTask { Title = "每日充值", TaskType = "Recharge", TargetValue = 100, RewardAmount = 5m, ActivityPoint = 30, Description = "每日累计充值金额大于100元", Icon = "coins", JumpPath = "/trans/recharge", IsEnabled = true, Sort = 3, CreatedTime = DateTime.Now },
                new J9_Admin.Entities.DTask { Title = "参与游戏", TaskType = "PlayGame", TargetValue = 5, RewardAmount = 2m, ActivityPoint = 30, Description = "每日累计参与5局游戏", Icon = "star", JumpPath = "/game/list", IsEnabled = true, Sort = 4, CreatedTime = DateTime.Now },
                new J9_Admin.Entities.DTask { Title = "邀请好友", TaskType = "Invite", TargetValue = 1, RewardAmount = 10m, ActivityPoint = 20, Description = "成功邀请1位好友注册", Icon = "star", JumpPath = "/user/invite", IsEnabled = true, Sort = 5, CreatedTime = DateTime.Now }
            };
            await uow.GetRepository<J9_Admin.Entities.DTask>().InsertAsync(tasks);
            uow.Commit();
        }

        return ApiResult.Success.SetData(true);
    }

    /// <summary>
    /// 获取租户信息
    /// </summary>
    [HttpGet($"@{nameof(GetTenantInfo)}")]
    [AllowAnonymous]
    public async Task<ApiResult> GetTenantInfo()
    {
        var titles = await _fsql.Select<BootstrapBlazor.Components.SysTenant>()
            .Where(t => t.IsEnabled)
            .ToListAsync(t => new
            {
                t.Id,
                t.Title,
                t.Host,
                t.Logo
            });

        return ApiResult.Success.SetData(titles);
    }
}
