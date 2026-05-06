using System.Globalization;
using FreeSql;
using FreeScheduler;
using Microsoft.AspNetCore.Mvc;
using J9_Admin.Utils;
using RestSharp;
using Newtonsoft.Json;
using J9_Admin.Services.PayApi;
using J9_Admin.Services;
using Microsoft.AspNetCore.Authorization;

namespace J9_Admin.API;

/// <summary>
/// 交易接口
/// </summary>
[ApiController]
[Route("api/trans")]
[Tags("交易处理")]
public class TransActionService : BaseService
{
    private readonly SessionAgent _sessionAgent;
    private readonly TGMessageApi _TGMessageApi;
    private readonly Pay0Api _pay0Api;
    private readonly PayPOPOApi _payPOPOApi;
    private readonly GameBetHistorySyncService _gameBetHistorySyncService;
    /// <summary>
    /// 交易服务构造
    /// </summary>
    /// <param name="freeSqlCloud">数据库服务</param>
    /// <param name="scheduler">任务调度器</param>
    /// <param name="logger">日志服务</param>
    /// <param name="adminContext">管理上下文</param>
    /// <param name="configuration">配置服务</param>
    /// <param name="sessionAgent">会话代理服务</param>
    /// <param name="TGMessageApi">Telegram消息服务</param>
    /// <param name="pay0Api">TokenPay支付服务</param>
    /// <param name="payPOPOApi">青蛙系统四方支付服务</param>
    public TransActionService(FreeSqlCloud freeSqlCloud, Scheduler scheduler, ILogger<TransActionService> logger, AdminContext adminContext, IConfiguration configuration, SessionAgent sessionAgent, TGMessageApi TGMessageApi, Pay0Api pay0Api, PayPOPOApi payPOPOApi, GameBetHistorySyncService gameBetHistorySyncService, IWebHostEnvironment webHostEnvironment)
        : base(freeSqlCloud, scheduler, logger, adminContext, configuration, webHostEnvironment)
    {
        _pay0Api = pay0Api ?? throw new ArgumentNullException(nameof(pay0Api));
        _payPOPOApi = payPOPOApi ?? throw new ArgumentNullException(nameof(payPOPOApi));
        _sessionAgent = sessionAgent ?? throw new ArgumentNullException(nameof(sessionAgent));
        _TGMessageApi = TGMessageApi ?? throw new ArgumentNullException(nameof(TGMessageApi));
        _gameBetHistorySyncService = gameBetHistorySyncService ?? throw new ArgumentNullException(nameof(gameBetHistorySyncService));
        _logger.LogInformation($"TransActionService initialized");
    }

    /// <summary>
    /// 获取月交易汇总
    /// </summary>
    [HttpGet($"@{nameof(GetTransActionMonthSummary)}")]
    public async Task<ApiResult> GetTransActionMonthSummary()
    {
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null) return ApiResult.Error.SetMessage("未登录或登录已过期");

        try
        {
            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0, DateTimeKind.Local);
            var monthStartUnix = TimeHelper.LocalToUnix(monthStart);
            var uid = currentUserId.Value;

            ISelect<DTransAction> MonthSuccessMemberBase() =>
                _fsql.Select<DTransAction>()
                    .Where(t => t.DMemberId == uid)
                    .Where(t => t.Status == TransactionStatus.Success)
                    .Where(t => t.TransactionTime >= monthStartUnix)
                    .Where(t => t.TransactionType != TransactionType.AgentTransferIn && t.TransactionType != TransactionType.AgentTransferOut);

            var rechargeQuery = MonthSuccessMemberBase().Where(t => t.TransactionType == TransactionType.Recharge);
            var withdrawQuery = MonthSuccessMemberBase().Where(t => t.TransactionType == TransactionType.Withdraw);

            var income = await rechargeQuery.SumAsync(t => t.ActualAmount);
            var expense = await withdrawQuery.SumAsync(t => t.ActualAmount);

            return ApiResult.Success.SetData(new { income, expense });
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "汇总会员 {UserId} 本月成功交易收支失败：{Error}", currentUserId, ex.Message);
            return ApiResult.Error.SetMessage($"Query month summary failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取交易记录
    /// </summary>
    /// <param name="transactionType">交易类型筛选</param>
    /// <param name="transactionStatus">交易状态筛选</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页条数</param>
    [HttpGet($"@{nameof(GetTransActionList)}")]
    public async Task<ApiResult> GetTransActionList(TransactionType? transactionType, TransactionStatus? transactionStatus, int page = 1, int pageSize = 500)
    {
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null) return ApiResult.Error.SetMessage("未登录或登录已过期");

        using var uow = _fsql.CreateUnitOfWork();

        try
        {
            // Query transaction records by member ID
            var transactionList = await uow.Orm.Select<DTransAction>()
                .Include(a => a.DGame)
                .Where(t => t.DMemberId == currentUserId)
                .WhereIf(transactionType != null, t => t.TransactionType == transactionType)
                .WhereIf(transactionStatus != null, t => t.Status == transactionStatus)
                .Where(t => t.TransactionType != TransactionType.AgentTransferIn && t.TransactionType != TransactionType.AgentTransferOut)
                .Where(t => t.TransactionTime >= TimeHelper.LocalToUnix(DateTime.Now.AddDays(-7)))
                .Page(page, pageSize)
                .OrderByDescending(x => x.TransactionTime)
                .ToListAsync(a => new
                {
                    a.Id,
                    a.DGame.ApiCode,
                    a.DAgentId,
                    a.TransactionType,
                    a.TransactionTime,
                    a.BeforeAmount,
                    a.AfterAmount,
                    a.BetAmount,
                    a.ActualAmount,
                    a.ValidBetAmount,
                    a.CurrencyCode,
                    a.SerialNumber,
                    a.BillNo,
                    a.PlayName,
                    a.GameRound,
                    a.Data,
                    a.Status,
                    a.Description,
                    a.RelatedTransActionId,
                    a.IsRebate,
                    a.CreatedTime,
                    a.ModifiedTime,
                    a.DMemberId
                });

            _logger.LogInformation("成功查询到 {Count} 条交易记录，用户ID：{UserId}", transactionList.Count, currentUserId);
            return ApiResult.Success.SetData(transactionList);
        }
        catch (Exception ex)
        {
            // 查询操作一般不需要回滚，但记录错误日志
            _logger.LogInformation(ex, "查询会员 {UserId} 交易记录失败，错误：{Error}", currentUserId, ex.Message);
            return ApiResult.Error.SetMessage($"Query transaction list failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 同步注单记录
    /// </summary>
    [HttpPost($"@{nameof(SyncBetHistoryToDatabaseAsync)}")]
    public async Task<ApiResult> SyncBetHistoryToDatabaseAsync()
    {
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null) return ApiResult.Error.SetMessage("未登录或登录已过期");

        try
        {
            var member = await _fsql.Select<DMember>()
                .Where(m => m.Id == currentUserId.Value)
                .ToOneAsync();

            if (member == null)
            {
                return ApiResult.Error.SetMessage("会员不存在");
            }

            if (string.IsNullOrWhiteSpace(member.Username))
            {
                return ApiResult.Error.SetMessage("会员账号不能为空，无法同步记录");
            }

            var syncUsername = member.Username.Trim();

            var outcome = await _gameBetHistorySyncService.SyncMsAndXhForUsernameAsync(syncUsername);
            var msResult = outcome.Ms;
            var xhResult = outcome.Xh;

            var summary = outcome.BuildSummaryLine();
            var data = new
            {
                targetMemberId = member.Id,
                targetUsername = syncUsername,
                currentUserId,
                ms = msResult,
                xh = xhResult
            };
            if (outcome.BothSuccess)
            {
                return ApiResult.Success.SetMessage("同步完成。" + summary).SetData(data);
            }

            return ApiResult.Error.SetMessage(summary).SetData(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步会员 {UserId} 注单记录失败（MS、XH 均为北京时间近6小时）", currentUserId);
            return ApiResult.Error.SetMessage($"Sync transaction history failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 会员提现
    /// </summary>
    /// <param name="Username">会员账号</param>
    /// <param name="amount">提现金额</param>
    /// <param name="withdrawPassword">提现密码</param>
    /// <param name="description">提现方式</param>
    [HttpPost($"@{nameof(PlayerWithdraw)}")]
    public async Task<ApiResult> PlayerWithdraw(string Username, decimal amount, string withdrawPassword, string description)
    {
        using var uow = _fsql.CreateUnitOfWork();

        try
        {
            var agentId = _sessionAgent.GetAgentId();
            if (agentId == 0)
            {
                return ApiResult.Error.SetMessage("代理未找到");
            }
            // 验证会员和代理
            var (errorResult, member, agent) = await ValidateMemberAndAgentAsync(Username);
            if (errorResult != null)
            {
                return errorResult;
            }

            if (member.WithdrawPassword != withdrawPassword)
            {
                return ApiResult.Error.SetMessage("提款密码错误");
            }

            if (amount <= 0)
            {
                return ApiResult.Error.SetMessage("提现金额必须大于 0");
            }

            // 检查余额是否足够
            if (member.CreditAmount < amount)
            {
                // 业务逻辑验证失败，不需要回滚
                return ApiResult.Error.SetMessage("余额不足");
            }

            // 创建代理交易记录
            // 找到第一个 属于agentId的会员信息，默认为管理员，赋值给 交易记录
            var agentMember = await uow.Orm.Select<DMember>().Where(m => m.DAgentId == agent.Id && m.IsSystem == false).OrderBy(a => a.Id).ToOneAsync();
            var transAction2 = new DTransAction()
            {
                DMemberId = agentMember.Id,
                DAgentId = agent.Id,
                BeforeAmount = 0,
                AfterAmount = amount,
                BetAmount = 0,
                ActualAmount = amount,
                CurrencyCode = "CNY",
                SerialNumber = Guid.NewGuid().ToString("N"),
                GameRound = "",
                TransactionTime = TimeHelper.UtcUnix(),
                TransactionType = TransactionType.AgentTransferIn,
                Status = TransactionStatus.Processing,
                Description = $"代理 {agent.Id} 下分 {amount} 元 {description},代理实际下分收入 {amount} 元",
                RelatedTransActionId = 0, // 关联会员交易记录,会员交易状态改变时,代理交易状态也会改变
            };
            var transActionResult2 = await uow.GetRepository<DTransAction>().InsertOrUpdateAsync(transAction2);

            // 创建交易记录
            var transAction = new DTransAction()
            {
                DMemberId = member.Id,
                DAgentId = agent.Id,
                TransactionType = TransactionType.Withdraw,
                BeforeAmount = member.CreditAmount,
                AfterAmount = member.CreditAmount - amount,
                BetAmount = 0,
                ActualAmount = amount,
                CurrencyCode = "CNY",
                SerialNumber = Guid.NewGuid().ToString("N"),
                GameRound = "",
                TransactionTime = TimeHelper.UtcUnix(),
                Status = TransactionStatus.Processing,
                Description = $"会员 {Username} 通过{description}提现，金额 {amount} 元",
                RelatedTransActionId = transActionResult2.Id, // 关联会员交易记录,会员交易状态改变时,代理交易状态也会改变
            };
            // 插入交易记录
            var transActionResult = await uow.GetRepository<DTransAction>().InsertOrUpdateAsync(transAction);

            _logger.LogInformation("会员 {Username} 提现成功，金额：{Amount}，订单号：{OrderId}",
                Username, amount, transActionResult.Id);

            // 更新会员余额
            member.CreditAmount -= amount;
            await uow.GetRepository<DMember>().InsertOrUpdateAsync(member);

            // 提交事务
            uow.Commit();

            // 发送消息
            await _TGMessageApi.SendMessageAsync(agent.TelegramChatId, $@"您有一笔新的提现订单：
会员: {Username} 
提现: {amount} 元
会员余额: {member.CreditAmount} 元
订单号: {transActionResult.Id}
状态: {transActionResult.Status}
详情: {transActionResult.Description}
下分完成后，请尽快标记为成功。

======================
代理用户: {agent.HomeUrl}"
);

            return ApiResult.Success.SetData(new
            {
                Message = "Withdraw successful",
                Balance = member.CreditAmount,
                TransactionId = transActionResult.Id
            });
        }
        catch (Exception ex)
        {
            // 回滚事务
            uow.Rollback();

            _logger.LogInformation(ex, "会员 {Username} 提现失败，金额：{Amount}，错误：{Error}",
                Username, amount, ex.Message);
            return ApiResult.Error.SetMessage($"Withdraw failed: {ex.Message}");
        }
    }


    /// <summary>
    /// 领取返水
    /// </summary>
    /// <param name="Username">会员账号</param>
    [HttpPost($"@{nameof(PlayerRebate)}")]
    public async Task<ApiResult> PlayerRebate(string Username)
    {
        using var uow = _fsql.CreateUnitOfWork();

        try
        {
            var agentId = _sessionAgent.GetAgentId();
            if (agentId == 0)
            {
                return ApiResult.Error.SetMessage("代理未找到");
            }
            // 验证会员和代理
            var (errorResult, member, agent) = await ValidateMemberAndAgentAsync(Username);
            if (errorResult != null)
            {
                // 验证失败，不需要回滚，直接返回
                return errorResult;
            }

            // 检查会员是否开启反水
            if (!member.IsRebateSwitch)
            {
                // 业务逻辑验证失败，不需要回滚
                return ApiResult.Error.SetMessage("会员反水开关已关闭");
            }

            // 查找该会员的为被反水的交易记录
            var transActionList = await uow.Orm.Select<DTransAction>()
                    .Where(a => a.DMemberId == member.Id &&
                            a.TransactionType == TransactionType.Bet &&
                            a.Status == TransactionStatus.Success &&
                            a.IsRebate == false)
                    .ToListAsync();

            // 按有效投注额计算会员返水
            var rebateAmount = transActionList.Sum(a => a.ValidBetAmount * agent.RebateRate);

            if (rebateAmount <= 0)
            {
                return ApiResult.Error.SetMessage("反水金额必须大于 0，暂无可反水记录");
            }

            // 创建反水交易记录
            var rebateTransAction = new DTransAction()
            {
                DMemberId = member.Id,
                DAgentId = agent.Id,
                TransactionType = TransactionType.Rebate,
                BeforeAmount = member.CreditAmount,
                AfterAmount = member.CreditAmount + rebateAmount,
                BetAmount = 0,
                ActualAmount = rebateAmount,
                CurrencyCode = "CNY",
                SerialNumber = Guid.NewGuid().ToString("N"),
                GameRound = "",
                TransactionTime = TimeHelper.UtcUnix(),
                Status = TransactionStatus.Success,
                Description = $"会员 {Username} 申请反水",
            };

            // 插入反水交易记录
            await uow.GetRepository<DTransAction>().InsertOrUpdateAsync(rebateTransAction);

            // 更新会员余额
            member.CreditAmount += rebateAmount;
            await uow.GetRepository<DMember>().InsertOrUpdateAsync(member);

            // 标记反水交易记录已反水
            foreach (var transAction in transActionList)
            {
                transAction.IsRebate = true;
                await uow.GetRepository<DTransAction>().InsertOrUpdateAsync(transAction);
            }

            // 提交事务
            uow.Commit();

            // 发送消息
            await _TGMessageApi.SendMessageAsync(agent.TelegramChatId, $@"您有一笔新的反水订单：
会员: {Username} 
反水: {rebateAmount} 元
会员余额: {member.CreditAmount} 元
订单号: {rebateTransAction.Id}
状态: {rebateTransAction.Status}

======================
代理用户: {agent.HomeUrl}"
);

            _logger.LogInformation("会员 {Username} 反水成功，金额：{Amount}", Username, rebateAmount);

            return ApiResult.Success.SetData(member);
        }
        catch (Exception ex)
        {
            // 回滚事务
            uow.Rollback();

            _logger.LogInformation(ex, "会员 {Username} 反水失败，错误：{Error}", Username, ex.Message);
            return ApiResult.Error.SetMessage($"Rebate failed: {ex.Message}");
        }
    }


    /// <summary>
    /// 充值赠送额：统一按充值金额的 3% 赠送
    /// </summary>
    private static decimal GetRechargeGiftAmount(decimal amount)
    {
        if (amount <= 0m) return 0m;
        return Math.Round(amount * 0.03m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 创建充值订单
    /// </summary>
    /// <param name="amountRaw">充值金额</param>
    /// <param name="payApiId">支付通道ID</param>
    /// <returns>订单ID</returns>
    [HttpPost($"@{nameof(CreateMemberRechargeOrder)}")]
    public async Task<ApiResult> CreateMemberRechargeOrder(
        [FromQuery(Name = "amount")] string? amountRaw,
        [FromQuery(Name = "payApiId")] long? payApiId)
    {
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId == null) return ApiResult.Error.SetMessage("未登录或登录已过期");

        if (string.IsNullOrWhiteSpace(amountRaw)
            || !decimal.TryParse(amountRaw.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return ApiResult.Error.SetMessage("充值金额无效");
        }

        if (amount <= 0)
        {
            return ApiResult.Error.SetMessage("交易金额必须大于 0");
        }

        if (amount < 2)
        {
            return ApiResult.Error.SetMessage("充值金额不能小于 2 元");
        }

        using var uow = _fsql.CreateUnitOfWork();
        try
        {
            if (payApiId == null || payApiId <= 0)
            {
                return ApiResult.Error.SetMessage("请选择支付通道");
            }

            var payApi = await uow.Orm.Select<DPayApi>()
                .Where(p => p.Id == payApiId.Value)
                .ToOneAsync();
            if (payApi == null || !payApi.IsEnabled)
            {
                return ApiResult.Error.SetMessage("支付通道不可用");
            }

            if (amount < payApi.MinAmount)
            {
                return ApiResult.Error.SetMessage($"当前支付通道最低充值 {payApi.MinAmount} 元");
            }

            if (amount > payApi.MaxAmount)
            {
                return ApiResult.Error.SetMessage($"当前支付通道最高充值 {payApi.MaxAmount} 元");
            }

            if (!payApi.IsUserInput)
            {
                var allowedAmounts = (payApi.DefaultValue ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(item => decimal.TryParse(item, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : (decimal?)null)
                    .Where(value => value != null)
                    .Select(value => value!.Value)
                    .ToHashSet();
                if (!allowedAmounts.Contains(amount))
                {
                    return ApiResult.Error.SetMessage("当前支付通道仅支持选择固定金额");
                }
            }

            var member = await uow.Orm.Select<DMember>().Include(m => m.DAgent).Where(m => m.Id == currentUserId).ToOneAsync();
            if (member == null) return ApiResult.Error.SetMessage("会员未找到");
            if (member.DAgentId == 0 || member.DAgent == null)
            {
                var defaultAgent = await uow.Orm.Select<DAgent>().OrderBy(a => a.Id).ToOneAsync();
                if (defaultAgent == null)
                    return ApiResult.Error.SetMessage("系统中暂无代理，无法充值");
                member.DAgentId = defaultAgent.Id;
                await uow.GetRepository<DMember>().InsertOrUpdateAsync(member);
                member.DAgent = defaultAgent;
                _logger.LogInformation("会员 {MemberId} 未绑定代理，已自动绑定至默认代理 {AgentId}", member.Id, defaultAgent.Id);
            }

            var usdtAddress = _configuration["Payment:UsdtAddress"]?.Trim();
            if (string.IsNullOrWhiteSpace(usdtAddress)) return ApiResult.Error.SetMessage("系统未配置 USDT 收款地址，暂无法充值");

            // 清理 10 天前的待支付充值订单
            var oldPending = await uow.Orm.Select<DTransAction>()
                .Where(m => m.DMemberId == member.Id && m.TransactionType == TransactionType.Recharge && m.Status == TransactionStatus.Pending)
                .Where(m => m.CreatedTime < DateTime.Now.AddDays(-10))
                .ToListAsync();
            foreach (var t in oldPending)
            {
                await uow.GetRepository<DTransAction>().DeleteAsync(t);
            }

            var giftAmount = GetRechargeGiftAmount(amount);
            var totalCredit = amount + giftAmount;
            var transAction = new DTransAction
            {
                DMemberId = member.Id,
                DAgentId = member.DAgentId,
                BeforeAmount = member.CreditAmount,
                AfterAmount = member.CreditAmount + totalCredit,
                BetAmount = giftAmount, // 充值赠送金额：统一按 3% 赠送
                ActualAmount = amount,
                CurrencyCode = "CNY",
                SerialNumber = Guid.NewGuid().ToString("N"),
                GameRound = "",
                TransactionTime = TimeHelper.UtcUnix(),
                TransactionType = TransactionType.Recharge,
                Status = TransactionStatus.Pending,
                Description = giftAmount > 0 ? $"会员自助充值 {amount} 元，赠送 {giftAmount} 元" : $"会员自助充值 {amount} 元",
                RelatedTransActionId = 0,
                PayApiId = payApi.Id,
            };
            var result = await uow.GetRepository<DTransAction>().InsertOrUpdateAsync(transAction);
            uow.Commit();

            _logger.LogInformation("会员 {MemberId} 创建充值订单成功，金额：{Amount}，赠送：{Gift}，订单号：{OrderId}，支付通道：{PayApiId}", member.Id, amount, giftAmount, result.Id, payApi.Id);

            // 下单成功后，给代理发送 Telegram 通知
            if (!string.IsNullOrWhiteSpace(member.DAgent?.TelegramChatId))
            {
                var giftText = giftAmount > 0 ? $"\n赠送: {giftAmount} 元\n到账: {amount + giftAmount} 元" : "";
                await _TGMessageApi.SendMessageAsync(member.DAgent.TelegramChatId, $@"会员提交了新的充值订单：
会员: {member.Username}
充值: {amount} 元{giftText}
会员当前余额: {member.CreditAmount} 元
订单号: {result.Id}
状态: {result.Status}
下单时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
请留意到账情况。

======================
代理用户: {member.DAgent.HomeUrl}");
            }

            return ApiResult.Success.SetData(new { TransactionId = result.Id, Message = "订单创建成功" });
        }
        catch (Exception ex)
        {
            uow.Rollback();
            _logger.LogError(ex, "会员 {MemberId} 创建充值订单失败", currentUserId);
            return ApiResult.Error.SetMessage($"创建订单失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建支付订单
    /// </summary>
    /// <param name="orderId">订单号</param>
    [HttpGet($"@{nameof(CreatePay0Order)}")]
    public async Task<ApiResult> CreatePay0Order(string orderId)
    {
        try
        {
            _logger.LogInformation("开始创建TokenPay订单，订单号：{OrderId}", orderId);

            // 1. 验证订单是否存在
            var (error, result) = await GetOrderOrErrorAsync(orderId);
            if (error != null) return error;

            // 2. 会员调用时校验订单归属
            var currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId != null && result.DMemberId != currentUserId)
            {
                return ApiResult.Error.SetMessage("无权操作此订单");
            }

            // 3. 构建返回URL
            var returnUrl = GetDefaultReturnUrl();

            // 4. 调用TokenPay创建订单
            _logger.LogInformation("调用TokenPay创建订单，订单号：{OrderId}，金额：{Amount}", orderId, result.ActualAmount);
            return await _pay0Api.CreateOrder(HttpContext, orderId, result.ActualAmount, returnUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建TokenPay订单时发生异常，订单号：{OrderId}", orderId);
            return ApiResult.Error.SetMessage($"创建订单失败：{ex.Message}");
        }

        async Task<(ApiResult? error, DTransAction? order)> GetOrderOrErrorAsync(string orderId)
        {
            if (!long.TryParse(orderId, out var id))
            {
                _logger.LogWarning("订单号格式不正确：{OrderId}", orderId);
                return (ApiResult.Error.SetMessage("订单号格式不正确"), null);
            }
            var order = await _fsql.Select<DTransAction>().Where(m => m.Id == id).ToOneAsync();
            if (order == null)
            {
                _logger.LogWarning("订单不存在，订单号：{OrderId}", orderId);
                return (ApiResult.Error.SetMessage("订单不存在"), null);
            }
            return (null, order);
        }

        string GetDefaultReturnUrl()
        {
            var origins = _configuration.GetSection("AllowedOrigins").Get<string[]>();
            return origins != null && origins.Length > 0 ? origins[0] : string.Empty;
        }
    }

    /// <summary>
    /// 处理支付回调
    /// </summary>
    /// <returns>回调处理结果</returns>
    [HttpPost($"@{nameof(Pay0Callback)}")]
    [AllowAnonymous]
    public async Task<string> Pay0Callback()
    {
        try
        {
            _logger.LogInformation("收到TokenPay支付回调通知");
            return await _pay0Api.HandlePayCallback(HttpContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理TokenPay支付回调时发生异常");
            return "error";
        }
    }

    /// <summary>
    /// 创建POPO支付订单
    /// </summary>
    /// <param name="orderId">订单号</param>
    [HttpGet($"@{nameof(CreatePayPOPOOrder)}")]
    public async Task<ApiResult> CreatePayPOPOOrder(string orderId)
    {
        try
        {
            _logger.LogInformation("开始创建POPO订单，订单号：{OrderId}", orderId);

            // 1. 验证订单是否存在
            var (error, result) = await GetOrderOrErrorAsync(orderId);
            if (error != null) return error;

            // 2. 会员调用时校验订单归属
            var currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId != null && result.DMemberId != currentUserId)
            {
                return ApiResult.Error.SetMessage("无权操作此订单");
            }

            // 3. 构建返回URL
            var returnUrl = GetDefaultReturnUrl();

            // 4. 调用POPO创建订单
            _logger.LogInformation($"调用POPO创建订单，订单号：{orderId}，金额：{result.ActualAmount}，返回地址：{returnUrl}");
            return await _payPOPOApi.CreateOrder(HttpContext, orderId, result.ActualAmount, returnUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建POPO订单时发生异常，订单号：{OrderId}", orderId);
            return ApiResult.Error.SetMessage($"创建订单失败：{ex.Message}");
        }

        async Task<(ApiResult? error, DTransAction? order)> GetOrderOrErrorAsync(string orderId)
        {
            if (!long.TryParse(orderId, out var id))
            {
                _logger.LogWarning("订单号格式不正确：{OrderId}", orderId);
                return (ApiResult.Error.SetMessage("订单号格式不正确"), null);
            }
            var order = await _fsql.Select<DTransAction>().Where(m => m.Id == id).ToOneAsync();
            if (order == null)
            {
                _logger.LogWarning("订单不存在，订单号：{OrderId}", orderId);
                return (ApiResult.Error.SetMessage("订单不存在"), null);
            }
            return (null, order);
        }

        string GetDefaultReturnUrl()
        {
            var origins = _configuration.GetSection("AllowedOrigins").Get<string[]>();
            return origins != null && origins.Length > 0 ? origins[0] : string.Empty;
        }
    }

    /// <summary>
    /// 处理POPO支付回调
    /// </summary>
    /// <returns>回调处理结果</returns>
    [HttpPost($"@{nameof(PayPOPOCallback)}")]
    [AllowAnonymous]
    public async Task<string> PayPOPOCallback()
    {
        try
        {
            _logger.LogInformation("收到POPO支付回调通知");
            return await _payPOPOApi.HandlePayCallback(HttpContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理POPO支付回调时发生异常");
            return "error";
        }
    }

    /// <summary>
    /// 获取支付通道
    /// </summary>
    [HttpGet($"@{nameof(GetPayApiList)}")]
    public async Task<ApiResult> GetPayApiList()
    {
        var payApiList = await _fsql.Select<DPayApi>()
            .Where(m => m.IsEnabled)
            .OrderBy(m => m.Sort)
            .OrderBy(m => m.PayMethod)
            .ToListAsync();

        return ApiResult.Success.SetData(payApiList);
    }


    /// <summary>
    /// 获取玩家动态
    /// </summary>
    /// <param name="count">返回条数</param>
    /// <param name="type">交易类型筛选</param>
    [HttpGet($"@{nameof(GetRecentPlayerActivity)}")]
    [AllowAnonymous]
    public async Task<ApiResult> GetRecentPlayerActivity(int count = 20, TransactionType? type = null)
    {
        try
        {
            if (count <= 0) count = 20;
            if (count > 100) count = 100;

            var activities = await _fsql.Select<DTransAction>()
                .Include(t => t.DMember)
                .Include(t => t.DGame)
                .Where(t => t.Status == TransactionStatus.Success)
                .WhereIf(type != null, t => t.TransactionType == type)
                .WhereIf(type == null, t => t.TransactionType == TransactionType.Bet
                          || t.TransactionType == TransactionType.Recharge
                          || t.TransactionType == TransactionType.Login
                          || t.TransactionType == TransactionType.CheckIn
                          || t.TransactionType == TransactionType.Register)
                .OrderByDescending(t => t.TransactionTime)
                .Take(count)
                .ToListAsync();

            var result = activities.Select(t => new
            {
                // 玩家名称（脱敏处理）
                MemberName = MaskPhoneNumber(t.DMember?.Username),
                MemberAvatar = t.DMember?.Avatar ?? "",
                // 交易类型
                TransactionType = t.TransactionType.ToString(),
                TransactionTypeValue = (int)t.TransactionType,
                // 游戏相关
                GameName = t.DGame?.GameCnName ?? t.DGame?.GameName ?? "",
                GameIcon = t.DGame?.Icon ?? "",
                // 金额
                ActualAmount = t.ActualAmount,
                BetAmount = t.BetAmount,
                // 时间
                TransactionTime = t.TransactionTime,
                // 生成前端友好的描述
                Description = GenerateActivityDescription(t.TransactionType, t.DGame?.GameCnName ?? t.DGame?.GameName ?? "", t.ActualAmount),
            }).ToList();

            _logger.LogInformation("获取玩家动态成功，返回 {Count} 条记录", result.Count);
            return ApiResult.Success.SetData(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取玩家动态失败");
            return ApiResult.Error.SetMessage($"获取玩家动态失败: {ex.Message}");
        }

        string MaskPhoneNumber(string? phone)
        {
            if (string.IsNullOrEmpty(phone))
                return "玩家***";
            if (phone.Length >= 11)
                return phone.Substring(0, 3) + "****" + phone.Substring(phone.Length - 4);
            if (phone.Length >= 4)
                return phone.Substring(0, 1) + "***" + phone.Substring(phone.Length - 1);
            return phone.Substring(0, 1) + "***";
        }

        string GenerateActivityDescription(TransactionType type, string gameName, decimal amount)
        {
            return type switch
            {
                TransactionType.Bet when amount > 0 => $"在「{gameName}」赢得 {amount:F2} 元，手气不错！",
                TransactionType.Bet when amount < 0 => $"在「{gameName}」投注 {Math.Abs(amount):F2} 元，期待下次好运。",
                TransactionType.Bet => $"在「{gameName}」完成了一局精彩对决。",
                TransactionType.Recharge => $"成功充值 {amount:F2} 元，余额已更新。",
                TransactionType.Login => "登录平台，开始新一天的精彩体验。",
                TransactionType.CheckIn => "完成每日签到，领取签到奖励！",
                TransactionType.Register => "新玩家加入平台，开启精彩旅程！",
                _ => $"完成了一笔 {amount:F2} 元的交易。",
            };
        }

    }


}
