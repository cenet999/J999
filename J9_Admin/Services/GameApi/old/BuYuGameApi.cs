using J9_Admin.Utils;
using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; // 用于JObject

namespace J9_Admin.Services.GameApi;

public class BuYuGameApi
{

    private readonly ILogger<BuYuGameApi> _logger;
    private readonly FreeSqlCloud _fsql;

    /// <summary>
    /// BuYu 构造注入
    /// </summary>
    public BuYuGameApi(ILogger<BuYuGameApi> logger, FreeSqlCloud fsql)
    {
        _logger = logger;
        _fsql = fsql;
        _logger.LogInformation($"BuYuGameApi initialized");
    }

    private string apiUrl = "https://a.d9dx5xy1.top";


    /// <summary>
    /// BuYu 入口
    /// </summary>
    /// <param name="uid">会员ID</param>
    /// <param name="userName">会员账号</param>
    /// <returns>游戏启动结果，包含URL和认证信息</returns>
    public async Task<string> GetGameUrl(string uid, string userName)
    {
        var gameUrl = await getGameUrl(uid, userName);
        var sign = StringHelper.ComputeMD5Hash(uid + userName + "xxlFour").ToUpper();
        var gameCombineUrl = $"{gameUrl}/index.html?userId={uid}&userName={userName}&sign={sign}";

        return gameCombineUrl;

        async Task<string> getGameUrl(string uid, string userName)
        {
            var sign = StringHelper.ComputeMD5Hash(uid + userName + "xxlFour").ToUpper();
            var gameUrl = $"{apiUrl}/api/channelOne/getGameUrl?userId={uid}&userName={userName}&sign={sign}";

            var options = new RestClientOptions(gameUrl)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };
            var client = new RestClient(options);
            var request = new RestRequest("", Method.Get);

            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                try
                {
                    dynamic jsonResponse = JsonConvert.DeserializeObject(response.Content);

                    // 检查响应结果
                    if (jsonResponse.code == 0)
                    {
                        return jsonResponse.data.gameUrl;
                    }

                }
                catch (Newtonsoft.Json.JsonException ex)
                {
                    _logger.LogInformation($"JSON解析失败: {ex.Message}");
                    _logger.LogInformation($"响应内容: {response.Content}");
                    return "";
                }
            }
            return "";

        }
    }


    /// <summary>
    /// BuYu 转账
    /// </summary>
    /// <param name="uid">会员ID</param>
    /// <param name="currencyNum">转换金额</param>
    /// <param name="orderId">订单ID</param>
    /// <param name="type">转换类型</param>
    /// <returns>Game launch result with URL and authentication info</returns>
    public async Task<bool> BuYuTransferGold(string uid, string currencyNum, string orderId, string type = "1")
    {
        // 增加日志输出，便于调试和排查问题
        // 检查参数有效性
        if (decimal.Parse(currencyNum) <= 0 || string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(orderId))
        {
            _logger.LogWarning($"BuYuTransferGold 参数无效: uid={uid}, currencyNum={currencyNum}, orderId={orderId}");
            return false;
        }

        // 生成签名
        var sign = StringHelper.ComputeMD5Hash(uid + currencyNum + orderId + type + "xxlFour").ToUpper();
        var gameCombineUrl = $"{apiUrl}/api/channelOne/transferGold?userId={uid}&currencyNum={currencyNum}&orderId={orderId}&type={type}&sign={sign}";

        _logger.LogInformation($"BuYuTransferGold 请求URL: {gameCombineUrl}");

        // 配置RestClient，忽略SSL证书校验
        var options = new RestClientOptions(gameCombineUrl)
        {
            RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
        };
        var client = new RestClient(options);
        var request = new RestRequest("", Method.Get);

        try
        {
            // 发送请求
            var response = await client.ExecuteAsync(request);

            // 输出响应内容到日志
            _logger.LogInformation($"BuYuTransferGold 响应状态: {response.StatusCode}, 内容: {response.Content}");

            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                dynamic jsonResponse = JsonConvert.DeserializeObject(response.Content);
                int code = jsonResponse.code;
                string msg = jsonResponse.msg != null ? jsonResponse.msg.ToString() : "";
                _logger.LogInformation($"BuYuTransferGold 响应code: {code}, msg: {msg}");

                if (code == 0)
                {
                    _logger.LogInformation("BuYuTransferGold 转账成功");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"BuYuTransferGold 转账失败，code: {code}, msg: {msg}");
                    return false;
                }
            }
            else
            {
                _logger.LogWarning("BuYuTransferGold 响应失败或内容为空");
            }
        }
        catch (Exception ex)
        {
            // 捕获异常并记录日志
            _logger.LogInformation(ex, $"BuYuTransferGold 调用接口异常: {ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// BuYu 用户信息
    /// </summary>
    /// <param name="uid"></param>
    /// <returns></returns>
    public async Task<decimal> BuYuQueryUserInfo(string uid)
    {
        var sign = StringHelper.ComputeMD5Hash(uid + "xxlFour").ToUpper();
        var gameCombineUrl = $"{apiUrl}/api/channelOne/queryUserInfo?userId={uid}&sign={sign}";
        var options = new RestClientOptions(gameCombineUrl)
        {
            RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
        };

        var client = new RestClient(options);
        var request = new RestRequest("", Method.Get);

        var response = await client.ExecuteAsync(request);

        if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
        {
            dynamic jsonResponse = JsonConvert.DeserializeObject(response.Content);
            int code = jsonResponse.code;
            if (code == 0)
            {
                return (decimal)jsonResponse.data.gold;
            }
            return 0;
        }
        return 0;
    }

    /// <summary>
    /// BuYu 金币流水
    /// </summary>
    /// <param name="startDate">查询开始时间</param>
    /// <param name="endDate">查询结束时间</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="pageIndex">页码</param>
    /// <returns>注单历史记录列表</returns>
    public async Task<List<DTransAction>> BuYuQueryGoldRecord(DateTime startDate, DateTime endDate, int pageSize, int pageIndex)
    {
        try
        {
            // 参数验证
            if (startDate == default || endDate == default)
            {
                _logger.LogWarning("获取游戏历史注单失败：查询时间不能为空");
                return new List<DTransAction>();
            }

            if (pageSize <= 0 || pageSize > 5000)
            {
                _logger.LogWarning("获取游戏历史注单失败：查询条数必须在1-5000之间");
                return new List<DTransAction>();
            }

            if (pageIndex <= 0)
            {
                _logger.LogWarning("获取游戏历史注单失败：页数必须大于0");
                return new List<DTransAction>();
            }

            // 生成签名
            string signStr = startDate.ToString("yyyy-MM-dd") + endDate.ToString("yyyy-MM-dd") + pageSize + pageIndex + "xxlFour";
            var sign = StringHelper.ComputeMD5Hash(signStr).ToUpper();

            // 构建请求URL
            var gameCombineUrl = $"{apiUrl}/api/channelOne/queryGoldRecord?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}&pageSize={pageSize}&pageIndex={pageIndex}&sign={sign}";

            _logger.LogInformation("获取BuYu游戏历史注单开始 - 开始时间: {StartDate}, 结束时间: {EndDate}, 条数: {PageSize}, 页数: {PageIndex}",
                startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"), pageSize, pageIndex);

            // 配置RestClient，忽略SSL证书校验
            var options = new RestClientOptions(gameCombineUrl)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };
            var client = new RestClient(options);
            var request = new RestRequest("", Method.Get);

            // 发送请求并获取响应
            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                try
                {
                    // 记录响应内容用于调试
                    _logger.LogInformation("BuYu获取注单历史API响应内容: {ResponseContent}", response.Content);

                    // 使用JObject来安全地访问JSON属性
                    var jsonResponse = JObject.Parse(response.Content);

                    // 检查响应结果 - 注意code字段是数字类型
                    if (jsonResponse["code"]?.Value<int>() == 0)
                    {
                        // 成功时，解析注单数据
                        if (jsonResponse["data"] != null && jsonResponse["data"] is JArray dataArray)
                        {
                            try
                            {
                                // 第一步：收集所有 SerialNumber
                                var serialNumbers = dataArray
                                    .Select(item => item["id"]?.ToString())
                                    .Where(id => !string.IsNullOrEmpty(id))
                                    .Distinct() // 去重，避免重复查询
                                    .ToList();

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
                                        var serialNumber = item["id"]?.ToString() ?? string.Empty;
                                        if (string.IsNullOrEmpty(serialNumber)) continue;

                                        // 获取玩家ID
                                        var playerIdStr = item["userId"]?.ToString();
                                        if (!long.TryParse(playerIdStr, out var playerId) || playerId == 0)
                                        {
                                            _logger.LogWarning("跳过无效的玩家ID: {PlayerId}", playerIdStr);
                                            continue;
                                        }

                                        // 查询会员信息
                                        var member = await _fsql.Select<DMember>().Where(m => m.Id == playerId).ToOneAsync();
                                        if (member == null)
                                        {
                                            _logger.LogWarning("跳过不存在的会员: {PlayerId}", playerId);
                                            continue;
                                        }

                                        if (existingRecordsDict.TryGetValue(serialNumber, out var existingRecord))
                                        {
                                            // 记录已存在，更新字段
                                            // existingRecord.BetAmount = decimal.TryParse(item["costNum"]?.ToString(), out var betAmount) ? betAmount : 0;
                                            // existingRecord.ActualAmount = decimal.TryParse(item["winGold"]?.ToString(), out var winAmount) ? winAmount : 0;
                                            // existingRecord.TransactionTime = DateTime.Now;
                                            existingRecord.ModifiedTime = DateTime.Now;

                                            updateList.Add(existingRecord);
                                        }
                                        else
                                        {
                                            // 记录不存在，创建新记录
                                            var hasCreateTime = DateTime.TryParse(item["createTime"]?.ToString(), out var bettedAt);
                                            var betHistory = new DTransAction
                                            {
                                                SerialNumber = serialNumber,
                                                DMemberId = playerId,
                                                Description = item["gameId"]?.ToString() ?? "",
                                                BetAmount = decimal.TryParse(item["costNum"]?.ToString(), out var betAmount) ? betAmount : 0,
                                                ActualAmount = decimal.TryParse(item["winGold"]?.ToString(), out var winAmount) ? winAmount : 0,
                                                TransactionType = TransactionType.Bet,
                                                CreatedTime = hasCreateTime ? bettedAt : DateTime.Now,
                                                TransactionTime = hasCreateTime
                                                    ? TimeHelper.LocalToUnix(bettedAt)
                                                    : TimeHelper.UtcUnix(),

                                                // 添加缺失的必要字段
                                                CurrencyCode = "CNY", // 货币代码，固定为CNY
                                                GameRound = "", // 游戏局号
                                                Status = TransactionStatus.Success, // 交易状态
                                                IsRebate = false, // 是否已反水
                                                DAgentId = member.DAgentId, // 代理ID
                                                RelatedTransActionId = 0, // 关联交易ID
                                                Data = string.Empty
                                            };

                                            // 计算交易前后的金额
                                            betHistory.BeforeAmount = 0;
                                            betHistory.AfterAmount = 0;

                                            insertList.Add(betHistory);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "解析注单数据项失败: {Item}", item);
                                        // 继续处理其他项，不中断整个流程
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

                                _logger.LogInformation("获取BuYu游戏历史注单成功 - 数据条数: {DataCount}, 当前页: {PageIndex}, 每页条数: {PageSize}",
                                    betHistoryList.Count, pageIndex, pageSize);

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
                        string errorMessage = jsonResponse["msg"]?.ToString() ?? "未知错误";
                        int errorCode = jsonResponse["code"]?.Value<int>() ?? 0;

                        _logger.LogWarning("获取BuYu游戏历史注单失败 - 错误码: {ErrorCode}, 错误信息: {ErrorMessage}",
                            errorCode, errorMessage);

                        return new List<DTransAction>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "获取BuYu游戏历史注单响应解析失败");
                    return new List<DTransAction>();
                }
            }
            else
            {
                _logger.LogInformation("获取BuYu游戏历史注单API调用失败 - 状态码: {StatusCode}, 响应内容: {ResponseContent}",
                    response.StatusCode, response.Content);
                return new List<DTransAction>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "获取BuYu游戏历史注单过程中发生异常");
            return new List<DTransAction>();
        }
    }


    /// <summary>
    /// BuYu 订单详情
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="serialNumber"></param>
    /// <returns></returns>
    public async Task<ApiResult> BuYuQueryTransferOrderInfo(string uid, string serialNumber)
    {
        var sign = StringHelper.ComputeMD5Hash(uid + serialNumber + "xxlFour").ToUpper();
        var gameCombineUrl = $"{apiUrl}/api/channelOne/queryTransferOrderInfo?userId={uid}&orderId={serialNumber}&sign={sign}";
        var options = new RestClientOptions(gameCombineUrl)
        {
            RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
        };
        var client = new RestClient(options);
        var request = new RestRequest("", Method.Get);

        var response = await client.ExecuteAsync(request);

        if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
        {
            _logger.LogInformation($"QueryTransferOrderInfo Response: {response.Content}");
            dynamic jsonResponse = JsonConvert.DeserializeObject(response.Content);

            int code = jsonResponse.code;
            if (code == 0)
            {
                return ApiResult.Success.SetData(new
                {
                    list = jsonResponse.data
                });
            }
            return ApiResult.Error.SetMessage(code.ToString());
        }
        return ApiResult.Error.SetMessage("查询转账订单信息失败");
    }



}