using J9_Admin.Utils;
using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; // 用于JObject

namespace J9_Admin.Services.GameApi;

public class PgGameApi
{

    private string MERCHANT_API_BASE = "https://pgapi.fqwfq89qwvn.com/merchant-api";
    public string MERCHANT_ID = "100255";
    public string MERCHANT_SECRET = "c7bb7c237a87a29e1536";

    private readonly ILogger<PgGameApi> _logger;
    private readonly FreeSqlCloud _fsql;

    /// <summary>
    /// PG 构造注入
    /// </summary>
    public PgGameApi(ILogger<PgGameApi> logger, FreeSqlCloud fsql)
    {
        _logger = logger;
        _fsql = fsql;
        _logger.LogInformation($"PgGameApi initialized");
    }

    /// <summary>
    /// PG 注册
    /// </summary>
    /// <param name="player_id">玩家ID</param>
    /// <param name="player_name">玩家名称</param>
    /// <returns>PG用户注册结果</returns>
    public async Task<bool> UserRegister(string player_id, string player_name)
    {


        string currency = "CNY"; // 假设币种为CNY，可以根据实际情况传参
        string language = "zh"; // 假设语言为中文简体，可以根据实际情况传参
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(); // 获取当前时间戳（秒）

        string signRaw = MERCHANT_ID + player_id + player_name + currency + language + timestamp + MERCHANT_SECRET;
        string sign = StringHelper.ComputeMD5Hash(signRaw);

        // 构建请求的URL
        string url = $"{MERCHANT_API_BASE}/player/create";

        // 构建请求体参数
        var requestBody = new
        {
            merchant_id = MERCHANT_ID,
            player_id = player_id,
            player_name = player_name,
            currency = currency,
            language = language,
            timestamp = timestamp,
            sign = sign
        };

        // 使用RestSharp构建POST请求
        var client = new RestClient(url);
        var request = new RestRequest("", Method.Post);

        // 设置请求头为application/json
        request.AddHeader("Content-Type", "application/json");

        // 序列化请求体为JSON字符串
        string jsonBody = JsonConvert.SerializeObject(requestBody);

        // 添加请求体
        request.AddStringBody(jsonBody, DataFormat.Json);

        // 发送请求并获取响应
        RestResponse response = await client.ExecuteAsync(request);

        if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
        {
            try
            {
                // 先检查响应内容
                _logger.LogInformation($"PG API响应内容: {response.Content}");

                // 使用JObject来安全地访问JSON属性
                var jsonResponse = JObject.Parse(response.Content);

                // 检查响应结果 - 注意success字段是字符串类型
                if (jsonResponse["success"]?.ToString() == "1")
                {
                    // 成功时，检查是否有player字段
                    if (jsonResponse["player"] != null)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    // 失败时，获取错误信息
                    string errorMessage = jsonResponse["message"]?.ToString() ?? "未知错误";
                    int errorCode = jsonResponse["code"]?.Value<int>() ?? 0;

                    _logger.LogWarning($"PG用户注册失败 - 错误码: {errorCode}, 错误信息: {errorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "PG用户注册响应解析失败");
                return false;
            }
        }
        return false;
    }


    /// <summary>
    /// PG 游戏地址
    /// </summary>
    /// <param name="player_id">玩家ID</param>
    /// <param name="gameid">游戏ID</param>
    /// <returns>游戏地址</returns>
    public async Task<string> GetGameUrl(string player_id, string gameid)
    {

        

        string currency = "CNY";
        string language = "zh";
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(); // 获取当前时间戳（秒）

        string signRaw = MERCHANT_ID + player_id + currency + gameid + timestamp + MERCHANT_SECRET;
        string sign = StringHelper.ComputeMD5Hash(signRaw);

        string url = $"{MERCHANT_API_BASE}/player/play_url";

        var requestBody = new
        {
            merchant_id = MERCHANT_ID,
            player_id = player_id,
            currency = currency,
            gameid = gameid,
            language = language,
            timestamp = timestamp,
            sign = sign
        };

        var client = new RestClient(url);
        var request = new RestRequest("", Method.Post);

        request.AddHeader("Content-Type", "application/json");

        string jsonBody = JsonConvert.SerializeObject(requestBody);

        request.AddStringBody(jsonBody, DataFormat.Json);

        RestResponse response = await client.ExecuteAsync(request);

        if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
        {
            try
            {
                var jsonResponse = JObject.Parse(response.Content);

                if (jsonResponse["success"]?.ToString() == "1")
                {
                    var gameUrl = jsonResponse["play_url"]?.ToString();
                    return gameUrl;
                }
                else
                {
                    return jsonResponse["message"]?.ToString() ?? "未知错误";
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "PG获取游戏地址响应解析失败");
                return "响应解析失败";
            }
        }
        return "PG获取游戏地址失败";

    }


    /// <summary>
    /// PG 上分
    /// </summary>
    /// <param name="player_id">玩家ID</param>
    /// <param name="amount">充值金额（最多两位小数）</param>
    /// <returns>上分结果，包含余额和交易信息</returns>
    public async Task<bool> PlayerDeposit(string player_id, decimal amount, string orderId)
    {
        try
        {
            var transactionid = orderId;
            var currency = "CNY";
            // 参数验证
            if (string.IsNullOrEmpty(player_id))
            {
                _logger.LogWarning("玩家上分失败：玩家ID不能为空");
                return false;
            }

            if (string.IsNullOrEmpty(currency))
            {
                _logger.LogWarning("玩家上分失败：币种不能为空");
                return false;
            }

            if (amount <= 0m)
            {
                _logger.LogWarning("玩家上分失败：充值金额必须大于0");
                return false;
            }

            if (string.IsNullOrEmpty(transactionid))
            {
                _logger.LogWarning("玩家上分失败：交易号不能为空");
                return false;
            }

            if (transactionid.Length > 32)
            {
                _logger.LogWarning("玩家上分失败：交易号长度不能超过32位 ： {TransactionId}", transactionid);
                return false;
            }

            // 获取当前时间戳（10位）
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            // 构建签名原始字符串：merchant_id + player_id + currency + amount + transactionid + timestamp + merchant_secret
            string signRaw = MERCHANT_ID + player_id + currency + amount.ToString() + transactionid + timestamp + MERCHANT_SECRET;

            // 计算MD5签名
            string sign = StringHelper.ComputeMD5Hash(signRaw);

            _logger.LogInformation("玩家上分开始 - 玩家ID: {PlayerId}, 币种: {Currency}, 金额: {Amount}, 交易号: {TransactionId}",
                player_id, currency, amount, transactionid);

            // 构建请求的URL
            string url = $"{MERCHANT_API_BASE}/player/deposit";

            // 构建请求体参数
            var requestBody = new
            {
                merchant_id = MERCHANT_ID,
                player_id = player_id,
                currency = currency,
                amount = amount.ToString(), // 格式化为两位小数
                transactionid = transactionid,
                timestamp = timestamp,
                sign = sign
            };

            // 使用RestSharp构建POST请求
            var client = new RestClient(url);
            var request = new RestRequest("", Method.Post);

            // 设置请求头为application/json
            request.AddHeader("Content-Type", "application/json");

            // 序列化请求体为JSON字符串
            string jsonBody = JsonConvert.SerializeObject(requestBody);

            // 添加请求体
            request.AddStringBody(jsonBody, DataFormat.Json);

            // 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                try
                {
                    // 记录响应内容用于调试
                    _logger.LogInformation("PG上分API响应内容: {ResponseContent}", response.Content);

                    // 使用JObject来安全地访问JSON属性
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查响应结果 - 注意success字段是字符串类型
                    if (jsonResponse["success"]?.ToString() == "1")
                    {
                        // 成功时，构建返回数据
                        var depositResult = new
                        {
                            player_id = jsonResponse["player_id"]?.ToString(),
                            currency = jsonResponse["currency"]?.ToString(),
                            amount = jsonResponse["amount"]?.ToString(),
                            balance = jsonResponse["balance"]?.ToString(),
                            transactionid = jsonResponse["transactionid"]?.ToString(),
                            tx = jsonResponse["tx"]?.ToString()
                        };

                        _logger.LogInformation("玩家上分成功 - 玩家ID: {PlayerId}, 金额: {Amount}, 余额: {Balance}, 系统交易号: {Tx}",
                            depositResult.player_id, depositResult.amount, depositResult.balance, depositResult.tx);

                        return true;
                    }
                    else
                    {
                        // 失败时，获取错误信息
                        string errorMessage = jsonResponse["message"]?.ToString() ?? "未知错误";
                        int errorCode = jsonResponse["code"]?.Value<int>() ?? 0;

                        _logger.LogWarning("玩家上分失败 - 错误码: {ErrorCode}, 错误信息: {ErrorMessage}, 玩家ID: {PlayerId}",
                            errorCode, errorMessage, player_id);

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "玩家上分响应解析失败 - 玩家ID: {PlayerId}", player_id);
                    return false;
                }
            }
            else
            {
                _logger.LogInformation("玩家上分API调用失败 - 状态码: {StatusCode}, 响应内容: {ResponseContent}, 玩家ID: {PlayerId}",
                    response.StatusCode, response.Content, player_id);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "玩家上分过程中发生异常 - 玩家ID: {PlayerId}, 金额: {Amount}", player_id, amount);
            return false;
        }
    }


    /// <summary>
    /// PG 下分
    /// </summary>
    /// <param name="player_id">玩家ID</param>
    /// <param name="amount">提现金额（最多两位小数）</param>
    /// <param name="orderId">交易号（不能超过32位）</param>
    /// <returns>下分结果，包含余额和交易信息</returns>
    public async Task<bool> PlayerWithdraw(string player_id, decimal amount, string orderId)
    {
        try
        {
            var transactionid = orderId;
            var currency = "CNY";
            // 参数验证
            if (string.IsNullOrEmpty(player_id))
            {
                _logger.LogWarning("玩家下分失败：玩家ID不能为空");
                return false;
            }

            if (string.IsNullOrEmpty(currency))
            {
                _logger.LogWarning("玩家下分失败：币种不能为空");
                return false;
            }

            if (amount <= 0m)
            {
                _logger.LogWarning("玩家下分失败：提现金额必须大于0");
                return false;
            }

            if (string.IsNullOrEmpty(transactionid))
            {
                _logger.LogWarning("玩家下分失败：交易号不能为空");
                return false;
            }

            if (transactionid.Length > 32)
            {
                _logger.LogWarning("玩家下分失败：交易号长度不能超过32位 ： {TransactionId}", transactionid);
                return false;
            }

            // 获取当前时间戳（10位）
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            // 构建签名原始字符串：merchant_id + player_id + currency + amount + transactionid + timestamp + merchant_secret
            string signRaw = MERCHANT_ID + player_id + currency + amount.ToString() + transactionid + timestamp + MERCHANT_SECRET;

            // 计算MD5签名
            string sign = StringHelper.ComputeMD5Hash(signRaw);

            _logger.LogInformation("玩家下分开始 - 玩家ID: {PlayerId}, 币种: {Currency}, 金额: {Amount}, 交易号: {TransactionId}",
                player_id, currency, amount, transactionid);

            // 构建请求的URL
            string url = $"{MERCHANT_API_BASE}/player/withdraw";

            // 构建请求体参数
            var requestBody = new
            {
                merchant_id = MERCHANT_ID,
                player_id = player_id,
                currency = currency,
                amount = amount.ToString(), // 格式化为两位小数
                transactionid = transactionid,
                timestamp = timestamp,
                sign = sign
            };

            // 使用RestSharp构建POST请求
            var client = new RestClient(url);
            var request = new RestRequest("", Method.Post);

            // 设置请求头为application/json
            request.AddHeader("Content-Type", "application/json");

            // 序列化请求体为JSON字符串
            string jsonBody = JsonConvert.SerializeObject(requestBody);

            // 添加请求体
            request.AddStringBody(jsonBody, DataFormat.Json);

            // 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                try
                {
                    // 记录响应内容用于调试
                    _logger.LogInformation("PG下分API响应内容: {ResponseContent}", response.Content);

                    // 使用JObject来安全地访问JSON属性
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查响应结果 - 注意success字段是字符串类型
                    if (jsonResponse["success"]?.ToString() == "1")
                    {
                        // 成功时，构建返回数据
                        var withdrawResult = new
                        {
                            player_id = jsonResponse["player_id"]?.ToString(),
                            currency = jsonResponse["currency"]?.ToString(),
                            amount = jsonResponse["amount"]?.ToString(),
                            balance = jsonResponse["balance"]?.ToString(),
                            transactionid = jsonResponse["transactionid"]?.ToString(),
                            tx = jsonResponse["tx"]?.ToString()
                        };

                        _logger.LogInformation("玩家下分成功 - 玩家ID: {PlayerId}, 金额: {Amount}, 余额: {Balance}, 系统交易号: {Tx}",
                            withdrawResult.player_id, withdrawResult.amount, withdrawResult.balance, withdrawResult.tx);

                        return true;
                    }
                    else
                    {
                        // 失败时，获取错误信息
                        string errorMessage = jsonResponse["message"]?.ToString() ?? "未知错误";
                        int errorCode = jsonResponse["code"]?.Value<int>() ?? 0;

                        _logger.LogWarning("玩家下分失败 - 错误码: {ErrorCode}, 错误信息: {ErrorMessage}, 玩家ID: {PlayerId}",
                            errorCode, errorMessage, player_id);

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "玩家下分响应解析失败 - 玩家ID: {PlayerId}", player_id);
                    return false;
                }
            }
            else
            {
                _logger.LogInformation("玩家下分API调用失败 - 状态码: {StatusCode}, 响应内容: {ResponseContent}, 玩家ID: {PlayerId}",
                    response.StatusCode, response.Content, player_id);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "玩家下分过程中发生异常 - 玩家ID: {PlayerId}, 金额: {Amount}", player_id, amount);
            return false;
        }
    }

    /// <summary>
    /// PG 查余额
    /// </summary>
    /// <param name="player_id">玩家ID</param>
    /// <returns>玩家余额</returns>
    public async Task<decimal> GetPlayerBalance(string player_id)
    {
        try
        {
            var currency = "CNY"; // 固定币种为CNY

            // 参数验证
            if (string.IsNullOrEmpty(player_id))
            {
                _logger.LogWarning("查询玩家余额失败：玩家ID不能为空");
                return 0;
            }

            // 获取当前时间戳（10位）
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            // 构建签名原始字符串：merchant_id + player_id + currency + timestamp + merchant_secret
            string signRaw = MERCHANT_ID + player_id + currency + timestamp + MERCHANT_SECRET;

            // 计算MD5签名
            string sign = StringHelper.ComputeMD5Hash(signRaw);

            _logger.LogInformation("查询玩家余额开始 - 玩家ID: {PlayerId}, 币种: {Currency}",
                player_id, currency);

            // 构建请求的URL
            string url = $"{MERCHANT_API_BASE}/player/balance";

            // 构建请求体参数
            var requestBody = new
            {
                merchant_id = MERCHANT_ID,
                player_id = player_id,
                currency = currency,
                timestamp = timestamp,
                sign = sign
            };

            // 使用RestSharp构建POST请求
            var client = new RestClient(url);
            var request = new RestRequest("", Method.Post);

            // 设置请求头为application/json
            request.AddHeader("Content-Type", "application/json");

            // 序列化请求体为JSON字符串
            string jsonBody = JsonConvert.SerializeObject(requestBody);

            // 添加请求体
            request.AddStringBody(jsonBody, DataFormat.Json);

            // 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                try
                {
                    // 记录响应内容用于调试
                    _logger.LogInformation("PG查询余额API响应内容: {ResponseContent}", response.Content);

                    // 使用JObject来安全地访问JSON属性
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查响应结果 - 注意success字段是字符串类型
                    if (jsonResponse["success"]?.ToString() == "1")
                    {
                        // 成功时，构建返回数据
                        var balanceResult = new
                        {
                            player_id = jsonResponse["player_id"]?.ToString(),
                            currency = jsonResponse["currency"]?.ToString(),
                            balance = jsonResponse["balance"]?.ToString()
                        };

                        _logger.LogInformation("查询玩家余额成功 - 玩家ID: {PlayerId}, 币种: {Currency}, 余额: {Balance}",
                            balanceResult.player_id, balanceResult.currency, balanceResult.balance);

                        // 返回成功结果，包含余额信息
                        return decimal.Parse(balanceResult.balance);
                    }
                    else
                    {
                        // 失败时，获取错误信息
                        string errorMessage = jsonResponse["message"]?.ToString() ?? "未知错误";
                        int errorCode = jsonResponse["code"]?.Value<int>() ?? 0;

                        _logger.LogWarning("查询玩家余额失败 - 错误码: {ErrorCode}, 错误信息: {ErrorMessage}, 玩家ID: {PlayerId}",
                            errorCode, errorMessage, player_id);

                        return 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "查询玩家余额响应解析失败 - 玩家ID: {PlayerId}", player_id);
                    return 0;
                }
            }
            else
            {
                _logger.LogInformation("查询玩家余额API调用失败 - 状态码: {StatusCode}, 响应内容: {ResponseContent}, 玩家ID: {PlayerId}",
                    response.StatusCode, response.Content, player_id);
                return 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "查询玩家余额过程中发生异常 - 玩家ID: {PlayerId}", player_id);
            return 0;
        }
    }


    /// <summary>
    /// PG 注单历史
    /// </summary>
    /// <param name="player_id">玩家ID（可选，为空时查询所有玩家）</param>
    /// <param name="from">查询开始时间（格式：2024-04-29 06:39:20）</param>
    /// <param name="to">查询结束时间（格式：2024-04-29 06:39:20）</param>
    /// <param name="limit">查询条数（单次查询不可超过5000条）</param>
    /// <param name="page">页数</param>
    /// <returns>注单历史记录列表</returns>
    public async Task<List<DTransAction>> GetBetHistory(string? player_id, string from, string to, int limit, int page)
    {
        try
        {
            var currency = "CNY"; // 固定币种为CNY

            // 参数验证
            if (string.IsNullOrEmpty(from))
            {
                _logger.LogWarning("获取游戏历史注单失败：查询开始时间不能为空");
                return new List<DTransAction>();
            }

            if (string.IsNullOrEmpty(to))
            {
                _logger.LogWarning("获取游戏历史注单失败：查询结束时间不能为空");
                return new List<DTransAction>();
            }

            if (limit <= 0 || limit > 5000)
            {
                _logger.LogWarning("获取游戏历史注单失败：查询条数必须在1-5000之间");
                return new List<DTransAction>();
            }

            if (page <= 0)
            {
                _logger.LogWarning("获取游戏历史注单失败：页数必须大于0");
                return new List<DTransAction>();
            }

            // 获取当前时间戳（10位）
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            // 构建签名原始字符串：merchant_id + "bethistory" + timestamp + merchant_secret
            // 注意：bethistory为固定字符串
            string signRaw = MERCHANT_ID + "bethistory" + timestamp + MERCHANT_SECRET;

            // 计算MD5签名
            string sign = StringHelper.ComputeMD5Hash(signRaw);

            _logger.LogInformation("获取游戏历史注单开始 - 玩家ID: {PlayerId}, 开始时间: {From}, 结束时间: {To}, 条数: {Limit}, 页数: {Page}",
                player_id ?? "所有玩家", from, to, limit, page);

            var member = await _fsql.Select<DMember>().Where(m => m.Id == long.Parse(player_id)).ToOneAsync();
            if (member == null)
            {
                _logger.LogWarning("获取游戏历史注单失败：玩家不存在");
                return new List<DTransAction>();
            }

            // 构建请求的URL
            string url = $"{MERCHANT_API_BASE}/player/bet_history";

            // 构建请求体参数
            var requestBody = new
            {
                merchant_id = MERCHANT_ID,
                player_id = player_id, // 可以为空
                currency = currency,
                from = from,
                to = to,
                limit = limit,
                page = page,
                with_data = 0, // 固定值
                timestamp = timestamp,
                sign = sign
            };

            // 使用RestSharp构建POST请求
            var client = new RestClient(url);
            var request = new RestRequest("", Method.Post);

            // 设置请求头为application/json
            request.AddHeader("Content-Type", "application/json");

            // 序列化请求体为JSON字符串
            string jsonBody = JsonConvert.SerializeObject(requestBody);

            // 添加请求体
            request.AddStringBody(jsonBody, DataFormat.Json);

            // 发送请求并获取响应
            RestResponse response = await client.ExecuteAsync(request);

            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                try
                {
                    // 记录响应内容用于调试
                    _logger.LogInformation("PG获取注单历史API响应内容: {ResponseContent}", response.Content);

                    // 使用JObject来安全地访问JSON属性
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查响应结果 - 注意success字段是字符串类型
                    if (jsonResponse["success"]?.ToString() == "1")
                    {
                        // 成功时，解析注单数据
                        if (jsonResponse["data"] != null && jsonResponse["data"] is JArray dataArray)
                        {
                            try
                            {
                                // 第一步：收集所有 SerialNumber
                                var serialNumbers = dataArray.Select(item => item["sid"]?.ToString()).Distinct().ToList();

                                _logger.LogInformation("开始处理注单数据，总条数: {TotalCount}, 唯一SerialNumber数量: {UniqueCount}",
                                    dataArray.Count, serialNumbers.Count);

                                // 第二步：批量查询已存在的记录（只需要一次数据库查询）
                                var existingRecords = await _fsql.Select<DTransAction>()
                                    .Where(x => serialNumbers.Contains(x.SerialNumber))
                                    .ToListAsync();

                                // 第三步：创建查找字典，提高查找效率
                                var existingRecordsDict = existingRecords.ToDictionary(x => x.SerialNumber, x => x);

                                // 第四步：分类处理数据
                                var insertList = new List<DTransAction>();
                                var updateList = new List<DTransAction>();

                                foreach (var item in dataArray)
                                {
                                    try
                                    {
                                        var serialNumber = item["sid"]?.ToString() ?? string.Empty;
                                        if (string.IsNullOrEmpty(serialNumber)) continue;

                                        if (existingRecordsDict.TryGetValue(serialNumber, out var existingRecord))
                                        {
                                            // 记录已存在，更新字段
                                            // existingRecord.BetAmount = decimal.TryParse(item["bet_amount"]?.ToString(), out var betAmount) ? betAmount : 0;
                                            // existingRecord.ActualAmount = decimal.TryParse(item["net_amount"]?.ToString(), out var winAmount) ? winAmount : 0;
                                            existingRecord.ModifiedTime = DateTime.Now;

                                            updateList.Add(existingRecord);
                                        }
                                        else
                                        {
                                            // 记录不存在，创建新记录
                                            var hasBettedAt = DateTime.TryParse(item["betted_at"]?.ToString(), out var bettedAt);
                                            var betHistory = new DTransAction
                                            {
                                                SerialNumber = serialNumber,
                                                DMemberId = long.TryParse(item["player_id"]?.ToString(), out var playerId) ? playerId : 0,
                                                DGameId = item["gid"]?.Value<int>() ?? 0,
                                                BetAmount = decimal.TryParse(item["bet_amount"]?.ToString(), out var betAmount) ? betAmount : 0,
                                                ActualAmount = decimal.TryParse(item["net_amount"]?.ToString(), out var winAmount) ? winAmount : 0,
                                                TransactionType = TransactionType.Bet,
                                                CreatedTime = hasBettedAt ? bettedAt : DateTime.MinValue,
                                                TransactionTime = hasBettedAt
                                                    ? TimeHelper.LocalToUnix(bettedAt)
                                                    : TimeHelper.UtcUnix(),

                                                // 添加缺失的必要字段
                                                CurrencyCode = currency, // 货币代码
                                                GameRound = "", // 游戏局号
                                                Status = TransactionStatus.Success, // 交易状态
                                                Description = "PG游戏投注记录", // 交易描述
                                                IsRebate = false, // 是否已反水
                                                DAgentId = member.DAgentId, // 代理ID，需要根据实际情况设置
                                                RelatedTransActionId = 0 // 关联交易ID
                                            };
                                            betHistory.BeforeAmount = 0;
                                            betHistory.AfterAmount = 0;

                                            insertList.Add(betHistory);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "解析注单数据项失败: {Item}", item);
                                    }
                                }

                                // 第五步：批量数据库操作（大幅减少数据库交互次数）
                                int totalAffected = 0;

                                // 批量插入新记录（只需要一次数据库操作）
                                if (insertList.Count > 0)
                                {
                                    var insertResult = await _fsql.Insert(insertList).ExecuteAffrowsAsync();
                                    totalAffected += insertResult;
                                    _logger.LogInformation("批量插入新注单记录完成 - 插入行数: {InsertRows}", insertResult);
                                }

                                // 批量更新已存在记录（使用事务批量处理）
                                if (updateList.Count > 0)
                                {
                                    using var uow = _fsql.CreateUnitOfWork();
                                    try
                                    {
                                        foreach (var record in updateList)
                                        {
                                            await uow.Orm.GetRepository<DTransAction>().UpdateAsync(record);
                                        }
                                        uow.Commit();
                                        totalAffected += updateList.Count;
                                        _logger.LogInformation("批量更新已存在注单记录完成 - 更新行数: {UpdateRows}", updateList.Count);
                                    }
                                    catch (Exception ex)
                                    {
                                        uow.Rollback();
                                        _logger.LogInformation(ex, "批量更新注单记录失败");
                                        throw;
                                    }
                                }

                                _logger.LogInformation("注单数据处理完成 - 总影响行数: {TotalAffected}, 新记录: {NewCount}, 更新记录: {UpdateCount}",
                                    totalAffected, insertList.Count, updateList.Count);

                                // 返回处理后的数据
                                var betHistoryList = insertList.Concat(updateList).ToList();

                                _logger.LogInformation("获取游戏历史注单成功 - 总条数: {Count}, 当前页: {Page}, 每页条数: {Limit}, 数据条数: {DataCount}",
                                    jsonResponse["count"]?.ToString(), jsonResponse["page"]?.ToString(), jsonResponse["limit"]?.ToString(),
                                    betHistoryList.Count);

                                return betHistoryList;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation(ex, "批量处理注单数据失败");
                                throw;
                            }
                        }
                        else
                        {
                            _logger.LogWarning("注单数据为空或格式不正确");
                            return new List<DTransAction>();
                        }
                    }
                    else
                    {
                        // 失败时，获取错误信息
                        string errorMessage = jsonResponse["message"]?.ToString() ?? "未知错误";
                        int errorCode = jsonResponse["code"]?.Value<int>() ?? 0;

                        _logger.LogWarning("获取游戏历史注单失败 - 错误码: {ErrorCode}, 错误信息: {ErrorMessage}",
                            errorCode, errorMessage);

                        return new List<DTransAction>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "获取游戏历史注单响应解析失败");
                    return new List<DTransAction>();
                }
            }
            else
            {
                _logger.LogInformation("获取游戏历史注单API调用失败 - 状态码: {StatusCode}, 响应内容: {ResponseContent}",
                    response.StatusCode, response.Content);
                return new List<DTransAction>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "获取游戏历史注单过程中发生异常");
            return new List<DTransAction>();
        }
    }

}