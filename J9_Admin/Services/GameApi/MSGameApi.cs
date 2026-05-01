using System.Globalization;
using System.Threading;
using J9_Admin.Utils;
using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; // 用于JObject

namespace J9_Admin.Services.GameApi;

/// <summary>MS 同步游戏列表结果（远程条数、新增/跳过、按类型与接口代码分类统计）。</summary>
public class MSGameListSyncResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    /// <summary>接口返回的游戏条数</summary>
    public int RemoteTotal { get; set; }
    /// <summary>新写入数据库的条数</summary>
    public int Inserted { get; set; }
    /// <summary>本地已存在（按中文名匹配）未写入的条数</summary>
    public int SkippedExisting { get; set; }
    /// <summary>与历史兼容：本次接口拉取到的全部游戏 JSON 数组</summary>
    public string GamesJson { get; set; } = "[]";
    /// <summary>仅统计本次「新增」记录，按游戏类型</summary>
    public Dictionary<GameType, int> NewByGameType { get; set; } = new();
    /// <summary>仅统计本次「新增」记录，按子游戏接口代码（code）</summary>
    public Dictionary<string, int> NewByApiCode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>MS 注单一键同步落库结果</summary>
public class MSBetHistorySyncResult
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    /// <summary>接口累计拉取条数（分页相加）</summary>
    public int RemoteFetched { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    /// <summary>rowid 为空无法去重的条数</summary>
    public int SkippedNoSerial { get; set; }
    public int PagesDone { get; set; }
}

public class MSGameApi
{
    private static int _msTransferSequence;
    private const string DebugLaunchUrl = "https://www.baidu.com";
    private const decimal DebugWalletBalance = 1000m;

    /// <summary>与 <see cref="J9_Admin.API.GameService"/> 中 MS 平台一致，按名称在 <c>ddd_game_platform</c> 解析主键。</summary>
    private static readonly string[] MsGamePlatformNames = ["MS", "MS游戏", "美盛游戏"];

    private readonly ILogger<MSGameApi> _logger;
    private readonly FreeSqlCloud _fsql;

    private string account = "levers990";
    private string apiKey = "FJdifHOvOSErvpwU73VndWsPJvg4kOtx";

    private string apiUrl = "https://apis.msgm01.com";

    private static bool IsDebugEnvironment()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            Environments.Development,
            StringComparison.OrdinalIgnoreCase);
    }


    public MSGameApi(ILogger<MSGameApi> logger, FreeSqlCloud fsql)
    {
        _logger = logger;
        _fsql = fsql;
        _logger.LogInformation($"MSGameApi initialized");
    }

    /// <summary>
    /// MS 接口要求 transferno（transid）为正整数，生成十进制字符串订单号。
    /// </summary>
    public static string CreateMsTransferOrderId()
    {
        long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ulong n = (ulong)ms * 10_000UL + (ulong)(Interlocked.Increment(ref _msTransferSequence) % 10_000);
        if (n == 0)
        {
            n = 1;
        }

        return n.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// MS 用户注册
    /// </summary>
    /// <param name="player_name">玩家名称（用作 MS 登录的 username）</param>
    /// <param name="apiCode">接口标识</param>
    /// <returns>MS用户注册结果</returns>
    public async Task<bool> UserRegister(string player_name, string apiCode)
    {
        if (IsDebugEnvironment())
        {
            _logger.LogInformation("MS游戏用户注册走调试模拟 - 玩家名称: {PlayerName}, 接口标识: {ApiCode}", player_name, apiCode);
            return true;
        }

        try
        {
            // 1. 参数验证
            if (string.IsNullOrEmpty(player_name))
            {
                _logger.LogWarning("MS游戏用户注册失败：玩家名称不能为空");
                return false;
            }

            // 2. 构建请求参数
            string account = this.account; // 商户账号
            string apiKey = this.apiKey; // 商户密钥
            // string apiCode = "AG"; // 接口标识，可以根据需要选择AG或BBIN

            // 3. 构建请求URL
            string url = $"{apiUrl}/ley/register";

            // 4. 使用RestSharp构建POST请求
            var client = new RestClient(url);
            var request = new RestRequest("", Method.Post);

            // 5. 设置请求头为application/x-www-form-urlencoded
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

            // 6. 添加表单参数
            request.AddParameter("account", account);
            request.AddParameter("api_key", apiKey);
            request.AddParameter("api_code", apiCode);
            request.AddParameter("username", player_name);
            request.AddParameter("password", "123456"); // 默认密码，可以根据需要修改

            _logger.LogInformation($"MS游戏用户注册开始 - 玩家名称: {player_name}, 接口标识: {apiCode}");

            // 7. 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            // 8. 处理响应
            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                try
                {
                    // 记录响应内容用于调试
                    _logger.LogInformation("MS游戏API注册响应: {ResponseContent}", response.Content);

                    // 解析JSON响应
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查返回码
                    int code = jsonResponse["Code"]?.Value<int>() ?? -1;
                    string message = jsonResponse["Message"]?.ToString() ?? "未知错误";

                    if (code == 0)
                    {
                        // 注册成功
                        _logger.LogInformation("MS游戏用户注册成功 - 玩家名称: {PlayerName}", player_name);
                        return true;
                    }
                    else
                    {
                        // 注册失败，记录具体错误信息
                        _logger.LogInformation($"MS游戏用户注册失败 - 玩家名称: {player_name}, 接口标识: {apiCode}, 错误码: {code}, 错误信息: {message}");
                        _logger.LogWarning("MS游戏用户注册失败 - 玩家名称: {PlayerName}, 错误码: {Code}, 错误信息: {Message}",
                            player_name, code, message);

                        // 特殊处理：如果用户已存在，也返回true（避免重复注册）
                        if (code == 33) // Member already exists
                        {
                            _logger.LogInformation("MS游戏用户已存在，视为注册成功 - 玩家名称: {PlayerName}", player_name);
                            return true;
                        }

                        return false;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "MS游戏API注册响应解析失败 - 玩家名称: {PlayerName}, 响应内容: {ResponseContent}",
                        player_name, response.Content);
                    return false;
                }
            }
            else
            {
                // HTTP请求失败
                _logger.LogError("MS游戏API注册请求失败 - 玩家名称: {PlayerName}, HTTP状态码: {StatusCode}, 错误信息: {ErrorMessage}",
                    player_name, response.StatusCode, response.ErrorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            // 捕获所有其他异常
            _logger.LogError(ex, "MS游戏用户注册发生异常 - 玩家名称: {PlayerName}", player_name);
            return false;
        }
    }


    /// <summary>
    /// MS 游戏入口
    /// </summary>
    /// <param name="player_name">玩家名称（须与 UserRegister 的 username 一致，用于 MS 登录）</param>
    /// <param name="gameCode">游戏代码</param>
    /// <param name="apiCode">接口标识</param>
    /// <param name="gametype">游戏类型</param>
    /// <returns>游戏登录URL，失败时返回空字符串</returns>
    public async Task<string> GetGameUrl(string player_name, string gameCode, string apiCode, GameType gametype)
    {
        if (IsDebugEnvironment())
        {
            _logger.LogInformation(
                "MS游戏登录走调试模拟 - 玩家名称: {PlayerName}, 游戏代码: {GameCode}, 接口标识: {ApiCode}, 返回链接: {DebugLaunchUrl}",
                player_name, gameCode, apiCode, DebugLaunchUrl);
            return DebugLaunchUrl;
        }

        try
        {
            // 1. 参数验证
            if (string.IsNullOrEmpty(player_name))
            {
                _logger.LogWarning("MS游戏登录失败：玩家名称不能为空");
                return "";
            }

            if (string.IsNullOrEmpty(gameCode))
            {
                _logger.LogWarning("MS游戏登录失败：游戏代码不能为空");
                return "";
            }
            if (string.IsNullOrEmpty(apiCode))
            {
                _logger.LogWarning("MS游戏登录失败：接口标识不能为空");
                return "";
            }

            // 2. 构建请求参数（username 必须与 UserRegister 一致，使用 player_name）
            string account = this.account; // 商户账号
            string apiKey = this.apiKey; // 商户密钥
            string username = player_name;
            int gameType = (int)gametype; // 根据游戏ID获取游戏类型
            int isMobile = 0; // 默认电脑版，可以根据需要调整

            // 3. 构建请求URL
            string url = $"{apiUrl}/ley/login";

            // 4. 使用RestSharp构建POST请求
            var client = new RestClient(url);
            var request = new RestRequest("", Method.Post);

            // 5. 设置请求头为application/x-www-form-urlencoded
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

            // 6. 添加表单参数
            request.AddParameter("account", account);
            request.AddParameter("api_key", apiKey);
            request.AddParameter("api_code", apiCode);
            request.AddParameter("username", username);
            request.AddParameter("gameType", gameType);
            request.AddParameter("gameCode", gameCode);
            request.AddParameter("isMobile", isMobile);

            _logger.LogInformation($"MS游戏登录开始 - 玩家名称: {player_name}, 游戏类型: {gameType}, 游戏代码: {gameCode}, 接口标识: {apiCode}");

            // 7. 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            // 8. 处理响应
            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                try
                {

                    // 记录响应内容用于调试
                    _logger.LogInformation("MS游戏API登录响应: {ResponseContent}", response.Content);

                    // 解析JSON响应
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查返回码
                    int code = jsonResponse["Code"]?.Value<int>() ?? -1;
                    string message = jsonResponse["Message"]?.ToString() ?? "未知错误";

                    if (code == 0)
                    {
                        // 登录成功，获取游戏URL
                        var data = jsonResponse["Data"];
                        string gameUrl = data?["url"]?.ToString() ?? "";

                        if (!string.IsNullOrEmpty(gameUrl))
                        {
                            _logger.LogInformation($"MS游戏登录成功 - 玩家名称: {player_name}, 游戏URL: {gameUrl}");
                            return gameUrl;
                        }
                        else
                        {
                            _logger.LogWarning($"MS游戏登录响应中未包含游戏URL - 玩家名称: {player_name}");
                            return "";
                        }
                    }
                    else
                    {
                        // 登录失败，记录具体错误信息
                        _logger.LogWarning($"MS游戏登录失败 - 玩家名称: {player_name}, 错误码: {code}, 错误信息: {message}");

                        // 特殊处理：如果会员未注册，先尝试注册
                        if (code == 34) // No user exists, please register
                        {
                            _logger.LogInformation("MS游戏会员未注册，尝试先注册 - 玩家名称: {PlayerName}", player_name);

                            // 先注册用户
                            bool registerResult = await UserRegister(player_name, apiCode);
                            if (registerResult)
                            {
                                _logger.LogInformation($"MS游戏用户注册成功，重新尝试登录 - 玩家名称: {player_name}");

                                // 递归调用自己，重新尝试登录
                                return await GetGameUrl(player_name, gameCode, apiCode, gametype);
                            }
                            else
                            {
                                _logger.LogError("MS游戏用户注册失败，无法登录游戏 - 玩家名称: {PlayerName}", player_name);
                                return "";
                            }
                        }

                        return "";
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, $"MS游戏API登录响应解析失败 - 玩家名称: {player_name}, 响应内容: {response.Content}");
                    return "";
                }
            }
            else
            {
                // HTTP请求失败
                _logger.LogError($"MS游戏API登录请求失败 - 玩家名称: {player_name}, HTTP状态码: {response.StatusCode}, 错误信息: {response.ErrorMessage}");
                return "";
            }
        }
        catch (Exception ex)
        {
            // 捕获所有其他异常
            _logger.LogError(ex, $"MS游戏登录发生异常 - 玩家名称: {player_name}");
            return "";
        }
    }



    /// <summary>
    /// MS 上分
    /// </summary>
    /// <param name="player_name">玩家名称（与注册/login 的 username 一致）</param>
    /// <param name="amount">充值金额（整数）</param>
    /// <param name="orderId">转账订单号：须为正整数的十进制字符串（MS 接口 transferno / transid）</param>
    /// <returns>上分结果：(是否成功, 错误信息)</returns>
    public async Task<(bool success, string errorMessage)> PlayerDeposit(string player_name, decimal amount, string orderId, string apiCode)
    {
        if (IsDebugEnvironment())
        {
            _logger.LogInformation(
                "MS游戏上分走调试模拟 - 玩家名称: {PlayerName}, 金额: {Amount}, 订单号: {OrderId}, 接口标识: {ApiCode}",
                player_name, amount, orderId, apiCode);
            return (true, string.Empty);
        }

        try
        {
            // 1. 参数验证
            if (string.IsNullOrEmpty(player_name))
            {
                _logger.LogWarning("MS游戏玩家上分失败：玩家ID不能为空");
                return (false, "玩家ID不能为空");
            }

            if (amount <= 0)
            {
                _logger.LogWarning("MS游戏玩家上分失败：充值金额必须大于0，当前金额: {Amount}", amount);
                return (false, "充值金额必须大于0");
            }

            if (string.IsNullOrEmpty(orderId))
            {
                _logger.LogWarning("MS游戏玩家上分失败：订单ID不能为空");
                return (false, "订单ID不能为空");
            }

            // MS 要求 transferno（transid）为正整数
            if (!ulong.TryParse(orderId.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var transferNoU) || transferNoU == 0)
            {
                _logger.LogWarning("MS游戏玩家上分失败：transid 须为正整数，当前: {OrderId}", orderId);
                return (false, "transid 须为正整数");
            }

            // 验证金额必须为整数，如果不是整数则舍掉小数部分
            if (amount != Math.Floor(amount))
            {
                _logger.LogWarning("MS游戏玩家上分：充值金额包含小数部分，将舍掉小数部分。原金额: {OriginalAmount}, 处理后金额: {ProcessedAmount}", amount, Math.Floor(amount));
                amount = Math.Floor(amount);
            }

            // 2. 构建请求参数
            string account = this.account; // 商户账号
            string apiKey = this.apiKey; // 商户密钥
            string username = player_name; // 使用玩家ID作为用户名
            int transferAmount = (int)amount; // 转换为整数
            string transferno = transferNoU.ToString(CultureInfo.InvariantCulture);

            // 3. 构建请求URL
            string url = $"{apiUrl}/ley/deposit";

            // 4. 使用RestSharp构建POST请求
            var client = new RestClient(url);
            var request = new RestRequest("", Method.Post);

            // 5. 设置请求头为application/x-www-form-urlencoded
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

            // 6. 添加表单参数
            request.AddParameter("account", account);
            request.AddParameter("api_key", apiKey);
            request.AddParameter("api_code", apiCode);
            request.AddParameter("username", username);
            request.AddParameter("amount", transferAmount);
            request.AddParameter("transferno", transferno);

            _logger.LogInformation("MS游戏玩家上分开始 - 玩家ID: {PlayerId}, 充值金额: {Amount}, 订单号: {OrderId}, 接口标识: {ApiCode}",
                player_name, transferAmount, orderId, apiCode);

            // 7. 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            // 8. 处理响应
            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                try
                {
                    // 记录响应内容用于调试
                    _logger.LogInformation("MS游戏API上分响应: {ResponseContent}", response.Content);

                    // 解析JSON响应
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查返回码
                    int code = jsonResponse["Code"]?.Value<int>() ?? -1;
                    string message = jsonResponse["Message"]?.ToString() ?? "未知错误";

                    if (code == 0)
                    {
                        // 上分成功
                        var data = jsonResponse["Data"];
                        int depositAmount = data?["deposit"]?.Value<int>() ?? 0;

                        _logger.LogInformation("MS游戏玩家上分成功 - 玩家名称: {PlayerName}, 充值金额: {Amount}, 实际到账: {DepositAmount}, 订单号: {OrderId}",
                            player_name, transferAmount, depositAmount, orderId);
                        return (true, "");
                    }
                    else
                    {
                        _logger.LogWarning("MS游戏玩家上分失败 - 玩家名称: {PlayerName}, 充值金额: {Amount}, 订单号: {OrderId}, 错误码: {Code}, 错误信息: {Message}",
                            player_name, transferAmount, orderId, code, message);
                        return (false, $"游戏平台返回错误({code}): {message}");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "MS游戏API上分响应解析失败 - 玩家ID: {PlayerId}, 订单号: {OrderId}, 响应内容: {ResponseContent}",
                        player_name, orderId, response.Content);
                    return (false, "游戏平台响应解析失败");
                }
            }
            else
            {
                _logger.LogError("MS游戏API上分请求失败 - 玩家ID: {PlayerId}, 订单号: {OrderId}, HTTP状态码: {StatusCode}, 错误信息: {ErrorMessage}",
                    player_name, orderId, response.StatusCode, response.ErrorMessage);
                return (false, $"游戏平台请求失败(HTTP {(int)response.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MS游戏玩家上分发生异常 - 玩家ID: {PlayerId}, 充值金额: {Amount}, 订单号: {OrderId}",
                player_name, amount, orderId);
            return (false, $"游戏平台异常: {ex.Message}");
        }
    }



    /// <summary>
    /// MS 下分
    /// </summary>
    /// <param name="player_name">玩家名称（与注册/login 的 username 一致）</param>
    /// <param name="amount">提现金额（整数）</param>
    /// <param name="orderId">转账订单号：须为正整数的十进制字符串（MS 接口 transferno / transid）</param>
    /// <returns>下分结果，true表示成功，false表示失败</returns>
    public async Task<bool> PlayerWithdraw(string player_name, decimal amount, string orderId, string apiCode)
    {
        if (IsDebugEnvironment())
        {
            _logger.LogInformation(
                "MS游戏下分走调试模拟 - 玩家名称: {PlayerName}, 金额: {Amount}, 订单号: {OrderId}, 接口标识: {ApiCode}",
                player_name, amount, orderId, apiCode);
            return true;
        }

        try
        {
            // 1. 参数验证
            if (string.IsNullOrEmpty(player_name))
            {
                _logger.LogWarning("MS游戏玩家下分失败：玩家名称不能为空");
                return false;
            }

            if (amount <= 0)
            {
                _logger.LogWarning("MS游戏玩家下分失败：提现金额必须大于0，当前金额: {Amount}", amount);
                return false;
            }

            if (string.IsNullOrEmpty(orderId))
            {
                _logger.LogWarning("MS游戏玩家下分失败：订单ID不能为空");
                return false;
            }

            if (!ulong.TryParse(orderId.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var transferNoU) || transferNoU == 0)
            {
                _logger.LogWarning("MS游戏玩家下分失败：transid 须为正整数，当前: {OrderId}", orderId);
                return false;
            }

            // 验证金额必须为整数，如果不是整数则舍掉小数部分
            if (amount != Math.Floor(amount))
            {
                _logger.LogWarning("MS游戏玩家下分：提现金额包含小数部分，将舍掉小数部分。原金额: {OriginalAmount}, 处理后金额: {ProcessedAmount}",
                    amount, Math.Floor(amount));
                // 舍掉小数部分
                amount = Math.Floor(amount);
            }

            // 2. 构建请求参数
            string account = this.account; // 商户账号
            string apiKey = this.apiKey; // 商户密钥
            string username = player_name; // 使用玩家名称作为用户名
            int transferAmount = (int)amount; // 转换为整数
            string transferno = transferNoU.ToString(CultureInfo.InvariantCulture);

            // 3. 构建请求URL
            string url = $"{apiUrl}/ley/withdrawal";

            // 4. 使用RestSharp构建POST请求
            var client = new RestClient(url);
            var request = new RestRequest("", Method.Post);

            // 5. 设置请求头为application/x-www-form-urlencoded
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

            // 6. 添加表单参数
            request.AddParameter("account", account);
            request.AddParameter("api_key", apiKey);
            request.AddParameter("api_code", apiCode);
            request.AddParameter("username", username);
            request.AddParameter("amount", transferAmount);
            request.AddParameter("transferno", transferno);

            _logger.LogInformation("MS游戏玩家下分开始 - 玩家ID: {PlayerId}, 提现金额: {Amount}, 订单号: {OrderId}, 接口标识: {ApiCode}",
                player_name, transferAmount, orderId, apiCode);

            // 7. 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            // 8. 处理响应
            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                try
                {
                    // 记录响应内容用于调试
                    _logger.LogInformation("MS游戏API下分响应: {ResponseContent}", response.Content);

                    // 解析JSON响应
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查返回码
                    int code = jsonResponse["Code"]?.Value<int>() ?? -1;
                    string message = jsonResponse["Message"]?.ToString() ?? "未知错误";

                    if (code == 0)
                    {
                        // 下分成功
                        var data = jsonResponse["Data"];
                        int withdrawalAmount = data?["withdrawal"]?.Value<int>() ?? 0;

                        _logger.LogInformation("MS游戏玩家下分成功 - 玩家ID: {PlayerId}, 提现金额: {Amount}, 实际到账: {WithdrawalAmount}, 订单号: {OrderId}",
                            player_name, transferAmount, withdrawalAmount, orderId);
                        return true;
                    }
                    else
                    {
                        // 下分失败，记录具体错误信息
                        _logger.LogWarning("MS游戏玩家下分失败 - 玩家ID: {PlayerId}, 提现金额: {Amount}, 订单号: {OrderId}, 错误码: {Code}, 错误信息: {Message}",
                            player_name, transferAmount, orderId, code, message);
                        return false;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "MS游戏API下分响应解析失败 - 玩家ID: {PlayerId}, 订单号: {OrderId}, 响应内容: {ResponseContent}",
                        player_name, orderId, response.Content);
                    return false;
                }
            }
            else
            {
                // HTTP请求失败
                _logger.LogError("MS游戏API下分请求失败 - 玩家ID: {PlayerId}, 订单号: {OrderId}, HTTP状态码: {StatusCode}, 错误信息: {ErrorMessage}",
                    player_name, orderId, response.StatusCode, response.ErrorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            // 捕获所有其他异常
            _logger.LogError(ex, "MS游戏玩家下分发生异常 - 玩家ID: {PlayerId}, 提现金额: {Amount}, 订单号: {OrderId}",
                player_name, amount, orderId);
            return false;
        }
    }


    /// <summary>
    /// MS 查余额
    /// </summary>
    /// <param name="player_name">玩家名称（与注册/login 的 username 一致）</param>
    /// <param name="apiCode">平台代码</param>
    /// <returns>玩家余额，失败时返回-1</returns>
    public async Task<decimal> GetPlayerBalance(string player_name, string apiCode)
    {
        if (IsDebugEnvironment())
        {
            _logger.LogInformation(
                "MS游戏查询余额走调试模拟 - 玩家名称: {PlayerName}, 接口标识: {ApiCode}, 返回余额: {Balance}",
                player_name, apiCode, DebugWalletBalance);
            return DebugWalletBalance;
        }

        try
        {
            // 1. 参数验证
            if (string.IsNullOrEmpty(player_name))
            {
                _logger.LogWarning("MS游戏查询玩家余额失败：玩家ID不能为空");
                return -1;
            }

            if (string.IsNullOrEmpty(apiCode))
            {
                _logger.LogWarning("MS游戏查询玩家余额失败：平台代码不能为空");
                return -1;
            }

            // 2. 构建请求参数
            string account = this.account; // 商户账号
            string apiKey = this.apiKey; // 商户密钥
            string username = player_name; // 使用玩家ID作为用户名

            // 3. 构建请求URL
            string url = $"{apiUrl}/ley/balance";

            // 4. 使用RestSharp构建POST请求
            var client = new RestClient(url);
            var request = new RestRequest("", Method.Post);

            // 5. 设置请求头为application/x-www-form-urlencoded
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

            // 6. 添加表单参数
            request.AddParameter("account", account);
            request.AddParameter("api_key", apiKey);
            request.AddParameter("api_code", apiCode);
            request.AddParameter("username", username);

            _logger.LogInformation("MS游戏查询玩家余额开始 - 玩家ID: {PlayerId}, 接口标识: {ApiCode}",
                player_name, apiCode);

            // 7. 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            // 8. 处理响应
            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                try
                {
                    // 记录响应内容用于调试
                    _logger.LogInformation("MS游戏API查询余额响应: {ResponseContent}", response.Content);

                    // 解析JSON响应
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查返回码
                    int code = jsonResponse["Code"]?.Value<int>() ?? -1;
                    string message = jsonResponse["Message"]?.ToString() ?? "未知错误";

                    if (code == 0)
                    {
                        // 查询成功，获取余额
                        var data = jsonResponse["Data"];
                        decimal balance = data?["balance"]?.Value<decimal>() ?? 0;

                        _logger.LogInformation("MS游戏查询玩家余额成功 - 玩家ID: {PlayerId}, 余额: {Balance}",
                            player_name, balance);
                        return balance;
                    }
                    else
                    {
                        // 查询失败，记录具体错误信息
                        _logger.LogWarning("MS游戏查询玩家余额失败 - 玩家ID: {PlayerId}, 错误码: {Code}, 错误信息: {Message}",
                            player_name, code, message);
                        return -1;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "MS游戏API查询余额响应解析失败 - 玩家ID: {PlayerId}, 响应内容: {ResponseContent}",
                        player_name, response.Content);
                    return -1;
                }
            }
            else
            {
                // HTTP请求失败
                _logger.LogError("MS游戏API查询余额请求失败 - 玩家ID: {PlayerId}, HTTP状态码: {StatusCode}, 错误信息: {ErrorMessage}",
                    player_name, response.StatusCode, response.ErrorMessage);
                return -1;
            }
        }
        catch (Exception ex)
        {
            // 捕获所有其他异常
            _logger.LogError(ex, "MS游戏查询玩家余额发生异常 - 玩家ID: {PlayerId}",
                player_name);
            return -1;
        }
    }

    /// <summary>从 MS <c>/ley/gamerecord</c> 的 <c>Data</c> 节点解析注单行列表（兼容 Data 为数组、<c>data</c>/<c>Data</c>/<c>list</c> 等，以及跳过分页元数据后取首个数组）。</summary>
    private static JArray? ResolveMsGamerecordBetList(JToken? dataNode)
    {
        if (dataNode == null || dataNode.Type == JTokenType.Null)
            return null;
        if (dataNode is JArray direct)
            return direct;
        if (dataNode is not JObject inner)
            return null;

        static JArray? CoerceArray(JToken? t)
        {
            if (t is JArray ja)
                return ja;
            if (t?.Type == JTokenType.String)
            {
                var s = t.Value<string>();
                if (string.IsNullOrWhiteSpace(s))
                    return null;
                try
                {
                    return JArray.Parse(s!);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        foreach (var key in new[] { "data", "Data", "list", "records", "rows", "items" })
        {
            var arr = CoerceArray(inner[key]);
            if (arr != null)
                return arr;
        }

        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "total_count", "lastPage", "pageSize", "page", "method", "current_page",
            "first_page_url", "from", "last_page", "last_page_url", "next_page_url",
            "path", "per_page", "prev_page_url", "to", "total",
        };

        foreach (var p in inner.Properties())
        {
            if (skip.Contains(p.Name))
                continue;
            if (p.Value is JArray ja)
                return ja;
        }

        return null;
    }

    /// <summary>
    /// MS 注单历史
    /// </summary>
    /// <param name="player_name">玩家名称（可选；与 MS 注册 username 一致；非空时会作为 <c>username</c> 表单参数参与 gamerecord 查询，与 XH 行为对齐）</param>
    /// <param name="from">查询开始时间（北京时间墙上时钟，格式：2024-04-29 06:39:20；与 MS <c>start_at</c> 一致按 Asia/Shanghai 转 Unix 秒）</param>
    /// <param name="to">查询结束时间（同上）</param>
    /// <param name="limit">查询条数（单次查询不可超过500条）</param>
    /// <param name="page">页数</param>
    /// <returns>注单历史记录列表</returns>
    public async Task<List<DTransAction>> GetBetHistory(string? player_name, string from, string to, int limit, int page)
    {
        try
        {
            // 1. 参数验证
            if (string.IsNullOrEmpty(from))
            {
                _logger.LogWarning("MS游戏获取注单历史失败：开始时间不能为空");
                return new List<DTransAction>();
            }

            if (string.IsNullOrEmpty(to))
            {
                _logger.LogWarning("MS游戏获取注单历史失败：结束时间不能为空");
                return new List<DTransAction>();
            }

            // 验证每页条数限制（最大500条）
            if (limit > 500)
            {
                _logger.LogWarning("MS游戏获取注单历史失败：每页条数不能超过500条，当前: {Limit}", limit);
                limit = 500; // 自动调整为最大限制
            }

            if (limit <= 0)
            {
                _logger.LogWarning("MS游戏获取注单历史失败：每页条数必须大于0，当前: {Limit}", limit);
                return new List<DTransAction>();
            }

            if (page <= 0)
            {
                _logger.LogWarning("MS游戏获取注单历史失败：页数必须大于0，当前: {Page}", page);
                return new List<DTransAction>();
            }

            // 2. 构建请求参数
            string account = this.account; // 商户账号
            string apiKey = this.apiKey; // 商户密钥
            string method = "betTime"; // 根据投注时间采集，也可以选择updateTime根据修改时间采集

            // 3. 时间格式转换：按北京时间墙上时钟解读，再转 Unix 秒
            DateTime startDateTime;
            DateTime endDateTime;

            if (!DateTime.TryParse(from, out startDateTime))
            {
                _logger.LogWarning("MS游戏获取注单历史失败：开始时间格式不正确，格式应为：2024-04-29 06:39:20，当前: {From}", from);
                return new List<DTransAction>();
            }

            if (!DateTime.TryParse(to, out endDateTime))
            {
                _logger.LogWarning("MS游戏获取注单历史失败：结束时间格式不正确，格式应为：2024-04-29 06:39:20，当前: {To}", to);
                return new List<DTransAction>();
            }

            long startTimestamp = TimeHelper.BeijingToUnix(startDateTime);
            long endTimestamp = TimeHelper.BeijingToUnix(endDateTime);

            if (endTimestamp < startTimestamp)
            {
                _logger.LogWarning("MS游戏获取注单历史失败：结束时间不能早于开始时间 - From: {From}, To: {To}", from, to);
                return new List<DTransAction>();
            }

            // 4. 构建请求URL
            string url = $"{apiUrl}/ley/gamerecord";

            // 5. 使用RestSharp构建POST请求
            var client = new RestClient(url);
            var request = new RestRequest("", Method.Post);

            // 6. 设置请求头为application/x-www-form-urlencoded
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

            // 7. 添加表单参数
            request.AddParameter("account", account);
            request.AddParameter("api_key", apiKey);
            request.AddParameter("method", method);
            request.AddParameter("page", page);
            request.AddParameter("pageSize", limit);
            request.AddParameter("start_at", startTimestamp);
            request.AddParameter("end_at", endTimestamp);
            if (!string.IsNullOrWhiteSpace(player_name))
                request.AddParameter("username", player_name.Trim());

            _logger.LogInformation("MS游戏获取注单历史开始 - 玩家ID: {PlayerId}, 开始时间: {From}, 结束时间: {To}, 页数: {Page}, 每页条数: {Limit}",
                player_name ?? "全部", from, to, page, limit);

            // 8. 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            // 9. 处理响应
            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                try
                {
                    // 记录响应内容用于调试
                    _logger.LogInformation("MS游戏API获取注单历史响应: {ResponseContent}", response.Content);

                    // 解析JSON响应
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查返回码
                    int code = jsonResponse["Code"]?.Value<int>() ?? -1;
                    string message = jsonResponse["Message"]?.ToString() ?? "未知错误";

                    if (code == 0)
                    {
                        // 查询成功，解析注单数据
                        var data = jsonResponse["Data"];
                        var betRecords = ResolveMsGamerecordBetList(data);

                        if (betRecords != null && betRecords.Count > 0)
                        {
                            List<DTransAction> transActions = new List<DTransAction>();
                            var okBetRecords = new List<JToken>();

                            foreach (var betRecord in betRecords)
                            {
                                try
                                {
                                    // 将API返回的注单数据转换为DTransAction对象
                                    DTransAction transAction = ConvertBetRecordToTransAction(betRecord);
                                    transActions.Add(transAction);
                                    okBetRecords.Add(betRecord);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "MS游戏注单数据转换失败 - 注单ID: {BetId}", betRecord["id"]?.ToString());
                                    // 继续处理其他记录，不中断整个流程
                                }
                            }

                            await ApplyMemberResolutionFromBetRecordsAsync(transActions, okBetRecords);

                            _logger.LogInformation("MS游戏获取注单历史成功 - 玩家ID: {PlayerId}, 获取到 {Count} 条记录",
                                player_name ?? "全部", transActions.Count);
                            return transActions;
                        }
                        else
                        {
                            _logger.LogInformation("MS游戏获取注单历史成功 - 玩家ID: {PlayerId}, 未找到符合条件的记录",
                                player_name ?? "全部");
                            return new List<DTransAction>();
                        }
                    }
                    else
                    {
                        // 查询失败，记录具体错误信息
                        _logger.LogWarning("MS游戏获取注单历史失败 - 玩家ID: {PlayerId}, 错误码: {Code}, 错误信息: {Message}",
                            player_name ?? "全部", code, message);
                        return new List<DTransAction>();
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "MS游戏API获取注单历史响应解析失败 - 玩家ID: {PlayerId}, 响应内容: {ResponseContent}",
                        player_name ?? "全部", response.Content);
                    return new List<DTransAction>();
                }
            }
            else
            {
                // HTTP请求失败
                _logger.LogError("MS游戏API获取注单历史请求失败 - 玩家ID: {PlayerId}, HTTP状态码: {StatusCode}, 错误信息: {ErrorMessage}",
                    player_name ?? "全部", response.StatusCode, response.ErrorMessage);
                return new List<DTransAction>();
            }
        }
        catch (Exception ex)
        {
            // 捕获所有其他异常
            _logger.LogError(ex, "MS游戏获取注单历史发生异常 - 玩家ID: {PlayerId}, 开始时间: {From}, 结束时间: {To}",
                player_name ?? "全部", from, to);
            return new List<DTransAction>();
        }
    }

    /// <summary>
    /// 分页调用 <see cref="GetBetHistory"/>，将 MS 注单写入本地表（按 <see cref="DTransAction.SerialNumber"/> / rowid 去重，已存在则更新）。
    /// </summary>
    /// <param name="filterUsername">可选：仅落库该登录名对应会员的注单（需在本地 SysUser 存在）</param>
    /// <param name="from">开始时间</param>
    /// <param name="to">结束时间</param>
    /// <param name="pageSize">每页条数，最大 500</param>
    /// <param name="maxPages">最多拉取页数，防止异常死循环</param>
    public async Task<MSBetHistorySyncResult> SyncBetHistoryToDatabaseAsync(
        string? filterUsername,
        string from,
        string to,
        int pageSize = 500,
        int maxPages = 500)
    {
        var result = new MSBetHistorySyncResult();
        try
        {
            long? onlyMemberId = null;
            if (!string.IsNullOrWhiteSpace(filterUsername))
            {
                var m = await _fsql.Select<DMember>().Where(u => u.Username == filterUsername.Trim()).FirstAsync();
                if (m == null)
                {
                    result.Success = false;
                    result.Message = $"未找到登录名为「{filterUsername.Trim()}」的会员";
                    return result;
                }

                onlyMemberId = m.Id;
            }

            var page = 1;
            while (page <= maxPages)
            {
                var rawBatch = await GetBetHistory(filterUsername, from, to, pageSize, page);
                if (rawBatch.Count == 0)
                    break;

                var batch = onlyMemberId.HasValue
                    ? rawBatch.Where(b => b.DMemberId == onlyMemberId.Value).ToList()
                    : rawBatch;

                result.RemoteFetched += rawBatch.Count;
                result.PagesDone++;

                var deduped = batch
                    .Where(x => !string.IsNullOrWhiteSpace(x.SerialNumber))
                    .GroupBy(x => x.SerialNumber!, StringComparer.Ordinal)
                    .Select(g => g.Last())
                    .ToList();

                foreach (var row in batch.Where(x => string.IsNullOrWhiteSpace(x.SerialNumber)))
                    result.SkippedNoSerial++;

                var serials = deduped.Select(x => x.SerialNumber!).Distinct().ToList();
                var existing = serials.Count == 0
                    ? new List<DTransAction>()
                    : await _fsql.Select<DTransAction>().Where(x => serials.Contains(x.SerialNumber)).ToListAsync();
                var existDict = existing.ToDictionary(x => x.SerialNumber, StringComparer.Ordinal);

                var toInsert = new List<DTransAction>();
                var toUpdate = new List<DTransAction>();

                foreach (var row in deduped)
                {
                    if (existDict.TryGetValue(row.SerialNumber, out var old))
                    {
                        old.BetAmount = row.BetAmount;
                        old.ActualAmount = row.ActualAmount;
                        old.Status = row.Status;
                        old.Description = row.Description;
                        old.Data = row.Data ?? "";

                        old.TransactionTime = row.TransactionTime;

                        old.GameRound = row.GameRound ?? "";
                        old.CurrencyCode = string.IsNullOrWhiteSpace(row.CurrencyCode) ? "CNY" : row.CurrencyCode;
                        if (row.DMemberId > 0)
                        {
                            old.DMemberId = row.DMemberId;
                            old.DAgentId = row.DAgentId;
                        }

                        old.ModifiedTime = DateTime.Now;
                        old.ModifiedUserName = "MS注单同步";
                        toUpdate.Add(old);
                    }
                    else
                    {



                        row.CreatedTime = DateTime.Now;
                        row.CreatedUserName = "MS注单同步";
                        row.ModifiedTime = row.CreatedTime;
                        row.ModifiedUserName = "MS注单同步";
                        toInsert.Add(row);
                    }
                }

                using (var uow = _fsql.CreateUnitOfWork())
                {
                    var rep = uow.GetRepository<DTransAction>();
                    foreach (var row in toInsert)
                        await rep.InsertAsync(row);

                    foreach (var old in toUpdate)
                        await rep.UpdateAsync(old);

                    uow.Commit();
                }

                result.Inserted += toInsert.Count;
                result.Updated += toUpdate.Count;

                if (rawBatch.Count < pageSize)
                    break;

                page++;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MS 注单同步落库异常");
            result.Success = false;
            result.Message = ex.Message;
            return result;
        }
    }

    private static string? ReadMsBetUsername(JToken betRecord)
    {
        static string? Pick(params string?[] xs)
        {
            foreach (var s in xs)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    return s.Trim();
            }

            return null;
        }

        return Pick(
            betRecord["username"]?.ToString(),
            betRecord["Username"]?.ToString(),
            betRecord["userName"]?.ToString(),
            betRecord["memberName"]?.ToString(),
            betRecord["MemberName"]?.ToString(),
            betRecord["account"]?.ToString(),
            betRecord["loginName"]?.ToString());
    }

    private async Task ApplyMemberResolutionFromBetRecordsAsync(List<DTransAction> transActions, IReadOnlyList<JToken> betRecords)
    {
        var n = Math.Min(transActions.Count, betRecords.Count);
        if (n == 0)
            return;

        var names = new List<string?>(n);
        for (var i = 0; i < n; i++)
            names.Add(ReadMsBetUsername(betRecords[i]));

        var distinct = names.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct().ToList();
        if (distinct.Count == 0)
            return;

        var members = await _fsql.Select<DMember>().Where(m => distinct.Contains(m.Username)).ToListAsync();
        var dict = members.ToDictionary(m => m.Username, m => m, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < n; i++)
        {
            var name = names[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;
            if (dict.TryGetValue(name.Trim(), out var member))
            {
                transActions[i].DMemberId = member.Id;
                transActions[i].DAgentId = member.DAgentId;
            }
        }
    }

    /// <summary>
    /// 注单转流水
    /// </summary>
    /// <param name="betRecord">API返回的注单JSON对象</param>
    /// <returns>转换后的DTransAction对象</returns>
    private DTransAction ConvertBetRecordToTransAction(JToken betRecord)
    {
        // 解析投注金额
        decimal betAmount = 0;
        if (decimal.TryParse(betRecord["betAmount"]?.ToString(), out decimal parsedBetAmount))
        {
            betAmount = parsedBetAmount;
        }

        // 解析输赢金额
        decimal netAmount = 0;
        if (decimal.TryParse(betRecord["netAmount"]?.ToString(), out decimal parsedNetAmount))
        {
            netAmount = parsedNetAmount;
        }

        var transactionTimeUnix = ReadBetTimeAsLong(betRecord);

        // 解析结算状态
        TransactionStatus status = TransactionStatus.Pending;
        if (int.TryParse(betRecord["status"]?.ToString(), out int statusCode))
        {
            status = statusCode switch
            {
                1 => TransactionStatus.Success,    // 已结算
                2 => TransactionStatus.Pending,   // 未结算
                3 => TransactionStatus.Cancelled, // 无效订单
                4 => TransactionStatus.Refunded,  // 已退款
                _ => TransactionStatus.Pending
            };
        }

        // 构建交易描述
        string description = $"游戏类型: {betRecord["gameType"]?.ToString()}, " +
                           $"游戏玩法: {betRecord["playType"]?.ToString()}, " +
                           $"投注内容: {betRecord["xzhm"]?.ToString()}, " +
                           $"赔率: {betRecord["Odds"]?.ToString()}, " +
                           $"局号: {betRecord["roundNo"]?.ToString()}";

        // 创建DTransAction对象
        DTransAction transAction = new DTransAction
        {
            TransactionType = TransactionType.Bet,           // 投注类型
            BetAmount = betAmount,                           // 投注金额
            ActualAmount = netAmount,                        // 输赢金额（不包含本金）
            CurrencyCode = "CNY",                           // 货币代码
            SerialNumber = betRecord["rowid"]?.ToString() ?? "", // 注单单号
            GameRound = betRecord["roundNo"]?.ToString() ?? "",   // 游戏局号
            TransactionTime = transactionTimeUnix,         // 投注时间（Unix 秒；0 表示本次未解析到真实时间）
            Status = status,                                 // 结算状态
            Description = description,                       // 交易描述
            Data = betRecord["result"]?.ToString() ?? "",    // 详细数据
            IsRebate = false,                               // 默认未反水
            DMemberId = 0,                                  // 需要根据username查找对应的会员ID
            DGameId = 0,                                    // 需要根据游戏代码查找对应的游戏ID
            DAgentId = 0                                    // 需要根据会员查找对应的代理ID
        };

        return transAction;
    }

    private static long ReadBetTimeAsLong(JToken betRecord)
    {
        var text = betRecord["betTime"]?.ToString()
                   ?? betRecord["updateTime"]?.ToString()
                   ?? "0";

        return long.TryParse(text, out var value) ? value : 0L;
    }

    private async Task<long> ResolveMsGamePlatformIdAsync()
    {
        var ids = await _fsql.Select<DGamePlatform>()
            .Where(p => MsGamePlatformNames.Contains(p.Name))
            .ToListAsync(p => p.Id);
        if (ids.Count > 0)
        {
            return ids[0];
        }

        _logger.LogWarning(
            "未找到 MS 游戏平台记录，请先在「游戏平台」中配置名称：{Names}",
            string.Join("、", MsGamePlatformNames));
        return 0;
    }

    public async Task<MSGameListSyncResult> GetGameList(string apiCode)
    {
        try
        {
            string account = this.account;
            string apiKey = this.apiKey;

            string url = $"{apiUrl}/ley/gamelist";

            var client = new RestClient(url);
            var request = new RestRequest("", Method.Post);

            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

            request.AddParameter("account", account);
            request.AddParameter("api_key", apiKey);
            request.AddParameter("api_code", apiCode);

            RestResponse response = await client.ExecuteAsync(request);

            if (string.IsNullOrWhiteSpace(response.Content))
            {
                return new MSGameListSyncResult { Success = false, Message = "接口返回空内容" };
            }

            var jsonResponse = JObject.Parse(response.Content);

            // 检查返回码
            int code = jsonResponse["Code"]?.Value<int>() ?? -1;
            string message = jsonResponse["Message"]?.ToString() ?? "未知错误";

            if (code == 0)
            {
                // 查询成功，解析注单数据
                long gamePlatformId = await ResolveMsGamePlatformIdAsync();
                if (gamePlatformId == 0)
                {
                    return new MSGameListSyncResult
                    {
                        Success = false,
                        Message = "未配置 MS 游戏平台，请在「游戏平台」中新增名称：MS、MS游戏 或 美盛游戏 之一",
                    };
                }

                var data = jsonResponse["Data"];
                var betRecords = data?["gamelist"] as JArray;

                var gamesList = new List<DGame>();
                var result = new MSGameListSyncResult { Success = true };
                if (betRecords == null || betRecords.Count == 0)
                {
                    result.RemoteTotal = 0;
                    result.GamesJson = "[]";
                    return result;
                }

                result.RemoteTotal = betRecords.Count;
                foreach (var game in betRecords)
                {
                    Console.WriteLine(game.ToJson());
                    // 判断 GameCnName 是否存在,再加入到数据库
                    var gameCnName = game["name"]?.ToString() ?? "";
                    var enName = game["en_name"]?.ToString() ?? "";
                    var gameName = string.IsNullOrWhiteSpace(enName)
                        ? PinyinHelper.ToPinyinUpper(gameCnName)
                        : enName;
                    var tmp = new DGame()
                    {
                        GameUID = game["id"]?.ToString() ?? "",
                        GameCnName = gameCnName,
                        GameName = gameName,
                        GameCode = game["gameCode"]?.ToString() ?? "",
                        GameType = (GameType)int.Parse(game["gameType"]?.ToString() ?? "0"),
                        ApiCode = game["code"]?.ToString() ?? "",
                        Icon = game["img"]?.ToString() ?? "",
                        DGamePlatformId = gamePlatformId,
                        IsTestPassed = false,
                        TestTime = null,
                        IsEnabled = false,
                        IsRecommended = false,
                    };

                    gamesList.Add(tmp);


                    var exist = await _fsql.Select<DGame>()
                          .Where(g => g.DGamePlatformId == gamePlatformId)
                          .Where(g =>
                              (!string.IsNullOrEmpty(tmp.GameUID) && g.GameUID == tmp.GameUID) &&
                              (!string.IsNullOrEmpty(tmp.GameCode) && g.ApiCode == tmp.ApiCode && g.GameCode == tmp.GameCode) &&
                              g.GameCnName == tmp.GameCnName)
                          .FirstAsync();

                    if (exist == null)
                    {
                        await _fsql.GetRepository<DGame>().InsertAsync(tmp);
                        result.Inserted++;
                        if (!result.NewByGameType.ContainsKey(tmp.GameType))
                            result.NewByGameType[tmp.GameType] = 0;
                        result.NewByGameType[tmp.GameType]++;

                        var apiKeyLabel = string.IsNullOrWhiteSpace(tmp.ApiCode) ? "(空)" : tmp.ApiCode.Trim();
                        if (!result.NewByApiCode.ContainsKey(apiKeyLabel))
                            result.NewByApiCode[apiKeyLabel] = 0;
                        result.NewByApiCode[apiKeyLabel]++;
                    }
                    else
                    {
                        result.SkippedExisting++;
                    }
                }

                result.GamesJson = gamesList.ToJson();
                return result;
            }
            else
            {
                _logger.LogError("MS游戏获取游戏列表失败 - HTTP状态码: {StatusCode}, 错误信息: {ErrorMessage}",
                    response.StatusCode, response.ErrorMessage);
                return new MSGameListSyncResult { Success = false, Message = message };
            }


        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MS游戏获取游戏列表发生异常");
            return new MSGameListSyncResult { Success = false, Message = ex.Message };
        }
    }


}
