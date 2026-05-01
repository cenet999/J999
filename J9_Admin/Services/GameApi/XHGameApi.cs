using J9_Admin.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; // 用于JObject
using RestSharp;
using System.Globalization;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading;

namespace J9_Admin.Services.GameApi;

/// <summary>XH 同步游戏列表结果（远程条数、新增/跳过、按类型与接口代码分类统计）。</summary>
public class XHGameListSyncResult {
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
    /// <summary>本次完成的 gamelist 请求次数（单页模式为 1；全量模式为实际请求页数）</summary>
    public int PagesDone { get; set; }
}

/// <summary>XH 注单一键同步落库结果</summary>
public class XHBetHistorySyncResult {
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

public class XHGameApi {
    private static int _xhTransferSequence;
    private const long DefaultXhGamePlatformId = 0;
    private const string DebugLaunchUrl = "https://www.baidu.com";
    private const decimal DebugWalletBalance = 1000m;

    /// <summary>
    /// 统一 RestSharp 出站配置：部分环境下默认 HttpClient 对 XH 域名出现
    /// 「The SSL connection could not be established」时，显式启用 TLS1.2/1.3 常有助于握手成功。
    /// </summary>
    private static RestClient CreateXhRestClient(string url) {
        var options = new RestClientOptions(url)
        {
            Timeout = TimeSpan.FromSeconds(120),
            ConfigureMessageHandler = ApplyXhSslForRestSharp
        };
        return new RestClient(options);
    }

    private static HttpMessageHandler ApplyXhSslForRestSharp(HttpMessageHandler handler) {
        for (HttpMessageHandler? h = handler; h != null;) {
            if (h is SocketsHttpHandler sockets) {
                sockets.SslOptions.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                break;
            }

            if (h is HttpClientHandler legacy) {
                legacy.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                break;
            }

            h = h is DelegatingHandler d ? d.InnerHandler : null;
        }

        return handler;
    }

    private readonly ILogger<XHGameApi> _logger;
    private readonly FreeSqlCloud _fsql;

    private string account = "cenet999";
    private string apiKey = "MshqzmC6w4O0KmZFwwyGdi9woirawyc8";

    private string apiUrl = "https://ap.xh-api.com";

    private static bool IsDebugEnvironment() {
        return string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            Environments.Development,
            StringComparison.OrdinalIgnoreCase);
    }


    public XHGameApi(ILogger<XHGameApi> logger, FreeSqlCloud fsql) {
        _logger = logger;
        _fsql = fsql;
        _logger.LogInformation($"XHGameApi initialized");
    }

    /// <summary>
    /// XH 接口要求 transferno（transid）为正整数，生成十进制字符串订单号。
    /// </summary>
    public static string CreateXhTransferOrderId() {
        long unixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ulong n = (ulong)unixMilliseconds * 10_000UL + (ulong)(Interlocked.Increment(ref _xhTransferSequence) % 10_000);
        if (n == 0) {
            n = 1;
        }

        return n.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// XH 用户注册
    /// </summary>
    /// <param name="player_name">玩家名称（用作 XH 登录的 username）</param>
    /// <param name="apiCode">接口标识</param>
    /// <returns>XH用户注册结果</returns>
    public async Task<bool> UserRegister(string player_name, string apiCode) {
        if (IsDebugEnvironment()) {
            _logger.LogInformation("XH游戏用户注册走调试模拟 - 玩家名称: {PlayerName}, 接口标识: {ApiCode}", player_name, apiCode);
            return true;
        }

        try {
            // 1. 参数验证
            if (string.IsNullOrEmpty(player_name)) {
                _logger.LogWarning("XH游戏用户注册失败：玩家名称不能为空");
                return false;
            }

            // 2. 构建请求参数
            string account = this.account; // 商户账号
            string apiKey = this.apiKey; // 商户密钥
            // string apiCode = "AG"; // 接口标识，可以根据需要选择AG或BBIN

            // 3. 构建请求URL
            string url = $"{apiUrl}/ley/register";

            // 4. 使用RestSharp构建POST请求
            var client = CreateXhRestClient(url);
            var request = new RestRequest("", Method.Post);

            // 5. 设置请求头为application/x-www-form-urlencoded
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

            // 6. 添加表单参数
            request.AddParameter("account", account);
            request.AddParameter("api_key", apiKey);
            request.AddParameter("api_code", apiCode);
            request.AddParameter("username", player_name);
            request.AddParameter("password", "123456"); // 默认密码，可以根据需要修改

            _logger.LogInformation($"XH游戏用户注册开始 - 玩家名称: {player_name}, 接口标识: {apiCode}");

            // 7. 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            // 8. 处理响应
            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content)) {
                try {
                    // 记录响应内容用于调试
                    _logger.LogInformation("XH游戏API注册响应: {ResponseContent}", response.Content);

                    // 解析JSON响应
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查返回码
                    int code = jsonResponse["Code"]?.Value<int>() ?? -1;
                    string message = jsonResponse["Message"]?.ToString() ?? "未知错误";

                    if (code == 0) {
                        // 注册成功
                        _logger.LogInformation("XH游戏用户注册成功 - 玩家名称: {PlayerName}", player_name);
                        return true;
                    } else {
                        // 注册失败，记录具体错误信息
                        _logger.LogInformation($"XH游戏用户注册失败 - 玩家名称: {player_name}, 接口标识: {apiCode}, 错误码: {code}, 错误信息: {message}");
                        _logger.LogWarning("XH游戏用户注册失败 - 玩家名称: {PlayerName}, 错误码: {Code}, 错误信息: {Message}",
                            player_name, code, message);

                        // 特殊处理：如果用户已存在，也返回true（避免重复注册）
                        if (code == 33) // Member already exists
                        {
                            _logger.LogInformation("XH游戏用户已存在，视为注册成功 - 玩家名称: {PlayerName}", player_name);
                            return true;
                        }

                        return false;
                    }
                } catch (JsonException ex) {
                    _logger.LogError(ex, "XH游戏API注册响应解析失败 - 玩家名称: {PlayerName}, 响应内容: {ResponseContent}",
                        player_name, response.Content);
                    return false;
                }
            } else {
                // HTTP请求失败
                _logger.LogError("XH游戏API注册请求失败 - 玩家名称: {PlayerName}, HTTP状态码: {StatusCode}, 错误信息: {ErrorMessage}",
                    player_name, response.StatusCode, response.ErrorMessage);
                return false;
            }
        } catch (Exception ex) {
            // 捕获所有其他异常
            _logger.LogError(ex, "XH游戏用户注册发生异常 - 玩家名称: {PlayerName}", player_name);
            return false;
        }
    }


    /// <summary>
    /// XH 游戏入口
    /// </summary>
    /// <param name="player_name">玩家名称（须与 UserRegister 的 username 一致，用于 XH 登录）</param>
    /// <param name="gameCode">游戏代码</param>
    /// <param name="apiCode">接口标识</param>
    /// <param name="gametype">游戏类型</param>
    /// <returns>游戏登录URL，失败时返回空字符串</returns>
    public async Task<string> GetGameUrl(string player_name, string gameCode, string apiCode, GameType gametype) {
        if (IsDebugEnvironment()) {
            _logger.LogInformation(
                "XH游戏登录走调试模拟 - 玩家名称: {PlayerName}, 游戏代码: {GameCode}, 接口标识: {ApiCode}, 返回链接: {DebugLaunchUrl}",
                player_name, gameCode, apiCode, DebugLaunchUrl);
            return DebugLaunchUrl;
        }

        try {
            // 1. 参数验证
            if (string.IsNullOrEmpty(player_name)) {
                _logger.LogWarning("XH游戏登录失败：玩家名称不能为空");
                return "";
            }

            if (string.IsNullOrEmpty(gameCode)) {
                _logger.LogWarning("XH游戏登录失败：游戏代码不能为空");
                return "";
            }
            if (string.IsNullOrEmpty(apiCode)) {
                _logger.LogWarning("XH游戏登录失败：接口标识不能为空");
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
            var client = CreateXhRestClient(url);
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
            request.AddParameter("currency", "CNY");
            

            _logger.LogInformation($"XH游戏登录开始 - 玩家名称: {player_name}, 游戏类型: {gameType}, 游戏代码: {gameCode}, 接口标识: {apiCode}");

            // 7. 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            // 8. 处理响应
            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content)) {
                try {

                    // 记录响应内容用于调试
                    _logger.LogInformation("XH游戏API登录响应: {ResponseContent}", response.Content);

                    // 解析JSON响应
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查返回码
                    int code = jsonResponse["Code"]?.Value<int>() ?? -1;
                    string message = jsonResponse["Message"]?.ToString() ?? "未知错误";

                    if (code == 0) {
                        // 登录成功，获取游戏URL
                        var data = jsonResponse["Data"];
                        string gameUrl = data?["url"]?.ToString() ?? "";

                        if (!string.IsNullOrEmpty(gameUrl)) {
                            _logger.LogInformation($"XH游戏登录成功 - 玩家名称: {player_name}, 游戏URL: {gameUrl}");
                            return gameUrl;
                        } else {
                            _logger.LogWarning($"XH游戏登录响应中未包含游戏URL - 玩家名称: {player_name}");
                            return "";
                        }
                    } else {
                        // 登录失败，记录具体错误信息
                        _logger.LogWarning($"XH游戏登录失败 - 玩家名称: {player_name}, 错误码: {code}, 错误信息: {message}");

                        // 特殊处理：如果会员未注册，先尝试注册
                        if (code == 34) // No user exists, please register
                        {
                            _logger.LogInformation("XH游戏会员未注册，尝试先注册 - 玩家名称: {PlayerName}", player_name);

                            // 先注册用户
                            bool registerResult = await UserRegister(player_name, apiCode);
                            if (registerResult) {
                                _logger.LogInformation($"XH游戏用户注册成功，重新尝试登录 - 玩家名称: {player_name}");

                                // 递归调用自己，重新尝试登录
                                return await GetGameUrl(player_name, gameCode, apiCode, gametype);
                            } else {
                                _logger.LogError("XH游戏用户注册失败，无法登录游戏 - 玩家名称: {PlayerName}", player_name);
                                return "";
                            }
                        }

                        return "";
                    }
                } catch (JsonException ex) {
                    _logger.LogError(ex, $"XH游戏API登录响应解析失败 - 玩家名称: {player_name}, 响应内容: {response.Content}");
                    return "";
                }
            } else {
                // HTTP请求失败
                _logger.LogError($"XH游戏API登录请求失败 - 玩家名称: {player_name}, HTTP状态码: {response.StatusCode}, 错误信息: {response.ErrorMessage}");
                return "";
            }
        } catch (Exception ex) {
            // 捕获所有其他异常
            _logger.LogError(ex, $"XH游戏登录发生异常 - 玩家名称: {player_name}");
            return "";
        }
    }



    /// <summary>
    /// XH 上分
    /// </summary>
    /// <param name="player_name">玩家名称（与注册/login 的 username 一致）</param>
    /// <param name="amount">充值金额（整数）</param>
    /// <param name="orderId">转账订单号：须为正整数的十进制字符串（XH 接口 transferno / transid）</param>
    /// <returns>上分结果：(是否成功, 错误信息)</returns>
    public async Task<(bool success, string errorMessage)> PlayerDeposit(string player_name, decimal amount, string orderId, string apiCode) {
        if (IsDebugEnvironment()) {
            _logger.LogInformation(
                "XH游戏上分走调试模拟 - 玩家名称: {PlayerName}, 金额: {Amount}, 订单号: {OrderId}, 接口标识: {ApiCode}",
                player_name, amount, orderId, apiCode);
            return (true, string.Empty);
        }

        try {
            // 1. 参数验证
            if (string.IsNullOrEmpty(player_name)) {
                _logger.LogWarning("XH游戏玩家上分失败：玩家ID不能为空");
                return (false, "玩家ID不能为空");
            }

            if (amount <= 0) {
                _logger.LogWarning("XH游戏玩家上分失败：充值金额必须大于0，当前金额: {Amount}", amount);
                return (false, "充值金额必须大于0");
            }

            if (string.IsNullOrEmpty(orderId)) {
                _logger.LogWarning("XH游戏玩家上分失败：订单ID不能为空");
                return (false, "订单ID不能为空");
            }

            // XH 要求 transferno（transid）为正整数
            if (!ulong.TryParse(orderId.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var transferNoU) || transferNoU == 0) {
                _logger.LogWarning("XH游戏玩家上分失败：transid 须为正整数，当前: {OrderId}", orderId);
                return (false, "transid 须为正整数");
            }

            // 验证金额必须为整数，如果不是整数则舍掉小数部分
            if (amount != Math.Floor(amount)) {
                _logger.LogWarning("XH游戏玩家上分：充值金额包含小数部分，将舍掉小数部分。原金额: {OriginalAmount}, 处理后金额: {ProcessedAmount}", amount, Math.Floor(amount));
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
            var client = CreateXhRestClient(url);
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

            _logger.LogInformation("XH游戏玩家上分开始 - 玩家ID: {PlayerId}, 充值金额: {Amount}, 订单号: {OrderId}, 接口标识: {ApiCode}",
                player_name, transferAmount, orderId, apiCode);

            // 7. 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            // 8. 处理响应
            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content)) {
                try {
                    // 记录响应内容用于调试
                    _logger.LogInformation("XH游戏API上分响应: {ResponseContent}", response.Content);

                    // 解析JSON响应
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查返回码
                    int code = jsonResponse["Code"]?.Value<int>() ?? -1;
                    string message = jsonResponse["Message"]?.ToString() ?? "未知错误";

                    if (code == 0) {
                        // 上分成功
                        var data = jsonResponse["Data"];
                        int depositAmount = data?["deposit"]?.Value<int>() ?? 0;

                        _logger.LogInformation("XH游戏玩家上分成功 - 玩家名称: {PlayerName}, 充值金额: {Amount}, 实际到账: {DepositAmount}, 订单号: {OrderId}",
                            player_name, transferAmount, depositAmount, orderId);
                        return (true, "");
                    } else {
                        _logger.LogWarning("XH游戏玩家上分失败 - 玩家名称: {PlayerName}, 充值金额: {Amount}, 订单号: {OrderId}, 错误码: {Code}, 错误信息: {Message}",
                            player_name, transferAmount, orderId, code, message);
                        return (false, $"游戏平台返回错误({code}): {message}");
                    }
                } catch (JsonException ex) {
                    _logger.LogError(ex, "XH游戏API上分响应解析失败 - 玩家ID: {PlayerId}, 订单号: {OrderId}, 响应内容: {ResponseContent}",
                        player_name, orderId, response.Content);
                    return (false, "游戏平台响应解析失败");
                }
            } else {
                _logger.LogError("XH游戏API上分请求失败 - 玩家ID: {PlayerId}, 订单号: {OrderId}, HTTP状态码: {StatusCode}, 错误信息: {ErrorMessage}",
                    player_name, orderId, response.StatusCode, response.ErrorMessage);
                return (false, $"游戏平台请求失败(HTTP {(int)response.StatusCode})");
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "XH游戏玩家上分发生异常 - 玩家ID: {PlayerId}, 充值金额: {Amount}, 订单号: {OrderId}",
                player_name, amount, orderId);
            return (false, $"游戏平台异常: {ex.Message}");
        }
    }



    /// <summary>
    /// XH 下分
    /// </summary>
    /// <param name="player_name">玩家名称（与注册/login 的 username 一致）</param>
    /// <param name="amount">提现金额（整数）</param>
    /// <param name="orderId">转账订单号：须为正整数的十进制字符串（XH 接口 transferno / transid）</param>
    /// <returns>下分结果，true表示成功，false表示失败</returns>
    public async Task<bool> PlayerWithdraw(string player_name, decimal amount, string orderId, string apiCode) {
        if (IsDebugEnvironment()) {
            _logger.LogInformation(
                "XH游戏下分走调试模拟 - 玩家名称: {PlayerName}, 金额: {Amount}, 订单号: {OrderId}, 接口标识: {ApiCode}",
                player_name, amount, orderId, apiCode);
            return true;
        }

        try {
            // 1. 参数验证
            if (string.IsNullOrEmpty(player_name)) {
                _logger.LogWarning("XH游戏玩家下分失败：玩家名称不能为空");
                return false;
            }

            if (amount <= 0) {
                _logger.LogWarning("XH游戏玩家下分失败：提现金额必须大于0，当前金额: {Amount}", amount);
                return false;
            }

            if (string.IsNullOrEmpty(orderId)) {
                _logger.LogWarning("XH游戏玩家下分失败：订单ID不能为空");
                return false;
            }

            if (!ulong.TryParse(orderId.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var transferNoU) || transferNoU == 0) {
                _logger.LogWarning("XH游戏玩家下分失败：transid 须为正整数，当前: {OrderId}", orderId);
                return false;
            }

            // 验证金额必须为整数，如果不是整数则舍掉小数部分
            if (amount != Math.Floor(amount)) {
                _logger.LogWarning("XH游戏玩家下分：提现金额包含小数部分，将舍掉小数部分。原金额: {OriginalAmount}, 处理后金额: {ProcessedAmount}",
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
            var client = CreateXhRestClient(url);
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

            _logger.LogInformation("XH游戏玩家下分开始 - 玩家ID: {PlayerId}, 提现金额: {Amount}, 订单号: {OrderId}, 接口标识: {ApiCode}",
                player_name, transferAmount, orderId, apiCode);

            // 7. 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            // 8. 处理响应
            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content)) {
                try {
                    // 记录响应内容用于调试
                    _logger.LogInformation("XH游戏API下分响应: {ResponseContent}", response.Content);

                    // 解析JSON响应
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查返回码
                    int code = jsonResponse["Code"]?.Value<int>() ?? -1;
                    string message = jsonResponse["Message"]?.ToString() ?? "未知错误";

                    if (code == 0) {
                        // 下分成功
                        var data = jsonResponse["Data"];
                        int withdrawalAmount = data?["withdrawal"]?.Value<int>() ?? 0;

                        _logger.LogInformation("XH游戏玩家下分成功 - 玩家ID: {PlayerId}, 提现金额: {Amount}, 实际到账: {WithdrawalAmount}, 订单号: {OrderId}",
                            player_name, transferAmount, withdrawalAmount, orderId);
                        return true;
                    } else {
                        // 下分失败，记录具体错误信息
                        _logger.LogWarning("XH游戏玩家下分失败 - 玩家ID: {PlayerId}, 提现金额: {Amount}, 订单号: {OrderId}, 错误码: {Code}, 错误信息: {Message}",
                            player_name, transferAmount, orderId, code, message);
                        return false;
                    }
                } catch (JsonException ex) {
                    _logger.LogError(ex, "XH游戏API下分响应解析失败 - 玩家ID: {PlayerId}, 订单号: {OrderId}, 响应内容: {ResponseContent}",
                        player_name, orderId, response.Content);
                    return false;
                }
            } else {
                // HTTP请求失败
                _logger.LogError("XH游戏API下分请求失败 - 玩家ID: {PlayerId}, 订单号: {OrderId}, HTTP状态码: {StatusCode}, 错误信息: {ErrorMessage}",
                    player_name, orderId, response.StatusCode, response.ErrorMessage);
                return false;
            }
        } catch (Exception ex) {
            // 捕获所有其他异常
            _logger.LogError(ex, "XH游戏玩家下分发生异常 - 玩家ID: {PlayerId}, 提现金额: {Amount}, 订单号: {OrderId}",
                player_name, amount, orderId);
            return false;
        }
    }


    /// <summary>
    /// XH 查余额
    /// </summary>
    /// <param name="player_name">玩家名称（与注册/login 的 username 一致）</param>
    /// <param name="apiCode">平台代码</param>
    /// <returns>玩家余额，失败时返回-1</returns>
    public async Task<decimal> GetPlayerBalance(string player_name, string apiCode) {
        if (IsDebugEnvironment()) {
            _logger.LogInformation(
                "XH游戏查询余额走调试模拟 - 玩家名称: {PlayerName}, 接口标识: {ApiCode}, 返回余额: {Balance}",
                player_name, apiCode, DebugWalletBalance);
            return DebugWalletBalance;
        }

        try {
            // 1. 参数验证
            if (string.IsNullOrEmpty(player_name)) {
                _logger.LogWarning("XH游戏查询玩家余额失败：玩家ID不能为空");
                return -1;
            }

            if (string.IsNullOrEmpty(apiCode)) {
                _logger.LogWarning("XH游戏查询玩家余额失败：平台代码不能为空");
                return -1;
            }

            // 2. 构建请求参数
            string account = this.account; // 商户账号
            string apiKey = this.apiKey; // 商户密钥
            string username = player_name; // 使用玩家ID作为用户名

            // 3. 构建请求URL
            string url = $"{apiUrl}/ley/balance";

            // 4. 使用RestSharp构建POST请求
            var client = CreateXhRestClient(url);
            var request = new RestRequest("", Method.Post);

            // 5. 设置请求头为application/x-www-form-urlencoded
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

            // 6. 添加表单参数
            request.AddParameter("account", account);
            request.AddParameter("api_key", apiKey);
            request.AddParameter("api_code", apiCode);
            request.AddParameter("username", username);

            _logger.LogInformation("XH游戏查询玩家余额开始 - 玩家ID: {PlayerId}, 接口标识: {ApiCode}",
                player_name, apiCode);

            // 7. 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            // 8. 处理响应
            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content)) {
                try {
                    // 记录响应内容用于调试
                    _logger.LogInformation("XH游戏API查询余额响应: {ResponseContent}", response.Content);

                    // 解析JSON响应
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查返回码
                    int code = jsonResponse["Code"]?.Value<int>() ?? -1;
                    string message = jsonResponse["Message"]?.ToString() ?? "未知错误";

                    if (code == 0) {
                        // 查询成功，获取余额
                        var data = jsonResponse["Data"];
                        decimal balance = data?["balance"]?.Value<decimal>() ?? 0;

                        _logger.LogInformation("XH游戏查询玩家余额成功 - 玩家ID: {PlayerId}, 余额: {Balance}",
                            player_name, balance);
                        return balance;
                    } else {
                        // 查询失败，记录具体错误信息
                        _logger.LogWarning("XH游戏查询玩家余额失败 - 玩家ID: {PlayerId}, 错误码: {Code}, 错误信息: {Message}",
                            player_name, code, message);
                        return -1;
                    }
                } catch (JsonException ex) {
                    _logger.LogError(ex, "XH游戏API查询余额响应解析失败 - 玩家ID: {PlayerId}, 响应内容: {ResponseContent}",
                        player_name, response.Content);
                    return -1;
                }
            } else {
                // HTTP请求失败
                _logger.LogError("XH游戏API查询余额请求失败 - 玩家ID: {PlayerId}, HTTP状态码: {StatusCode}, 错误信息: {ErrorMessage}",
                    player_name, response.StatusCode, response.ErrorMessage);
                return -1;
            }
        } catch (Exception ex) {
            // 捕获所有其他异常
            _logger.LogError(ex, "XH游戏查询玩家余额发生异常 - 玩家ID: {PlayerId}",
                player_name);
            return -1;
        }
    }



    /// <summary>
    /// XH 注单历史
    /// </summary>
    /// <param name="player_name">玩家名称（可选；与 XH 注册 username 一致；非空时作为表单 username 传给 gamerecord，仅查该会员）</param>
    /// <param name="from">查询开始时间（<c>yyyy-MM-dd HH:mm:ss</c>，按 <see cref="J9_Admin.Utils.TimeHelper.BeijingTz"/> 墙上时钟解读，与官方示例 Unix 语义一致）</param>
    /// <param name="to">查询结束时间（同上）</param>
    /// <param name="limit">查询条数（单次查询不可超过500条）</param>
    /// <param name="page">页数</param>
    /// <returns>注单历史记录列表</returns>
    public async Task<List<DTransAction>> GetBetHistory(string? player_name, string from, string to, int limit, int page) {
        try {
            // 1. 参数验证
            if (string.IsNullOrEmpty(from)) {
                _logger.LogWarning("XH游戏获取注单历史失败：开始时间不能为空");
                return new List<DTransAction>();
            }

            if (string.IsNullOrEmpty(to)) {
                _logger.LogWarning("XH游戏获取注单历史失败：结束时间不能为空");
                return new List<DTransAction>();
            }

            // 验证每页条数限制（最大500条）
            if (limit > 500) {
                _logger.LogWarning("XH游戏获取注单历史失败：每页条数不能超过500条，当前: {Limit}", limit);
                limit = 500; // 自动调整为最大限制
            }

            if (limit <= 0) {
                _logger.LogWarning("XH游戏获取注单历史失败：每页条数必须大于0，当前: {Limit}", limit);
                return new List<DTransAction>();
            }

            if (page <= 0) {
                _logger.LogWarning("XH游戏获取注单历史失败：页数必须大于0，当前: {Page}", page);
                return new List<DTransAction>();
            }

            // 2. 构建请求参数
            string account = this.account; // 商户账号
            string apiKey = this.apiKey; // 商户密钥
            string method = "betTime"; // 根据投注时间采集，也可以选择updateTime根据修改时间采集

            // 3. 时间格式转换：将字符串时间转换为十位时间戳
            DateTime startDateTime;
            DateTime endDateTime;

            if (!DateTime.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.None, out startDateTime)) {
                _logger.LogWarning("XH游戏获取注单历史失败：开始时间格式不正确，格式应为：2024-04-29 06:39:20，当前: {From}", from);
                return new List<DTransAction>();
            }

            if (!DateTime.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.None, out endDateTime)) {
                _logger.LogWarning("XH游戏获取注单历史失败：结束时间格式不正确，格式应为：2024-04-29 06:39:20，当前: {To}", to);
                return new List<DTransAction>();
            }

            // 按北京时间墙上时钟解读，再转 Unix 秒（与 XH.rest 文档示例一致；避免部署在 UTC 时误用本机时区）
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startDateTime, DateTimeKind.Unspecified));
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endDateTime, DateTimeKind.Unspecified));
            long startTimestamp = new DateTimeOffset(startUtc, TimeSpan.Zero).ToUnixTimeSeconds();
            long endTimestamp = new DateTimeOffset(endUtc, TimeSpan.Zero).ToUnixTimeSeconds();

            if (endTimestamp < startTimestamp) {
                _logger.LogWarning("XH游戏获取注单历史失败：结束时间不能早于开始时间 - From: {From}, To: {To}", from, to);
                return new List<DTransAction>();
            }

            // XH gamerecord：开始与结束时间最大间隔不允许超过 24 小时（含边界：允许恰好 86400 秒）
            if (endTimestamp - startTimestamp > 86400) {
                _logger.LogWarning(
                    "XH游戏获取注单历史失败：时间跨度超过 24 小时（{Span} 秒），请拆分为多段请求 - From: {From}, To: {To}",
                    endTimestamp - startTimestamp, from, to);
                return new List<DTransAction>();
            }

            // 4. 构建请求URL
            string url = $"{apiUrl}/ley/gamerecord";

            // 5. 使用RestSharp构建POST请求
            var client = CreateXhRestClient(url);
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

            _logger.LogInformation("XH游戏获取注单历史开始 - 玩家ID: {PlayerId}, 开始时间: {From}, 结束时间: {To}, 页数: {Page}, 每页条数: {Limit}",
                player_name ?? "全部", from, to, page, limit);

            // 8. 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            // 9. 处理响应
            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content)) {
                try {
                    // 记录响应内容用于调试
                    _logger.LogInformation("XH游戏API获取注单历史响应: {ResponseContent}", response.Content);

                    // 解析JSON响应
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查返回码
                    int code = jsonResponse["Code"]?.Value<int>() ?? -1;
                    string message = jsonResponse["Message"]?.ToString() ?? "未知错误";

                    if (code == 0) {
                        // 查询成功，解析注单数据
                        var data = jsonResponse["Data"];
                        var betRecords = data?["data"] as JArray;

                        if (betRecords != null && betRecords.Count > 0) {
                            List<DTransAction> transActions = new List<DTransAction>();
                            var okBetRecords = new List<JToken>();

                            foreach (var betRecord in betRecords) {
                                try {
                                    // 将API返回的注单数据转换为DTransAction对象
                                    DTransAction transAction = ConvertBetRecordToTransAction(betRecord);
                                    transActions.Add(transAction);
                                    okBetRecords.Add(betRecord);
                                } catch (Exception ex) {
                                    _logger.LogWarning(ex, "XH游戏注单数据转换失败 - 注单ID: {BetId}", betRecord["id"]?.ToString());
                                    // 继续处理其他记录，不中断整个流程
                                }
                            }

                            // 解析注单数据中的会员名称
                            await ApplyMemberResolutionFromBetRecordsAsync(transActions, okBetRecords);

                            _logger.LogInformation("XH游戏获取注单历史成功 - 玩家ID: {PlayerId}, 获取到 {Count} 条记录",
                                player_name ?? "全部", transActions.Count);
                            return transActions;
                        } else {
                            _logger.LogInformation("XH游戏获取注单历史成功 - 玩家ID: {PlayerId}, 未找到符合条件的记录",
                                player_name ?? "全部");
                            return new List<DTransAction>();
                        }
                    } else {
                        // 查询失败，记录具体错误信息
                        _logger.LogWarning("XH游戏获取注单历史失败 - 玩家ID: {PlayerId}, 错误码: {Code}, 错误信息: {Message}",
                            player_name ?? "全部", code, message);
                        return new List<DTransAction>();
                    }
                } catch (JsonException ex) {
                    _logger.LogError(ex, "XH游戏API获取注单历史响应解析失败 - 玩家ID: {PlayerId}, 响应内容: {ResponseContent}",
                        player_name ?? "全部", response.Content);
                    return new List<DTransAction>();
                }
            } else {
                // HTTP 请求失败（含 SSL 握手失败时 StatusCode 常为 0）
                _logger.LogError(response.ErrorException,
                    "XH游戏API获取注单历史请求失败 - 玩家ID: {PlayerId}, HTTP状态码: {StatusCode}, 错误信息: {ErrorMessage}",
                    player_name ?? "全部", response.StatusCode, response.ErrorMessage);
                return new List<DTransAction>();
            }
        } catch (Exception ex) {
            // 捕获所有其他异常
            _logger.LogError(ex, "XH游戏获取注单历史发生异常 - 玩家ID: {PlayerId}, 开始时间: {From}, 结束时间: {To}",
                player_name ?? "全部", from, to);
            return new List<DTransAction>();
        }
    }

    /// <summary>
    /// 分页调用 <see cref="GetBetHistory"/>，将 XH 注单写入本地表（按 <see cref="DTransAction.SerialNumber"/> / rowid 去重，已存在则更新）。
    /// </summary>
    /// <param name="filterUsername">可选：仅落库该登录名对应会员的注单（需在本地 SysUser 存在）</param>
    /// <param name="from">开始时间</param>
    /// <param name="to">结束时间（与 from 间隔不得超过 24 小时，否则 <see cref="GetBetHistory"/> 直接返回空）</param>
    /// <param name="pageSize">每页条数，最大 500</param>
    /// <param name="maxPages">最多拉取页数，防止异常死循环</param>
    public async Task<XHBetHistorySyncResult> SyncBetHistoryToDatabaseAsync(
        string? filterUsername,
        string from,
        string to,
        int pageSize = 500,
        int maxPages = 500) {
        var result = new XHBetHistorySyncResult();
        try {
            long? onlyMemberId = null;
            if (!string.IsNullOrWhiteSpace(filterUsername)) {
                var m = await _fsql.Select<DMember>().Where(u => u.Username == filterUsername.Trim()).FirstAsync();
                if (m == null) {
                    result.Success = false;
                    result.Message = $"未找到登录名为「{filterUsername.Trim()}」的会员";
                    return result;
                }

                onlyMemberId = m.Id;
            }

            var page = 1;
            while (page <= maxPages) {
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

                foreach (var row in deduped) {
                    if (existDict.TryGetValue(row.SerialNumber, out var old)) {
                        old.BetAmount = row.BetAmount;
                        old.ActualAmount = row.ActualAmount;
                        old.Status = row.Status;
                        old.Description = row.Description;
                        old.Data = row.Data ?? "";

                        old.GameRound = row.GameRound ?? "";
                        old.CurrencyCode = string.IsNullOrWhiteSpace(row.CurrencyCode) ? "CNY" : row.CurrencyCode;
                        if (row.DMemberId > 0) {
                            old.DMemberId = row.DMemberId;
                            old.DAgentId = row.DAgentId;
                        }

                        old.ModifiedTime = DateTime.Now;
                        old.ModifiedUserName = "XH注单同步";
                        toUpdate.Add(old);
                    } else {
                        row.CreatedTime = DateTime.Now;
                        row.CreatedUserName = "XH注单同步";
                        row.ModifiedTime = row.CreatedTime;
                        row.ModifiedUserName = "XH注单同步";
                        toInsert.Add(row);
                    }
                }

                using (var uow = _fsql.CreateUnitOfWork()) {
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
        } catch (Exception ex) {
            _logger.LogError(ex, "XH 注单同步落库异常");
            result.Success = false;
            result.Message = ex.Message;
            return result;
        }
    }

    private static string? ReadXhBetUsername(JToken betRecord) {
        static string? Pick(params string?[] xs) {
            foreach (var s in xs) {
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

    private async Task ApplyMemberResolutionFromBetRecordsAsync(List<DTransAction> transActions, IReadOnlyList<JToken> betRecords) {
        var n = Math.Min(transActions.Count, betRecords.Count);
        if (n == 0)
            return;

        var names = new List<string?>(n);
        for (var i = 0; i < n; i++)
            names.Add(ReadXhBetUsername(betRecords[i]));

        var distinct = names.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct().ToList();
        if (distinct.Count == 0)
            return;

        var members = await _fsql.Select<DMember>().Where(m => distinct.Contains(m.Username)).ToListAsync();
        var dict = members.ToDictionary(m => m.Username, m => m, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < n; i++) {
            var name = names[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;
            if (dict.TryGetValue(name.Trim(), out var member)) {
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
    private DTransAction ConvertBetRecordToTransAction(JToken betRecord) {
        // 解析投注金额
        decimal betAmount = 0;
        if (decimal.TryParse(betRecord["betAmount"]?.ToString(), out decimal parsedBetAmount)) {
            betAmount = parsedBetAmount;
        }

        // 解析输赢金额
        decimal netAmount = 0;
        if (decimal.TryParse(betRecord["netAmount"]?.ToString(), out decimal parsedNetAmount)) {
            netAmount = parsedNetAmount;
        }

        // 解析结算状态
        TransactionStatus status = TransactionStatus.Pending;
        if (int.TryParse(betRecord["status"]?.ToString(), out int statusCode)) {
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
            TransactionTime = ReadBetTimeAsLong(betRecord),           // 投注时间（Unix 秒）
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

    private static long ReadBetTimeAsLong(JToken betRecord) {
        var text = betRecord["betTime"]?.ToString()
                   ?? betRecord["updateTime"]?.ToString()
                   ?? "0";

        if (!long.TryParse(text, out var value)) {
            return TimeHelper.UtcUnix();
        }

        // 过小数字（如 43200）能解析但不是 Unix 秒级绝对时间，改用当前 Unix 秒
        const long minPlausibleUnixSeconds = 946684800L; // 2000-01-01 00:00:00 UTC
        if (value < minPlausibleUnixSeconds) {
            return TimeHelper.UtcUnix();
        }

        return value;
    }

    private async Task<long> ResolveXhGamePlatformIdAsync() {
        var platform = await _fsql.Select<DGamePlatform>()
            .Where(p => p.Name == "XH" || p.Name == "XH游戏" || p.Name == "星汇游戏")
            .ToOneAsync();

        if (platform != null) {
            return platform.Id;
        }

        _logger.LogWarning("未找到 XH 游戏平台配置，将使用默认 DGamePlatformId={PlatformId} 写入游戏数据", DefaultXhGamePlatformId);
        return DefaultXhGamePlatformId;
    }

    private static string ReadLocalizedTitle(JToken? game, params string[] languageCodes) {
        var langParams = game?["lang_params"] as JArray;
        if (langParams == null || langParams.Count == 0) {
            return "";
        }

        foreach (var languageCode in languageCodes) {
            var title = langParams
                .OfType<JObject>()
                .FirstOrDefault(x => string.Equals(x["code"]?.ToString(), languageCode, StringComparison.OrdinalIgnoreCase))?["title"]
                ?.ToString();

            if (!string.IsNullOrWhiteSpace(title)) {
                return title.Trim();
            }
        }

        return "";
    }

    private static GameType ParseGameType(JToken? token) {
        if (int.TryParse(token?.ToString(), out var rawValue) && Enum.IsDefined(typeof(GameType), rawValue)) {
            return (GameType)rawValue;
        }

        return GameType.Other;
    }

    /// <summary>
    /// 请求 XH <c>/ley/gamelist</c> 单页。官方说明见
    /// <see href="https://doc.xh-api.com/apidoc/#/api?appKey=api&amp;key=App%255CHttp%255CControllers%255CApi%255CIndex%2540gamelist">获取子游戏列表</see>；
    /// 分页请求体为 <c>page</c>、<c>per_page</c>（与返回里的 <c>per_page</c>/<c>last_page</c> 对应；注单 <c>gamerecord</c> 仍用 <c>pageSize</c>）。
    /// </summary>
    private async Task<(int code, string message, JObject? data)> FetchGameListPageRawAsync(string apiCode, int page, int perPage) {
        string url = $"{apiUrl}/ley/gamelist";
        var client = CreateXhRestClient(url);
        var request = new RestRequest("", Method.Post);
        request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
        request.AddParameter("account", account);
        request.AddParameter("api_key", apiKey);
        request.AddParameter("api_code", apiCode);
        request.AddParameter("page", page);
        request.AddParameter("per_page", perPage);

        RestResponse response = await client.ExecuteAsync(request);

        if (string.IsNullOrWhiteSpace(response.Content)) {
            return (-1, "接口返回空内容", null);
        }

        if (!response.IsSuccessful) {
            _logger.LogWarning("XH游戏获取游戏列表响应非成功状态 - HTTP状态码: {StatusCode}, 错误信息: {ErrorMessage}, 响应内容: {Content}",
                response.StatusCode, response.ErrorMessage, response.Content);
        }

        var jsonResponse = JObject.Parse(response.Content);
        int code = jsonResponse["Code"]?.Value<int>() ?? -1;
        string message = jsonResponse["Message"]?.ToString() ?? "未知错误";
        var data = jsonResponse["Data"] as JObject;
        return (code, message, data);
    }

    private async Task ProcessGameListRecordsAsync(
        string apiCode,
        long gamePlatformId,
        JArray gameRecords,
        List<DGame> gamesList,
        XHGameListSyncResult result) {
        foreach (var game in gameRecords) {
            var title = game["title"]?.ToString()?.Trim() ?? "";
            var cnTitle = ReadLocalizedTitle(game, "CN", "ZH", "TW");
            var enTitle = ReadLocalizedTitle(game, "EN");

            var tmp = new DGame()
            {
                GameUID = game["id"]?.ToString() ?? "",
                GameName = !string.IsNullOrWhiteSpace(enTitle) ? enTitle : title,
                GameCnName = !string.IsNullOrWhiteSpace(cnTitle) ? cnTitle : title,
                GameCode = (game["gameCode"]?.ToString() ?? "").Trim(),
                GameType = ParseGameType(game["gameType"]),
                ApiCode = game["code"]?.ToString()?.Trim() ?? apiCode.Trim(),
                Icon = game["img"]?.ToString() ?? "",
                DGamePlatformId = gamePlatformId,
                IsTestPassed = false,
                TestTime = null,
                IsEnabled = false,
                IsRecommended = false,
            };

            gamesList.Add(tmp);

            if (string.IsNullOrWhiteSpace(tmp.GameCnName)) {
                result.SkippedExisting++;
                _logger.LogWarning("XH游戏同步跳过无名称记录 - ApiCode: {ApiCode}, 原始数据: {GameJson}", apiCode, game.ToString(Formatting.None));
                continue;
            }

            var exist = await _fsql.Select<DGame>()
                .Where(g => g.DGamePlatformId == gamePlatformId)
                .Where(g =>
                    (!string.IsNullOrEmpty(tmp.GameUID) && g.GameUID == tmp.GameUID) &&
                    (!string.IsNullOrEmpty(tmp.GameCode) && g.ApiCode == tmp.ApiCode && g.GameCode == tmp.GameCode) &&
                    g.GameCnName == tmp.GameCnName)
                .FirstAsync();

            if (exist == null) {
                await _fsql.GetRepository<DGame>().InsertAsync(tmp);
                result.Inserted++;
                if (!result.NewByGameType.ContainsKey(tmp.GameType))
                    result.NewByGameType[tmp.GameType] = 0;
                result.NewByGameType[tmp.GameType]++;

                var apiKeyLabel = string.IsNullOrWhiteSpace(tmp.ApiCode) ? "(空)" : tmp.ApiCode.Trim();
                if (!result.NewByApiCode.ContainsKey(apiKeyLabel))
                    result.NewByApiCode[apiKeyLabel] = 0;
                result.NewByApiCode[apiKeyLabel]++;
            } else {
                result.SkippedExisting++;
            }
        }
    }

    /// <summary>
    /// 拉取 XH 游戏列表并同步到库。官方字段说明见
    /// <see href="https://doc.xh-api.com/apidoc/#/api?appKey=api&amp;key=App%255CHttp%255CControllers%255CApi%255CIndex%2540gamelist">获取子游戏列表</see>。
    /// </summary>
    /// <param name="apiCode">子游戏厂商代码</param>
    /// <param name="page">页码；<c>0</c> 表示按接口返回的 <c>last_page</c> 依次拉取全部页（默认，便于后台一键同步）。<c>≥1</c> 时只请求该页。</param>
    /// <param name="pageSize">每页条数（请求里对应 <c>per_page</c>），最大 100</param>
    public async Task<XHGameListSyncResult> GetGameList(string apiCode, int page = 0, int pageSize = 100) {
        try {
            long gamePlatformId = await ResolveXhGamePlatformIdAsync();

            if (pageSize > 100) {
                pageSize = 100;
            }

            if (pageSize < 1) {
                pageSize = 15;
            }

            var gamesList = new List<DGame>();
            var result = new XHGameListSyncResult { Success = true };

            if (page > 0) {
                var (code, message, data) = await FetchGameListPageRawAsync(apiCode, page, pageSize);
                result.PagesDone = 1;
                if (code != 0) {
                    _logger.LogError("XH游戏获取游戏列表失败 - Code: {Code}, Message: {Message}", code, message);
                    return new XHGameListSyncResult { Success = false, Message = message };
                }

                var gameRecords = data?["data"] as JArray ?? data?["gamelist"] as JArray;
                if (gameRecords == null || gameRecords.Count == 0) {
                    result.RemoteTotal = data?["total"]?.Value<int>() ?? 0;
                    result.GamesJson = "[]";
                    return result;
                }

                result.RemoteTotal = data?["total"]?.Value<int>() ?? gameRecords.Count;
                await ProcessGameListRecordsAsync(apiCode, gamePlatformId, gameRecords, gamesList, result);
                result.GamesJson = gamesList.ToJson();
                return result;
            }

            // page == 0：按 last_page 拉全部分页
            int current = 1;
            int lastPage = 1;
            bool remoteTotalSet = false;

            while (current <= lastPage) {
                var (code, message, data) = await FetchGameListPageRawAsync(apiCode, current, pageSize);
                result.PagesDone++;

                if (code != 0) {
                    _logger.LogError("XH游戏获取游戏列表失败 - Code: {Code}, Message: {Message}, Page: {Page}", code, message, current);
                    return new XHGameListSyncResult { Success = false, Message = message, PagesDone = result.PagesDone };
                }

                var gameRecords = data?["data"] as JArray ?? data?["gamelist"] as JArray;

                if (!remoteTotalSet && data != null) {
                    result.RemoteTotal = data["total"]?.Value<int>() ?? 0;
                    var lastFromApi = data["last_page"]?.Value<int>();
                    if (lastFromApi is > 0) {
                        lastPage = lastFromApi.Value;
                    } else {
                        var total = data["total"]?.Value<int>() ?? 0;
                        var perInResp = data["per_page"]?.Value<int>() ?? pageSize;
                        lastPage = total > 0 && perInResp > 0
                            ? (total + perInResp - 1) / perInResp
                            : 1;
                    }

                    if (lastPage < 1) {
                        lastPage = 1;
                    }

                    remoteTotalSet = true;
                }

                if (gameRecords == null || gameRecords.Count == 0) {
                    break;
                }

                await ProcessGameListRecordsAsync(apiCode, gamePlatformId, gameRecords, gamesList, result);
                current++;

                if (current > 10_000) {
                    _logger.LogWarning("XH游戏列表分页超过安全上限，停止拉取 - ApiCode: {ApiCode}", apiCode);
                    break;
                }
            }

            result.GamesJson = gamesList.ToJson();
            return result;
        } catch (Exception ex) {
            _logger.LogError(ex, "XH游戏获取游戏列表发生异常");
            return new XHGameListSyncResult { Success = false, Message = ex.Message };
        }
    }


}
