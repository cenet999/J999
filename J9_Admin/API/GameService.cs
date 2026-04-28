using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AdminBlazor.Infrastructure.Encrypt;
using BootstrapBlazor.Components;
using FreeScheduler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using J9_Admin.Utils;
using RestSharp;
using J9_Admin.Services.GameApi;
using Newtonsoft.Json;

namespace J9_Admin.API;


/// <summary>
/// 游戏接口
/// </summary>
[ApiController]
[Route("api/game")]
[Tags("游戏")]
public class GameService : BaseService
{
    private static readonly string[] MsGamePlatformNames = ["MS", "MS游戏", "美盛游戏"];
    private static readonly string[] XhGamePlatformNames = ["XH", "XH游戏", "星汇游戏"];

    private readonly MSGameApi _msGameApi;
    private readonly XHGameApi _xhGameApi;
    private readonly PerMemberAsyncGate _perMemberGate;

    public GameService(
        FreeSqlCloud freeSqlCloud,
        Scheduler scheduler,
        ILogger<GameService> logger,
        AdminContext adminContext,
        IConfiguration configuration,
        MSGameApi msGameApi,
        XHGameApi xhGameApi,
        IWebHostEnvironment webHostEnvironment,
        PerMemberAsyncGate perMemberGate)
        : base(freeSqlCloud, scheduler, logger, adminContext, configuration, webHostEnvironment)
    {
        _msGameApi = msGameApi;
        _xhGameApi = xhGameApi;
        _perMemberGate = perMemberGate;
    }

    private async Task<List<long>> ResolveGamePlatformIdsAsync(params string[] platformNames)
    {
        var names = platformNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0)
        {
            return [];
        }

        return await _fsql.Select<DGamePlatform>()
            .Where(platform => names.Contains(platform.Name))
            .ToListAsync(platform => platform.Id);
    }

    private async Task<DGame?> FindGameByIdAsync(long gameId, params string[] platformNames)
    {
        var platformIds = await ResolveGamePlatformIdsAsync(platformNames);
        var query = _fsql.Select<DGame>();
        if (platformIds.Count > 0)
        {
            query = query.Where(game => platformIds.Contains(game.DGamePlatformId));
        }

        return await query.Where(g => g.Id == gameId).ToOneAsync();
    }

    private async Task<(List<string> ApiCodes, int DistinctTotal)> GetRecentTransferApiCodesAsync(long memberId, int maxApiCodes, params string[] platformNames)
    {
        var sinceUnix = TimeHelper.LocalToUnix(DateTime.Now.AddDays(-2));
        var platformIds = await ResolveGamePlatformIdsAsync(platformNames);

        List<long>? gameIds = null;
        if (platformIds.Count > 0)
        {
            gameIds = await _fsql.Select<DGame>()
                .Where(game => platformIds.Contains(game.DGamePlatformId))
                .ToListAsync(game => game.Id);

            if (gameIds.Count == 0)
            {
                return ([], 0);
            }
        }

        var transQuery = _fsql.Select<DTransAction>()
            .Include(t => t.DGame)
            .Where(t => t.DMemberId == memberId
                && t.TransactionTime >= sinceUnix
                && t.TransactionType == TransactionType.TransferIn
                && t.Status == TransactionStatus.Success
                && t.DGameId > 0);

        if (gameIds != null)
        {
            transQuery = transQuery.Where(t => gameIds.Contains(t.DGameId));
        }

        var transRows = await transQuery
            .OrderByDescending(t => t.TransactionTime)
            .ToListAsync(t => new
            {
                t.DGameId,
                t.TransactionTime,
                ApiCode = t.DGame != null ? t.DGame.ApiCode : null
            });

        var apiCodesNewestFirst = new List<string>();
        var seenApiCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in transRows)
        {
            var code = row.ApiCode?.Trim();
            if (string.IsNullOrEmpty(code) || !seenApiCodes.Add(code))
            {
                continue;
            }

            apiCodesNewestFirst.Add(code);
        }

        var distinctTotal = apiCodesNewestFirst.Count;
        return (apiCodesNewestFirst.Take(maxApiCodes).ToList(), distinctTotal);
    }

    /// <summary>
    /// 游戏列表
    /// </summary>
    /// <returns>游戏列表</returns>
    [HttpGet($"@{nameof(GetGameList)}")]
    [AllowAnonymous]
    public async Task<ApiResult> GetGameList(string keyword = "", int type = 0, int page = 1, int limit = 20, string sort = "", string apiCode = "")
    {
        try
        {
            var isHotRequest = string.IsNullOrWhiteSpace(keyword)
                && type <= 0
                && page == 1
                && string.IsNullOrWhiteSpace(sort)
                && string.IsNullOrWhiteSpace(apiCode);

            _logger.LogInformation(
                "获取游戏列表 - keyword: {Keyword}, type: {Type}, page: {Page}, limit: {Limit}, sort: {Sort}, apiCode: {ApiCode}",
                keyword, type, page, limit, sort, apiCode);

            var query = _fsql.Select<DGame>().Include(a => a.DGamePlatform)
                .Where(d => d.IsEnabled)
                .WhereIf(!string.IsNullOrEmpty(keyword), d => d.GameCnName.Contains(keyword) || d.GameName.Contains(keyword))
                .WhereIf(type > 0, d => d.GameType == (GameType)type)
                .WhereIf(!string.IsNullOrEmpty(apiCode), d => d.ApiCode == apiCode)
                .WhereIf(isHotRequest, d => d.GameType != GameType.Live && d.GameType != GameType.Sports && d.GameType != GameType.Other)
                .OrderByDescending(d => d.IsRecommended)
                .OrderByDescending(d => d.ClickCount)
                .OrderByDescending(d => d.PlayerCount);

            var gameList = await query.Page(page, limit).ToListAsync();
            _logger.LogInformation("获取游戏列表成功，返回 {Count} 条", gameList.Count);
            return ApiResult.Success.SetData(gameList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏列表时发生异常");
            return ApiResult.Error.SetMessage("获取游戏列表失败，请稍后重试");
        }
    }


    /// <summary>
    /// 启动 MS 局
    /// </summary>
    /// <param name="player_id">玩家ID</param>
    /// <param name="gameId">游戏ID</param>
    /// <returns>包含游戏URL的API结果</returns>
    [HttpGet($"@{nameof(StartMSGame)}")]
    public async Task<ApiResult> StartMSGame(string player_id, string gameId)
    {
        try
        {
            // 1. 验证玩家ID
            if (!long.TryParse(player_id, out long memberId))
            {
                _logger.LogWarning("启动MS游戏失败：玩家ID格式不正确 - player_id: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("玩家ID格式不正确").SetData(new { });
            }

            // 同一玩家不能并发或连续重入启动流程（排队执行）
            using (await _perMemberGate.LockAsync(memberId))
            {
                var coreResult = await StartMSGameCoreAsync(player_id, gameId, memberId, adminTestLaunch: false);
                return coreResult;
            }
        }
        catch (Exception ex)
        {
            // 记录详细错误日志
            _logger.LogError(ex, "启动MS游戏时发生异常，玩家ID: {PlayerId}, 游戏ID: {GameId}", player_id, gameId);
            return ApiResult.Error.SetMessage("启动游戏时发生系统错误，请稍后重试").SetData<object>(null);
        }
    }

    /// <summary>
    /// 后台游戏管理「测试启动」专用：不根据主钱包/游戏钱包做上分；游戏侧余额查询失败时仍尝试返回启动链接；允许已禁用游戏取链（不入 HTTP 路由）。
    /// </summary>
    [NonAction]
    public async Task<ApiResult> StartMSGameAdminTestAsync(string player_id, string gameId)
    {
        try
        {
            if (!long.TryParse(player_id, out long memberId))
            {
                _logger.LogWarning("后台测试启动MS游戏失败：玩家ID格式不正确 - player_id: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("玩家ID格式不正确").SetData(new { });
            }

            using (await _perMemberGate.LockAsync(memberId))
            {
                return await StartMSGameCoreAsync(player_id, gameId, memberId, adminTestLaunch: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "后台测试启动MS游戏时发生异常，玩家ID: {PlayerId}, 游戏ID: {GameId}", player_id, gameId);
            return ApiResult.Error.SetMessage("启动游戏时发生系统错误，请稍后重试").SetData<object>(null);
        }
    }

    /// <summary>
    /// 锁内启动
    /// </summary>
    private async Task<ApiResult> StartMSGameCoreAsync(string player_id, string gameId, long memberId, bool adminTestLaunch = false)
    {
        var member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
        if (member == null)
        {
            _logger.LogWarning("启动MS游戏失败：会员不存在 - 玩家ID: {PlayerId}", player_id);
            return ApiResult.Error.SetMessage("会员不存在").SetData(new { });
        }

        if (member.CreditAmount <= 0)
        {
            _logger.LogInformation("启动MS游戏 - 会员余额为0或负数，跳过上分 - 玩家ID: {PlayerId}, 用户名: {Username}", player_id, member.Username);
            // return ApiResult.Error.SetMessage("会员余额不足").SetData<object>(null);
        }

        if (!long.TryParse(gameId, out var parsedGameId))
        {
            _logger.LogWarning("启动MS游戏失败：游戏ID格式不正确 - 玩家ID: {PlayerId}, gameId: {GameId}", player_id, gameId);
            return ApiResult.Error.SetMessage("游戏ID格式不正确").SetData(new { });
        }

        // 2. 检查游戏是否存在（统一按主键 Id 启动）
        var game = await FindGameByIdAsync(parsedGameId, MsGamePlatformNames);
        if (game == null)
        {
            _logger.LogWarning("启动MS游戏失败：游戏不存在 - 玩家ID: {PlayerId}, gameId: {GameId}", player_id, gameId);
            return ApiResult.Error.SetMessage($"游戏不存在，请检查 gameId 参数（当前: {gameId}）。").SetData(new { });
        }
        if (game.IsEnabled == false && !adminTestLaunch)
        {
            _logger.LogWarning("启动MS游戏失败：游戏已禁用 - 玩家ID: {PlayerId}, gameId: {GameId}", player_id, game.Id);
            return ApiResult.Error.SetMessage("游戏已禁用").SetData(new { });
        }

        await _fsql.Update<DGame>()
            .Set(g => g.ClickCount + 1)
            .Where(g => g.Id == game.Id)
            .ExecuteAffrowsAsync();
        game.ClickCount += 1;

        _logger.LogInformation(
            "{LaunchMode}MS游戏 - 玩家ID: {PlayerId}, 用户名: {Username}, gameId: {GameId}, GameCode: {GameCode}, ApiCode: {ApiCode}, 余额: {Credit}, 点击次数: {ClickCount}",
            adminTestLaunch ? "后台测试启动" : "启动", player_id, member.Username, game.Id, game.GameCode, game.ApiCode, member.CreditAmount, game.ClickCount);

        // 3. 创建游戏账户 - 确保用户已注册
        try
        {
            await _msGameApi.UserRegister(member.Username, game.ApiCode);
        }
        catch (Exception regEx)
        {
            _logger.LogError(regEx, "MS游戏用户注册失败 - 玩家ID: {PlayerId}, 游戏ID: {GameId}", player_id, game.Id);
            return ApiResult.Error.SetMessage("游戏服务器连接失败，请稍后重试").SetData(new { });
        }

        // 4. 先拿游戏地址，地址拿不到就不要上分，避免“钱已转走但用户没进到游戏”
        string gameUrl;
        try
        {
            gameUrl = await _msGameApi.GetGameUrl(member.Username, game.GameCode, game.ApiCode, game.GameType);
        }
        catch (Exception urlEx)
        {
            _logger.LogError(urlEx, "获取MS游戏URL失败 - 玩家ID: {PlayerId}, 游戏ID: {GameId}", player_id, game.Id);
            return ApiResult.Error.SetMessage("获取游戏地址失败，请稍后重试").SetData<object>(null);
        }

        _logger.LogInformation(
            "已获取MS游戏URL - 玩家ID: {PlayerId}, GameCode: {ResolvedGameCode}, ApiCode: {ApiCode}, GameType: {GameType}",
            player_id, game.GameCode, game.ApiCode, game.GameType);
        if (string.IsNullOrEmpty(gameUrl))
        {
            _logger.LogWarning("获取MS游戏URL为空 - 玩家ID: {PlayerId}, gameId: {GameId}", player_id, game.Id);
            return ApiResult.Error.SetMessage("获取游戏URL失败：游戏地址为空").SetData<object>(null);
        }

        // 4.5 锁内重新读取会员、查询 MS 侧余额，避免重复进入时再次「全量上分」叠加入账
        member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
        if (member == null)
        {
            _logger.LogWarning("启动MS游戏失败：会员不存在（二次读取）- 玩家ID: {PlayerId}", player_id);
            return ApiResult.Error.SetMessage("会员不存在").SetData(new { });
        }

        var msWalletBalance = await _msGameApi.GetPlayerBalance(member.Username, game.ApiCode);
        if (msWalletBalance < 0)
        {
            if (adminTestLaunch)
            {
                _logger.LogWarning(
                    "后台测试启动MS：游戏侧余额查询失败，按 0 处理且不上分 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}",
                    player_id, game.ApiCode);
                msWalletBalance = 0;
            }
            else
            {
                _logger.LogWarning("启动MS游戏失败：无法查询游戏侧余额 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}", player_id, game.ApiCode);
                return ApiResult.Error.SetMessage("查询游戏钱包余额失败，请稍后再试").SetData(new { });
            }
        }

        // 后台测试：不从主钱包向游戏上分（不判断/不划转主钱包余额）
        var skipDepositFromMain = adminTestLaunch || msWalletBalance > 100;
        if (skipDepositFromMain)
        {
            _logger.LogInformation(
                "MS游戏侧仍有余额，本次不再从主钱包上分（避免重复入账）- 玩家ID: {PlayerId}, ApiCode: {ApiCode}, MsBalance: {MsBalance}, MainCredit: {Credit}",
                player_id, game.ApiCode, msWalletBalance, member.CreditAmount);
        }

        // 5. 将会员余额转出到游戏（上分）
        var amountMovedToGame = skipDepositFromMain ? 0 : NormalizeMsTransferAmount(member.CreditAmount);
        if (amountMovedToGame > 0)
        {
            var transferOrderId = MSGameApi.CreateMsTransferOrderId();

            try
            {
                var (depositOk, depositError) = await _msGameApi.PlayerDeposit(
                    member.Username,
                    amountMovedToGame,
                    transferOrderId,
                    game.ApiCode);
                if (!depositOk)
                {
                    _logger.LogWarning(
                        "MS游戏上分接口返回失败 - 玩家ID: {PlayerId}, 金额: {Amount}, 原因: {Reason}",
                        player_id, amountMovedToGame, depositError);
                    return ApiResult.Error.SetMessage($"余额转入游戏失败: {depositError}").SetData(new { });
                }
            }
            catch (Exception depEx)
            {
                _logger.LogError(depEx, "MS游戏上分失败 - 玩家ID: {PlayerId}, 金额: {Amount}", player_id, amountMovedToGame);
                return ApiResult.Error.SetMessage($"余额转入游戏失败: {depEx.Message}").SetData(new { });
            }

            var transactionResult = await CreateTransactionAsync(
                member,
                transferOrderId,
                -amountMovedToGame,
                TransactionType.TransferIn,
                $"会员 {member.Username} 转入MS游戏",
                "",
                0,
                game.Id
            );

            if (transactionResult.Code != 0)
            {
                var rollbackOk = await TryRollbackMsTransferAsync(member, amountMovedToGame, game.ApiCode, player_id, "启动MS游戏后记账失败");
                var errorMessage = rollbackOk
                    ? "余额转入后记账失败，已自动回滚，请重试"
                    : "余额转入后记账失败且自动回滚失败，请联系客服";
                return ApiResult.Error.SetMessage(errorMessage).SetData<object>(null);
            }
        }
        else if (member.CreditAmount > 0)
        {
            _logger.LogInformation(
                "启动MS游戏 - 会员余额不足1，MS平台不支持上分小数，跳过上分 - 玩家ID: {PlayerId}, 用户名: {Username}, 余额: {Credit}",
                player_id, member.Username, member.CreditAmount);
        }

        _logger.LogInformation(
            "MS游戏启动成功 - 玩家ID: {PlayerId}, 游戏ID: {GameId}, 游戏代码: {GameCode}, 会员账户: {Username}, 转账金额: {Amount}",
            player_id, game.Id, game.GameCode, member.Username, amountMovedToGame);

        // 参与游戏任务：启动游戏即计为 1 局（后台测试不计入）
        if (!adminTestLaunch)
        {
            _ = TryUpdateTaskProgressAsync(member.Id, "PlayGame", 1);
        }

        return ApiResult.Success.SetData(
            new
            {
                gameUrl,
                game.ApiCode
            }
        );
    }

    /// <summary>
    /// 结束 MS 局
    /// </summary>
    /// <param name="player_id">玩家ID</param>
    /// <param name="apiCode">游戏平台代码</param>
    /// <returns>结束游戏结果</returns>
    [HttpGet($"@{nameof(EndMSGame)}")]
    public async Task<ApiResult> EndMSGame(string player_id, string apiCode)
    {
        try
        {
            if (!long.TryParse(player_id, out long memberId))
            {
                _logger.LogWarning("结束MS游戏失败：玩家ID格式不正确 - player_id: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("玩家ID格式不正确");
            }

            // 与启动游戏共用按会员互斥，避免同一玩家并发结束/回收
            using (await _perMemberGate.LockAsync(memberId))
            {
                return await EndMSGameCoreAsync(player_id, apiCode, memberId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束MS游戏时发生异常，玩家ID: {PlayerId}", player_id);
            return ApiResult.Error.SetMessage("结束游戏时发生系统错误，请稍后重试");
        }
    }

    /// <summary>
    /// 锁内回收
    /// </summary>
    private async Task<ApiResult> EndMSGameCoreAsync(string player_id, string apiCode, long memberId)
    {
        var member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
        if (member == null)
        {
            _logger.LogWarning("结束MS游戏失败：会员不存在 - 玩家ID: {PlayerId}", player_id);
            return ApiResult.Error.SetMessage("会员不存在");
        }

        return await RecycleMSGameBalanceAsync(member, apiCode);
    }

    /// <summary>
    /// 近两日回收
    /// </summary>
    /// <param name="player_id">玩家ID（会员 Id）</param>
    [HttpPost($"@{nameof(RecycleRecentTransferInMSGames)}")]
    public async Task<ApiResult> RecycleRecentTransferInMSGames(string player_id)
    {
        try
        {
            if (!long.TryParse(player_id, out long memberId))
            {
                _logger.LogWarning("一键回收MS游戏失败：玩家ID格式不正确 - player_id: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("玩家ID格式不正确");
            }

            using (await _perMemberGate.LockAsync(memberId))
            {
                var member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
                if (member == null)
                {
                    _logger.LogWarning("一键回收MS游戏失败：会员不存在 - 玩家ID: {PlayerId}", player_id);
                    return ApiResult.Error.SetMessage("会员不存在");
                }

                const int maxApiCodes = 3;
                var (apiCodes, distinctTotal) = await GetRecentTransferApiCodesAsync(memberId, maxApiCodes, MsGamePlatformNames);
                if (apiCodes.Count == 0)
                {
                    return ApiResult.Success
                        .SetMessage("近2日内无关联游戏的上分记录，无需回收")
                        .SetData(new { apiCodes = Array.Empty<string>(), details = Array.Empty<object>() });
                }

                _logger.LogInformation(
                    "一键回收MS游戏 - 玩家ID: {PlayerId}, 去重后平台数: {DistinctTotal}, 实际回收数: {RecycleCount}, ApiCodes: {ApiCodes}",
                    memberId, distinctTotal, apiCodes.Count, string.Join(",", apiCodes));

                var details = new List<object>();
                var failCount = 0;
                foreach (var code in apiCodes)
                {
                    var one = await RecycleMSGameBalanceAsync(member, code);
                    if (one.Code != 0)
                    {
                        failCount++;
                    }

                    details.Add(new { apiCode = code, code = one.Code, message = one.Message });
                }

                var summary = failCount == 0
                    ? "已全部尝试回收"
                    : $"部分平台回收失败（失败 {failCount}/{apiCodes.Count}），请查看 details";

                return ApiResult.Success.SetMessage(summary).SetData(new { apiCodes, details });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "一键回收MS游戏时发生异常，玩家ID: {PlayerId}", player_id);
            return ApiResult.Error.SetMessage("一键回收游戏时发生系统错误，请稍后重试");
        }
    }

    private static decimal NormalizeMsTransferAmount(decimal amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        return Math.Floor(amount);
    }

    private async Task<bool> TryRollbackMsTransferAsync(DMember member, decimal amount, string apiCode, string playerId, string scene)
    {
        try
        {
            var rollbackOk = await _msGameApi.PlayerWithdraw(
                member.Username,
                amount,
                MSGameApi.CreateMsTransferOrderId(),
                apiCode);

            if (rollbackOk)
            {
                _logger.LogWarning(
                    "{Scene}，自动回滚上分成功 - 玩家ID: {PlayerId}, 金额: {Amount}, ApiCode: {ApiCode}",
                    scene, playerId, amount, apiCode);
                return true;
            }
        }
        catch (Exception rollbackEx)
        {
            _logger.LogError(
                rollbackEx,
                "{Scene}，自动回滚上分时发生异常 - 玩家ID: {PlayerId}, 金额: {Amount}, ApiCode: {ApiCode}",
                scene, playerId, amount, apiCode);
        }

        _logger.LogError(
            "{Scene}，自动回滚上分失败 - 玩家ID: {PlayerId}, 金额: {Amount}, ApiCode: {ApiCode}",
            scene, playerId, amount, apiCode);
        return false;
    }

    /// <summary>
    /// 回收MS余额
    /// </summary>
    private async Task<ApiResult> RecycleMSGameBalanceAsync(DMember member, string apiCode)
    {
        if (string.IsNullOrWhiteSpace(apiCode))
        {
            return ApiResult.Error.SetMessage("平台代码不能为空");
        }

        try
        {
            var playerIdStr = member.Id.ToString();
            _logger.LogInformation(
                "开始结束MS游戏 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}, 会员账户: {Username}",
                playerIdStr, apiCode, member.Username);

            decimal gameBalance = await _msGameApi.GetPlayerBalance(member.Username, apiCode);
            _logger.LogInformation("玩家MS游戏余额: {GameBalance} - 玩家ID: {PlayerId}", gameBalance, playerIdStr);

            if (gameBalance < 0)
            {
                _logger.LogError("查询MS游戏余额失败 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}", playerIdStr, apiCode);
                return ApiResult.Error.SetMessage("查询游戏余额失败，请联系客服");
            }

            if (gameBalance == 0)
            {
                _logger.LogInformation("玩家MS游戏余额为0，无需转回 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}", playerIdStr, apiCode);
                return ApiResult.Success.SetMessage("游戏结束成功，余额为0");
            }

            bool re = await _msGameApi.PlayerWithdraw(member.Username, gameBalance, MSGameApi.CreateMsTransferOrderId(), apiCode);
            if (!re)
            {
                _logger.LogWarning(
                    "MS游戏下分失败 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}, 金额: {Amount}, 用户名: {Username}",
                    playerIdStr, apiCode, gameBalance, member.Username);
                return ApiResult.Error.SetMessage("游戏转账到余额失败");
            }

            var msPlatformIds = await ResolveGamePlatformIdsAsync(MsGamePlatformNames);
            var gameForLogQuery = _fsql.Select<DGame>().Where(g => g.ApiCode == apiCode);
            if (msPlatformIds.Count > 0)
            {
                gameForLogQuery = gameForLogQuery.Where(g => msPlatformIds.Contains(g.DGamePlatformId));
            }

            var gameForLog = await gameForLogQuery.FirstAsync();
            var transactionResult = await CreateTransactionAsync(
                member,
                Guid.NewGuid().ToString("N"),
                gameBalance,
                TransactionType.TransferOut,
                $"会员 {member.Username} 从MS游戏转回账户",
                "",
                0,
                gameForLog?.Id ?? 0
            );

            if (transactionResult.Code != 0)
            {
                _logger.LogError(
                    "结束MS游戏后创建交易记录失败 - 玩家ID: {PlayerId}, Code: {Code}, Message: {Message}",
                    playerIdStr, transactionResult.Code, transactionResult.Message);
                return ApiResult.Error.SetMessage("创建交易记录失败");
            }

            _logger.LogInformation(
                "MS游戏结束成功 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}, 转回余额: {GameBalance}, 会员账户: {Username}",
                playerIdStr, apiCode, gameBalance, member.Username);

            return ApiResult.Success.SetMessage($"游戏结束成功，转回余额 {gameBalance} CNY");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束MS游戏时发生异常，玩家ID: {PlayerId}, ApiCode: {ApiCode}", member.Id, apiCode);
            return ApiResult.Error.SetMessage("结束游戏时发生系统错误，请稍后重试");
        }
    }

    /// <summary>
    /// MS 历史注单
    /// </summary>
    /// <param name="player_id">玩家ID</param>
    /// <param name="from">开始时间</param>
    /// <param name="to">结束时间</param>
    /// <param name="limit">每页条数</param>
    /// <param name="page">页码</param>
    /// <returns>注单历史记录列表</returns>
    [HttpGet($"@{nameof(GetMSGameHistory)}")]
    public async Task<ApiResult<List<DTransAction>>> GetMSGameHistory(string player_id, string from = "", string to = "", int limit = 500, int page = 1)
    {
        try
        {
            // 1. 验证玩家ID
            if (!long.TryParse(player_id, out long memberId))
            {
                _logger.LogWarning("获取MS游戏历史失败：玩家ID格式不正确 - player_id: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("玩家ID格式不正确").SetData<List<DTransAction>>(null);
            }

            var member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
            if (member == null)
            {
                _logger.LogWarning("获取MS游戏历史失败：会员不存在 - 玩家ID: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("会员不存在").SetData<List<DTransAction>>(null);
            }

            // 2. 设置默认时间范围（如果未提供）：与会员同步一致，按北京时间近 7 日
            var china = CultureInfo.GetCultureInfo("zh-CN");
            const string msBetFmt = "yyyy-MM-dd HH:mm:ss";
            var bjNow = DateTime.Now;
            if (string.IsNullOrEmpty(from))
            {
                from = bjNow.AddDays(-7).ToString(msBetFmt, china);
            }
            if (string.IsNullOrEmpty(to))
            {
                to = bjNow.ToString(msBetFmt, china);
            }

            // 3. 验证时间格式
            if (!DateTime.TryParse(from, china, DateTimeStyles.None, out DateTime fromDateTime))
            {
                _logger.LogWarning("获取MS游戏历史失败：开始时间格式错误 - from: {From}", from);
                return ApiResult.Error.SetMessage("开始时间格式不正确，格式应为：yyyy-MM-dd HH:mm:ss").SetData<List<DTransAction>>(null);
            }
            if (!DateTime.TryParse(to, china, DateTimeStyles.None, out DateTime toDateTime))
            {
                _logger.LogWarning("获取MS游戏历史失败：结束时间格式错误 - to: {To}", to);
                return ApiResult.Error.SetMessage("结束时间格式不正确，格式应为：yyyy-MM-dd HH:mm:ss").SetData<List<DTransAction>>(null);
            }

            // 4. 验证分页参数
            if (limit <= 0 || limit > 500)
            {
                _logger.LogWarning("获取MS游戏历史失败：limit 非法 - limit: {Limit}", limit);
                return ApiResult.Error.SetMessage("每页条数必须在1-500之间").SetData<List<DTransAction>>(null);
            }
            if (page <= 0)
            {
                _logger.LogWarning("获取MS游戏历史失败：page 非法 - page: {Page}", page);
                return ApiResult.Error.SetMessage("页码必须大于0").SetData<List<DTransAction>>(null);
            }

            _logger.LogInformation("开始获取MS游戏历史注单 - 玩家ID: {PlayerId}, 开始时间: {From}, 结束时间: {To}, 页数: {Page}, 每页条数: {Limit}",
                player_id, from, to, page, limit);

            // 5. 调用MS游戏API获取注单历史
            var history = await _msGameApi.GetBetHistory(member.Username, from, to, limit, page);

            _logger.LogInformation("MS游戏历史注单获取完成 - 玩家ID: {PlayerId}, 获取到 {Count} 条记录",
                player_id, history.Count);

            return ApiResult.Success.SetData(history);
        }
        catch (Exception ex)
        {
            // 记录详细错误日志
            _logger.LogError(ex, "获取MS游戏历史注单时发生异常，玩家ID: {PlayerId}, 开始时间: {From}, 结束时间: {To}",
                player_id, from, to);
            return ApiResult.Error.SetMessage("获取游戏历史时发生系统错误，请稍后重试").SetData<List<DTransAction>>(null);
        }
    }


    /// <summary>
    /// MS 游戏列表
    /// </summary>
    [HttpGet($"@{nameof(GetMSGameList)}")]
    [AllowAnonymous]
    public async Task<ApiResult<string>> GetMSGameList(string apiCode)
    {
        try
        {
            var gameList = new List<DGame>();
            if (string.IsNullOrEmpty(apiCode))
            {
                apiCode = "AG, BBIN, YB, WM, AB, OB, XGLIVE, BG, DG, EVO, SEXY, TTG, AT, PG, PP, ISB, JDB, CQ9, PNG, RTG, GSS, PT, MG, TCG, VR, OBCP, SBO, IBC, SS, AGTY, FB, CMD, XJ, HG, NEWBB, OB, KY, GDQ, BL, TH, LEG, KX, NW, WALI, DTQP, BBCARD, OB, IA, TFG, AVIA, BBIN, OB, KY, AT, CQ9, JDB, LEG, AP, TH, KA, JOKER, GMFX, AP";
            }
            var apiCodeList = apiCode.Split(',').ToList();
            _logger.LogInformation("获取MS游戏列表 - 平台代码段数: {SegmentCount}", apiCodeList.Count);

            foreach (var code in apiCodeList)
            {
                var trimmed = code.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;
                var sync = await _msGameApi.GetGameList(trimmed);
                if (!sync.Success)
                {
                    _logger.LogWarning("MS GetGameList 失败 - apiCode: {ApiCode}, Message: {Message}", trimmed, sync.Message);
                    continue;
                }

                var gameListTmp = JsonConvert.DeserializeObject<List<DGame>>(sync.GamesJson) ?? [];
                gameList.AddRange(gameListTmp);
            }

            _logger.LogInformation("获取MS游戏列表完成 - 合并后游戏数: {TotalCount}", gameList.Count);
            return ApiResult.Success.SetData(gameList.ToJson());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取MS游戏列表时发生异常");
            return ApiResult.Error.SetMessage("获取MS游戏列表失败，请稍后重试").SetData<string>(null);
        }
    }

    /// <summary>
    /// MS 平台余额
    /// </summary>
    /// <param name="player_id">玩家ID</param>
    /// <param name="apiCode">游戏平台代码</param>
    /// <returns>包含余额的API结果</returns>
    [HttpGet($"@{nameof(GetMSGameBalance)}")]
    public async Task<ApiResult<decimal>> GetMSGameBalance(string player_id, string apiCode)
    {
        try
        {
            // 1. 验证玩家ID
            if (!long.TryParse(player_id, out long memberId))
            {
                _logger.LogWarning("获取MS游戏余额失败：玩家ID格式不正确 - player_id: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("玩家ID格式不正确").SetData<decimal>(-1);
            }

            var member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
            if (member == null)
            {
                _logger.LogWarning("获取MS游戏余额失败：会员不存在 - 玩家ID: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("会员不存在").SetData<decimal>(-1);
            }

            // 2. 验证平台代码
            if (string.IsNullOrEmpty(apiCode))
            {
                _logger.LogWarning("获取MS游戏余额失败：平台代码为空 - 玩家ID: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("平台代码不能为空").SetData<decimal>(-1);
            }

            _logger.LogInformation("获取MS游戏余额 - 玩家ID: {PlayerId}, 平台代码: {ApiCode}, 会员账户: {Username}",
                player_id, apiCode, member.Username);

            // 3. 查询玩家在指定游戏平台的余额
            decimal gameBalance = await _msGameApi.GetPlayerBalance(member.Username, apiCode);

            if (gameBalance < 0)
            {
                _logger.LogError("查询MS游戏余额失败 - 玩家ID: {PlayerId}, 平台代码: {ApiCode}", player_id, apiCode);
                return ApiResult.Error.SetMessage("查询游戏余额失败，请联系客服").SetData<decimal>(-1);
            }

            _logger.LogInformation("MS游戏余额查询成功 - 玩家ID: {PlayerId}, 平台代码: {ApiCode}, 余额: {Balance}",
                player_id, apiCode, gameBalance);

            return ApiResult.Success.SetData(gameBalance);
        }
        catch (Exception ex)
        {
            // 记录详细错误日志
            _logger.LogError(ex, "获取MS游戏余额时发生异常，玩家ID: {PlayerId}, 平台代码: {ApiCode}", player_id, apiCode);
            return ApiResult.Error.SetMessage("查询游戏余额时发生系统错误，请稍后重试").SetData<decimal>(-1);
        }
    }

    /// <summary>
    /// 启动 XH 局
    /// </summary>
    /// <param name="player_id">玩家ID</param>
    /// <param name="gameId">游戏ID</param>
    /// <returns>包含游戏URL的API结果</returns>
    [HttpGet($"@{nameof(StartXHGame)}")]
    public async Task<ApiResult> StartXHGame(string player_id, string gameId)
    {
        try
        {
            if (!long.TryParse(player_id, out long memberId))
            {
                _logger.LogWarning("启动XH游戏失败：玩家ID格式不正确 - player_id: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("玩家ID格式不正确").SetData(new { });
            }

            using (await _perMemberGate.LockAsync(memberId))
            {
                return await StartXHGameCoreAsync(player_id, gameId, memberId, adminTestLaunch: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动XH游戏时发生异常，玩家ID: {PlayerId}, 游戏ID: {GameId}", player_id, gameId);
            return ApiResult.Error.SetMessage("启动游戏时发生系统错误，请稍后重试").SetData<object>(null);
        }
    }

    /// <summary>
    /// 后台游戏管理「测试启动」专用：不根据主钱包/游戏钱包做上分；游戏侧余额查询失败时仍尝试返回启动链接；允许已禁用游戏取链（不入 HTTP 路由）。
    /// </summary>
    [NonAction]
    public async Task<ApiResult> StartXHGameAdminTestAsync(string player_id, string gameId)
    {
        try
        {
            if (!long.TryParse(player_id, out long memberId))
            {
                _logger.LogWarning("后台测试启动XH游戏失败：玩家ID格式不正确 - player_id: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("玩家ID格式不正确").SetData(new { });
            }

            using (await _perMemberGate.LockAsync(memberId))
            {
                return await StartXHGameCoreAsync(player_id, gameId, memberId, adminTestLaunch: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "后台测试启动XH游戏时发生异常，玩家ID: {PlayerId}, 游戏ID: {GameId}", player_id, gameId);
            return ApiResult.Error.SetMessage("启动游戏时发生系统错误，请稍后重试").SetData<object>(null);
        }
    }

    /// <summary>
    /// 锁内启动
    /// </summary>
    private async Task<ApiResult> StartXHGameCoreAsync(string player_id, string gameId, long memberId, bool adminTestLaunch = false)
    {
        var member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
        if (member == null)
        {
            _logger.LogWarning("启动XH游戏失败：会员不存在 - 玩家ID: {PlayerId}", player_id);
            return ApiResult.Error.SetMessage("会员不存在").SetData(new { });
        }

        if (member.CreditAmount <= 0)
        {
            _logger.LogInformation("启动XH游戏 - 会员余额为0或负数，跳过上分 - 玩家ID: {PlayerId}, 用户名: {Username}", player_id, member.Username);
        }

        if (!long.TryParse(gameId, out var parsedGameId))
        {
            _logger.LogWarning("启动XH游戏失败：游戏ID格式不正确 - 玩家ID: {PlayerId}, gameId: {GameId}", player_id, gameId);
            return ApiResult.Error.SetMessage("游戏ID格式不正确").SetData(new { });
        }

        var game = await FindGameByIdAsync(parsedGameId, XhGamePlatformNames);
        if (game == null)
        {
            _logger.LogWarning("启动XH游戏失败：游戏不存在 - 玩家ID: {PlayerId}, gameId: {GameId}", player_id, gameId);
            return ApiResult.Error.SetMessage($"游戏不存在，请检查 gameId 参数（当前: {gameId}）").SetData(new { });
        }
        if (game.IsEnabled == false && !adminTestLaunch)
        {
            _logger.LogWarning("启动XH游戏失败：游戏已禁用 - 玩家ID: {PlayerId}, gameId: {GameId}", player_id, game.Id);
            return ApiResult.Error.SetMessage("游戏已禁用").SetData(new { });
        }

        await _fsql.Update<DGame>()
            .Set(g => g.ClickCount + 1)
            .Where(g => g.Id == game.Id)
            .ExecuteAffrowsAsync();
        game.ClickCount += 1;

        _logger.LogInformation(
            "{LaunchMode}XH游戏 - 玩家ID: {PlayerId}, 用户名: {Username}, gameId: {GameId}, GameCode: {GameCode}, ApiCode: {ApiCode}, 余额: {Credit}, 点击次数: {ClickCount}",
            adminTestLaunch ? "后台测试启动" : "启动", player_id, member.Username, game.Id, game.GameCode, game.ApiCode, member.CreditAmount, game.ClickCount);

        try
        {
            await _xhGameApi.UserRegister(member.Username, game.ApiCode);
        }
        catch (Exception regEx)
        {
            _logger.LogError(regEx, "XH游戏用户注册失败 - 玩家ID: {PlayerId}, 游戏ID: {GameId}", player_id, game.Id);
            return ApiResult.Error.SetMessage("游戏服务器连接失败，请稍后重试").SetData(new { });
        }

        string gameUrl;
        try
        {
            gameUrl = await _xhGameApi.GetGameUrl(member.Username, game.GameCode, game.ApiCode, game.GameType);
        }
        catch (Exception urlEx)
        {
            _logger.LogError(urlEx, "获取XH游戏URL失败 - 玩家ID: {PlayerId}, 游戏ID: {GameId}", player_id, game.Id);
            return ApiResult.Error.SetMessage("获取游戏地址失败，请稍后重试").SetData<object>(null);
        }

        _logger.LogInformation(
            "已获取XH游戏URL - 玩家ID: {PlayerId}, GameCode: {ResolvedGameCode}, ApiCode: {ApiCode}, GameType: {GameType}",
            player_id, game.GameCode, game.ApiCode, game.GameType);
        if (string.IsNullOrEmpty(gameUrl))
        {
            _logger.LogWarning("获取XH游戏URL为空 - 玩家ID: {PlayerId}, gameId: {GameId}", player_id, game.Id);
            return ApiResult.Error.SetMessage("获取游戏URL失败：游戏地址为空").SetData<object>(null);
        }

        member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
        if (member == null)
        {
            _logger.LogWarning("启动XH游戏失败：会员不存在（二次读取）- 玩家ID: {PlayerId}", player_id);
            return ApiResult.Error.SetMessage("会员不存在").SetData(new { });
        }

        var xhWalletBalance = await _xhGameApi.GetPlayerBalance(member.Username, game.ApiCode);
        if (xhWalletBalance < 0)
        {
            if (adminTestLaunch)
            {
                _logger.LogWarning(
                    "后台测试启动XH：游戏侧余额查询失败，按 0 处理且不上分 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}",
                    player_id, game.ApiCode);
                xhWalletBalance = 0;
            }
            else
            {
                _logger.LogWarning("启动XH游戏失败：无法查询游戏侧余额 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}", player_id, game.ApiCode);
                return ApiResult.Error.SetMessage("查询游戏钱包余额失败，请稍后再试").SetData(new { });
            }
        }

        var skipDepositFromMain = adminTestLaunch || xhWalletBalance > 100;
        if (skipDepositFromMain)
        {
            _logger.LogInformation(
                "XH游戏侧仍有余额，本次不再从主钱包上分（避免重复入账）- 玩家ID: {PlayerId}, ApiCode: {ApiCode}, XhBalance: {XhBalance}, MainCredit: {Credit}",
                player_id, game.ApiCode, xhWalletBalance, member.CreditAmount);
        }

        var amountMovedToGame = skipDepositFromMain ? 0 : NormalizeXhTransferAmount(member.CreditAmount);
        if (amountMovedToGame > 0)
        {
            var transferOrderId = XHGameApi.CreateXhTransferOrderId();

            try
            {
                var (depositOk, depositError) = await _xhGameApi.PlayerDeposit(
                    member.Username,
                    amountMovedToGame,
                    transferOrderId,
                    game.ApiCode);
                if (!depositOk)
                {
                    _logger.LogWarning(
                        "XH游戏上分接口返回失败 - 玩家ID: {PlayerId}, 金额: {Amount}, 原因: {Reason}",
                        player_id, amountMovedToGame, depositError);
                    return ApiResult.Error.SetMessage($"余额转入游戏失败: {depositError}").SetData(new { });
                }
            }
            catch (Exception depEx)
            {
                _logger.LogError(depEx, "XH游戏上分失败 - 玩家ID: {PlayerId}, 金额: {Amount}", player_id, amountMovedToGame);
                return ApiResult.Error.SetMessage($"余额转入游戏失败: {depEx.Message}").SetData(new { });
            }

            var transactionResult = await CreateTransactionAsync(
                member,
                transferOrderId,
                -amountMovedToGame,
                TransactionType.TransferIn,
                $"会员 {member.Username} 转入XH游戏",
                "",
                0,
                game.Id
            );

            if (transactionResult.Code != 0)
            {
                var rollbackOk = await TryRollbackXhTransferAsync(member, amountMovedToGame, game.ApiCode, player_id, "启动XH游戏后记账失败");
                var errorMessage = rollbackOk
                    ? "余额转入后记账失败，已自动回滚，请重试"
                    : "余额转入后记账失败且自动回滚失败，请联系客服";
                return ApiResult.Error.SetMessage(errorMessage).SetData<object>(null);
            }
        }
        else if (member.CreditAmount > 0)
        {
            _logger.LogInformation(
                "启动XH游戏 - 会员余额不足1，XH平台不支持上分小数，跳过上分 - 玩家ID: {PlayerId}, 用户名: {Username}, 余额: {Credit}",
                player_id, member.Username, member.CreditAmount);
        }

        _logger.LogInformation(
            "XH游戏启动成功 - 玩家ID: {PlayerId}, 游戏ID: {GameId}, 游戏代码: {GameCode}, 会员账户: {Username}, 转账金额: {Amount}",
            player_id, game.Id, game.GameCode, member.Username, amountMovedToGame);

        if (!adminTestLaunch)
        {
            _ = TryUpdateTaskProgressAsync(member.Id, "PlayGame", 1);
        }

        return ApiResult.Success.SetData(
            new
            {
                gameUrl,
                game.ApiCode
            }
        );
    }

    /// <summary>
    /// 结束 XH 局
    /// </summary>
    /// <param name="player_id">玩家ID</param>
    /// <param name="apiCode">游戏平台代码</param>
    /// <returns>结束游戏结果</returns>
    [HttpGet($"@{nameof(EndXHGame)}")]
    public async Task<ApiResult> EndXHGame(string player_id, string apiCode)
    {
        try
        {
            if (!long.TryParse(player_id, out long memberId))
            {
                _logger.LogWarning("结束XH游戏失败：玩家ID格式不正确 - player_id: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("玩家ID格式不正确");
            }

            using (await _perMemberGate.LockAsync(memberId))
            {
                return await EndXHGameCoreAsync(player_id, apiCode, memberId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束XH游戏时发生异常，玩家ID: {PlayerId}", player_id);
            return ApiResult.Error.SetMessage("结束游戏时发生系统错误，请稍后重试");
        }
    }

    /// <summary>
    /// 锁内回收
    /// </summary>
    private async Task<ApiResult> EndXHGameCoreAsync(string player_id, string apiCode, long memberId)
    {
        var member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
        if (member == null)
        {
            _logger.LogWarning("结束XH游戏失败：会员不存在 - 玩家ID: {PlayerId}", player_id);
            return ApiResult.Error.SetMessage("会员不存在");
        }

        return await RecycleXHGameBalanceAsync(member, apiCode);
    }

    /// <summary>
    /// 近两日回收
    /// </summary>
    /// <param name="player_id">玩家ID（会员 Id）</param>
    [HttpPost($"@{nameof(RecycleRecentTransferInXHGames)}")]
    public async Task<ApiResult> RecycleRecentTransferInXHGames(string player_id)
    {
        try
        {
            if (!long.TryParse(player_id, out long memberId))
            {
                _logger.LogWarning("一键回收XH游戏失败：玩家ID格式不正确 - player_id: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("玩家ID格式不正确");
            }

            using (await _perMemberGate.LockAsync(memberId))
            {
                var member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
                if (member == null)
                {
                    _logger.LogWarning("一键回收XH游戏失败：会员不存在 - 玩家ID: {PlayerId}", player_id);
                    return ApiResult.Error.SetMessage("会员不存在");
                }

                const int maxApiCodes = 3;
                var (apiCodes, distinctTotal) = await GetRecentTransferApiCodesAsync(memberId, maxApiCodes, XhGamePlatformNames);
                if (apiCodes.Count == 0)
                {
                    return ApiResult.Success
                        .SetMessage("近2日内无关联游戏的上分记录，无需回收")
                        .SetData(new { apiCodes = Array.Empty<string>(), details = Array.Empty<object>() });
                }

                _logger.LogInformation(
                    "一键回收XH游戏 - 玩家ID: {PlayerId}, 去重后平台数: {DistinctTotal}, 实际回收数: {RecycleCount}, ApiCodes: {ApiCodes}",
                    memberId, distinctTotal, apiCodes.Count, string.Join(",", apiCodes));

                var details = new List<object>();
                var failCount = 0;
                foreach (var code in apiCodes)
                {
                    var one = await RecycleXHGameBalanceAsync(member, code);
                    if (one.Code != 0)
                    {
                        failCount++;
                    }

                    details.Add(new { apiCode = code, code = one.Code, message = one.Message });
                }

                var summary = failCount == 0
                    ? "已全部尝试回收"
                    : $"部分平台回收失败（失败 {failCount}/{apiCodes.Count}），请查看 details";

                return ApiResult.Success.SetMessage(summary).SetData(new { apiCodes, details });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "一键回收XH游戏时发生异常，玩家ID: {PlayerId}", player_id);
            return ApiResult.Error.SetMessage("一键回收游戏时发生系统错误，请稍后重试");
        }
    }

    private static decimal NormalizeXhTransferAmount(decimal amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        return Math.Floor(amount);
    }

    private async Task<bool> TryRollbackXhTransferAsync(DMember member, decimal amount, string apiCode, string playerId, string scene)
    {
        try
        {
            var rollbackOk = await _xhGameApi.PlayerWithdraw(
                member.Username,
                amount,
                XHGameApi.CreateXhTransferOrderId(),
                apiCode);

            if (rollbackOk)
            {
                _logger.LogWarning(
                    "{Scene}，自动回滚上分成功 - 玩家ID: {PlayerId}, 金额: {Amount}, ApiCode: {ApiCode}",
                    scene, playerId, amount, apiCode);
                return true;
            }
        }
        catch (Exception rollbackEx)
        {
            _logger.LogError(
                rollbackEx,
                "{Scene}，自动回滚上分时发生异常 - 玩家ID: {PlayerId}, 金额: {Amount}, ApiCode: {ApiCode}",
                scene, playerId, amount, apiCode);
        }

        _logger.LogError(
            "{Scene}，自动回滚上分失败 - 玩家ID: {PlayerId}, 金额: {Amount}, ApiCode: {ApiCode}",
            scene, playerId, amount, apiCode);
        return false;
    }

    /// <summary>
    /// 回收XH余额
    /// </summary>
    private async Task<ApiResult> RecycleXHGameBalanceAsync(DMember member, string apiCode)
    {
        if (string.IsNullOrWhiteSpace(apiCode))
        {
            return ApiResult.Error.SetMessage("平台代码不能为空");
        }

        try
        {
            var playerIdStr = member.Id.ToString();
            _logger.LogInformation(
                "开始结束XH游戏 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}, 会员账户: {Username}",
                playerIdStr, apiCode, member.Username);

            decimal gameBalance = await _xhGameApi.GetPlayerBalance(member.Username, apiCode);
            _logger.LogInformation("玩家XH游戏余额: {GameBalance} - 玩家ID: {PlayerId}", gameBalance, playerIdStr);

            if (gameBalance < 0)
            {
                _logger.LogError("查询XH游戏余额失败 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}", playerIdStr, apiCode);
                return ApiResult.Error.SetMessage("查询游戏余额失败，请联系客服");
            }

            if (gameBalance == 0)
            {
                _logger.LogInformation("玩家XH游戏余额为0，无需转回 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}", playerIdStr, apiCode);
                return ApiResult.Success.SetMessage("游戏结束成功，余额为0");
            }

            bool re = await _xhGameApi.PlayerWithdraw(member.Username, gameBalance, XHGameApi.CreateXhTransferOrderId(), apiCode);
            if (!re)
            {
                _logger.LogWarning(
                    "XH游戏下分失败 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}, 金额: {Amount}, 用户名: {Username}",
                    playerIdStr, apiCode, gameBalance, member.Username);
                return ApiResult.Error.SetMessage("游戏转账到余额失败");
            }

            var xhPlatformIds = await ResolveGamePlatformIdsAsync(XhGamePlatformNames);
            var gameForLogQuery = _fsql.Select<DGame>().Where(g => g.ApiCode == apiCode);
            if (xhPlatformIds.Count > 0)
            {
                gameForLogQuery = gameForLogQuery.Where(g => xhPlatformIds.Contains(g.DGamePlatformId));
            }

            var gameForLog = await gameForLogQuery.FirstAsync();
            var transactionResult = await CreateTransactionAsync(
                member,
                Guid.NewGuid().ToString("N"),
                gameBalance,
                TransactionType.TransferOut,
                $"会员 {member.Username} 从XH游戏转回账户",
                "",
                0,
                gameForLog?.Id ?? 0
            );

            if (transactionResult.Code != 0)
            {
                _logger.LogError(
                    "结束XH游戏后创建交易记录失败 - 玩家ID: {PlayerId}, Code: {Code}, Message: {Message}",
                    playerIdStr, transactionResult.Code, transactionResult.Message);
                return ApiResult.Error.SetMessage("创建交易记录失败");
            }

            _logger.LogInformation(
                "XH游戏结束成功 - 玩家ID: {PlayerId}, ApiCode: {ApiCode}, 转回余额: {GameBalance}, 会员账户: {Username}",
                playerIdStr, apiCode, gameBalance, member.Username);

            return ApiResult.Success.SetMessage($"游戏结束成功，转回余额 {gameBalance} CNY");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束XH游戏时发生异常，玩家ID: {PlayerId}, ApiCode: {ApiCode}", member.Id, apiCode);
            return ApiResult.Error.SetMessage("结束游戏时发生系统错误，请稍后重试");
        }
    }

    /// <summary>
    /// XH 历史注单
    /// </summary>
    /// <param name="player_id">玩家ID</param>
    /// <param name="from">开始时间</param>
    /// <param name="to">结束时间</param>
    /// <param name="limit">每页条数</param>
    /// <param name="page">页码</param>
    /// <returns>注单历史记录列表</returns>
    [HttpGet($"@{nameof(GetXHGameHistory)}")]
    public async Task<ApiResult<List<DTransAction>>> GetXHGameHistory(string player_id, string from = "", string to = "", int limit = 500, int page = 1)
    {
        try
        {
            if (!long.TryParse(player_id, out long memberId))
            {
                _logger.LogWarning("获取XH游戏历史失败：玩家ID格式不正确 - player_id: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("玩家ID格式不正确").SetData<List<DTransAction>>(null);
            }

            var member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
            if (member == null)
            {
                _logger.LogWarning("获取XH游戏历史失败：会员不存在 - 玩家ID: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("会员不存在").SetData<List<DTransAction>>(null);
            }

            if (string.IsNullOrEmpty(from))
            {
                // XH gamerecord 要求开始/结束间隔不超过 24 小时，默认只查最近 24 小时（勿用近 7 天）
                from = DateTime.Now.AddHours(-24).ToString("yyyy-MM-dd HH:mm:ss");
            }
            if (string.IsNullOrEmpty(to))
            {
                to = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            if (!DateTime.TryParse(from, out var fromDt))
            {
                _logger.LogWarning("获取XH游戏历史失败：开始时间格式错误 - from: {From}", from);
                return ApiResult.Error.SetMessage("开始时间格式不正确，格式应为：yyyy-MM-dd HH:mm:ss").SetData<List<DTransAction>>(null);
            }
            if (!DateTime.TryParse(to, out var toDt))
            {
                _logger.LogWarning("获取XH游戏历史失败：结束时间格式错误 - to: {To}", to);
                return ApiResult.Error.SetMessage("结束时间格式不正确，格式应为：yyyy-MM-dd HH:mm:ss").SetData<List<DTransAction>>(null);
            }

            var spanSec = (long)(toDt - fromDt).TotalSeconds;
            if (spanSec < 0)
            {
                return ApiResult.Error.SetMessage("结束时间不能早于开始时间").SetData<List<DTransAction>>(null);
            }
            if (spanSec > 86400)
            {
                return ApiResult.Error.SetMessage("XH 注单查询时间跨度不能超过 24 小时，请缩小开始/结束时间后重试").SetData<List<DTransAction>>(null);
            }

            if (limit <= 0 || limit > 500)
            {
                _logger.LogWarning("获取XH游戏历史失败：limit 非法 - limit: {Limit}", limit);
                return ApiResult.Error.SetMessage("每页条数必须在1-500之间").SetData<List<DTransAction>>(null);
            }
            if (page <= 0)
            {
                _logger.LogWarning("获取XH游戏历史失败：page 非法 - page: {Page}", page);
                return ApiResult.Error.SetMessage("页码必须大于0").SetData<List<DTransAction>>(null);
            }

            _logger.LogInformation("开始获取XH游戏历史注单 - 玩家ID: {PlayerId}, 开始时间: {From}, 结束时间: {To}, 页数: {Page}, 每页条数: {Limit}",
                player_id, from, to, page, limit);

            var history = await _xhGameApi.GetBetHistory(member.Username, from, to, limit, page);

            _logger.LogInformation("XH游戏历史注单获取完成 - 玩家ID: {PlayerId}, 获取到 {Count} 条记录",
                player_id, history.Count);

            return ApiResult.Success.SetData(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取XH游戏历史注单时发生异常，玩家ID: {PlayerId}, 开始时间: {From}, 结束时间: {To}",
                player_id, from, to);
            return ApiResult.Error.SetMessage("获取游戏历史时发生系统错误，请稍后重试").SetData<List<DTransAction>>(null);
        }
    }

    /// <summary>
    /// XH 游戏列表
    /// </summary>
    [HttpGet($"@{nameof(GetXHGameList)}")]
    [AllowAnonymous]
    public async Task<ApiResult<string>> GetXHGameList(string apiCode)
    {
        try
        {
            var gameList = new List<DGame>();
            if (string.IsNullOrWhiteSpace(apiCode))
            {
                apiCode = "GW";
            }

            var apiCodeList = apiCode.Split(',').ToList();
            _logger.LogInformation("获取XH游戏列表 - 平台代码段数: {SegmentCount}", apiCodeList.Count);

            foreach (var code in apiCodeList)
            {
                var trimmed = code.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                var sync = await _xhGameApi.GetGameList(trimmed);
                if (!sync.Success)
                {
                    _logger.LogWarning("XH GetGameList 失败 - apiCode: {ApiCode}, Message: {Message}", trimmed, sync.Message);
                    continue;
                }

                var gameListTmp = JsonConvert.DeserializeObject<List<DGame>>(sync.GamesJson) ?? [];
                gameList.AddRange(gameListTmp);
            }

            _logger.LogInformation("获取XH游戏列表完成 - 合并后游戏数: {TotalCount}", gameList.Count);
            return ApiResult.Success.SetData(gameList.ToJson());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取XH游戏列表时发生异常");
            return ApiResult.Error.SetMessage("获取XH游戏列表失败，请稍后重试").SetData<string>(null);
        }
    }

    /// <summary>
    /// XH 平台余额
    /// </summary>
    /// <param name="player_id">玩家ID</param>
    /// <param name="apiCode">游戏平台代码</param>
    /// <returns>包含余额的API结果</returns>
    [HttpGet($"@{nameof(GetXHGameBalance)}")]
    public async Task<ApiResult<decimal>> GetXHGameBalance(string player_id, string apiCode)
    {
        try
        {
            if (!long.TryParse(player_id, out long memberId))
            {
                _logger.LogWarning("获取XH游戏余额失败：玩家ID格式不正确 - player_id: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("玩家ID格式不正确").SetData<decimal>(-1);
            }

            var member = await _fsql.Select<DMember>().Where(m => m.Id == memberId).ToOneAsync();
            if (member == null)
            {
                _logger.LogWarning("获取XH游戏余额失败：会员不存在 - 玩家ID: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("会员不存在").SetData<decimal>(-1);
            }

            if (string.IsNullOrEmpty(apiCode))
            {
                _logger.LogWarning("获取XH游戏余额失败：平台代码为空 - 玩家ID: {PlayerId}", player_id);
                return ApiResult.Error.SetMessage("平台代码不能为空").SetData<decimal>(-1);
            }

            _logger.LogInformation("获取XH游戏余额 - 玩家ID: {PlayerId}, 平台代码: {ApiCode}, 会员账户: {Username}",
                player_id, apiCode, member.Username);

            decimal gameBalance = await _xhGameApi.GetPlayerBalance(member.Username, apiCode);

            if (gameBalance < 0)
            {
                _logger.LogError("查询XH游戏余额失败 - 玩家ID: {PlayerId}, 平台代码: {ApiCode}", player_id, apiCode);
                return ApiResult.Error.SetMessage("查询游戏余额失败，请联系客服").SetData<decimal>(-1);
            }

            _logger.LogInformation("XH游戏余额查询成功 - 玩家ID: {PlayerId}, 平台代码: {ApiCode}, 余额: {Balance}",
                player_id, apiCode, gameBalance);

            return ApiResult.Success.SetData(gameBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取XH游戏余额时发生异常，玩家ID: {PlayerId}, 平台代码: {ApiCode}", player_id, apiCode);
            return ApiResult.Error.SetMessage("查询游戏余额时发生系统错误，请稍后重试").SetData<decimal>(-1);
        }
    }


}
