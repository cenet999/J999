using System.Text;
using System.Text.Json;
using J9_Admin.API;
using J9_Admin.Utils;
using FreeScheduler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestSharp;
using System.Text.Json.Serialization;

namespace J9_Admin.Services.PayApi;

/// <summary>
/// TokenPay 支付
/// </summary>
public class Pay0Api
{
    private readonly ILogger<Pay0Api> _logger;
    private readonly FreeSqlCloud _fsql;
    private readonly TaskProgressService _taskProgressService;

    // TokenPay配置参数
    private string TokenPayUrl = "https://payu.moneysb.com"; // TokenPay API地址
    private string TokenPayApiToken = "ABC123"; // API Token

    /// <summary>
    /// 支付服务构造
    /// </summary>
    public Pay0Api(ILogger<Pay0Api> logger, FreeSqlCloud fsql, TaskProgressService taskProgressService)
    {
        _logger = logger;
        _fsql = fsql;
        _taskProgressService = taskProgressService ?? throw new ArgumentNullException(nameof(taskProgressService));
        _logger.LogInformation("Pay0Api initialized");
    }

    /// <summary>
    /// 创建支付单
    /// </summary>
    /// <param name="httpContext">HTTP上下文</param>
    /// <param name="mchOrderNo">商户订单号</param>
    /// <param name="amount">支付金额</param>
    /// <param name="returnUrl">支付结果前端跳转URL</param>
    /// <returns>支付URL，如果创建失败则返回错误信息</returns>
    public async Task<ApiResult> CreateOrder(HttpContext httpContext, string mchOrderNo, decimal amount, string returnUrl = "")
    {
        try
        {
            _logger.LogInformation("开始创建TokenPay支付订单，订单号：{MchOrderNo}，金额：{Amount}", mchOrderNo, amount);

            // 1. 验证必填参数
            if (string.IsNullOrEmpty(mchOrderNo))
            {
                _logger.LogWarning("创建订单失败：订单号不能为空");
                return ApiResult.Error.SetMessage("订单号不能为空");
            }

            // 2. 验证金额范围
            if (amount < 2)
            {
                _logger.LogWarning("创建订单失败：金额不能小于2");
                return ApiResult.Error.SetMessage("金额不能小于2");
            }

            // 3. 查找交易记录
            var transAction = await _fsql.Select<DTransAction>()
                    .Include(t => t.DMember)
                    .Include(t => t.DAgent)
                    .Where(m => m.Id == long.Parse(mchOrderNo) &&
                                m.TransactionType == TransactionType.Recharge &&
                                m.Status == TransactionStatus.Pending)
                    .ToOneAsync();

            if (transAction == null)
            {
                _logger.LogWarning("创建订单失败：订单不存在，订单号：{MchOrderNo}", mchOrderNo);
                return ApiResult.Error.SetMessage("订单不存在");
            }

            // 4. 构建订单参数
            var orderParams = new Dictionary<string, string>
            {
                ["OutOrderId"] = transAction.Id.ToString(),
                ["OrderUserKey"] = transAction.DMember.Username,
                ["ActualAmount"] = amount.ToString(),
                ["Currency"] = "USDT_TRC20",
                ["NotifyUrl"] = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/api/trans/@Pay0Callback",
                ["RedirectUrl"] = returnUrl,
                ["Address_TRON"] = transAction.DAgent.UsdtAddress
            };

            // 5. 生成签名
            string signature = StringHelper.GenerateSignature(orderParams, TokenPayApiToken);
            orderParams["Signature"] = signature;

            // 6. 调用TokenPay接口创建订单
            var tokenPayResult = await CallTokenPayCreateOrder(orderParams);
            if (tokenPayResult == null)
            {
                _logger.LogError("调用TokenPay接口失败，订单号：{MchOrderNo}", mchOrderNo);
                return ApiResult.Error.SetMessage("支付接口调用失败");
            }

            _logger.LogInformation("TokenPay订单创建成功，订单号：{MchOrderNo}，支付URL：{PaymentUrl}", mchOrderNo, tokenPayResult);
            return ApiResult.Success.SetData(tokenPayResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建TokenPay支付订单时发生异常，订单号：{MchOrderNo}", mchOrderNo);
            return ApiResult.Error.SetMessage($"创建订单失败：{ex.Message}");
        }

        // 调用TokenPay创建订单接口的内部方法
        async Task<string?> CallTokenPayCreateOrder(Dictionary<string, string> orderParams)
        {
            try
            {
                // 构建请求对象
                var createOrderRequest = new
                {
                    OutOrderId = orderParams["OutOrderId"],
                    OrderUserKey = orderParams["OrderUserKey"],
                    ActualAmount = orderParams["ActualAmount"],
                    Currency = orderParams["Currency"],
                    NotifyUrl = orderParams["NotifyUrl"],
                    RedirectUrl = orderParams["RedirectUrl"],
                    Address_TRON = orderParams["Address_TRON"],
                    Signature = orderParams["Signature"]
                };

                // 构建请求URL
                string createOrderUrl = $"{TokenPayUrl}/CreateOrder";
                _logger.LogInformation("调用TokenPay接口，URL：{RequestUrl}", createOrderUrl);

                // 使用RestSharp发送POST请求
                var options = new RestClientOptions(createOrderUrl)
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                };
                var client = new RestClient(options);
                var request = new RestRequest("", Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddJsonBody(createOrderRequest);

                var response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    _logger.LogError("TokenPay接口调用失败，状态码：{StatusCode}，响应内容：{Content}", response.StatusCode, response.Content);
                    return null;
                }

                if (string.IsNullOrEmpty(response.Content))
                {
                    _logger.LogError("TokenPay接口返回空内容");
                    return null;
                }

                // 解析JSON响应
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(response.Content);
                if (jsonResponse.GetProperty("success").GetBoolean())
                {
                    string paymentUrl = jsonResponse.GetProperty("data").GetString();
                    paymentUrl = NormalizePaymentUrl(paymentUrl);
                    _logger.LogInformation("TokenPay订单创建成功，支付URL：{PaymentUrl}", paymentUrl);
                    return paymentUrl;
                }
                else
                {
                    string errorMessage = jsonResponse.GetProperty("message").GetString();
                    _logger.LogError("TokenPay创建订单失败：{ErrorMessage}", errorMessage);
                    return null;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "解析TokenPay响应JSON时发生异常");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调用TokenPay创建订单接口时发生异常");
                return null;
            }
        }

        string NormalizePaymentUrl(string? paymentUrl)
        {
            if (string.IsNullOrWhiteSpace(paymentUrl))
                return string.Empty;

            if (!Uri.TryCreate(TokenPayUrl, UriKind.Absolute, out var payApiBaseUri))
                return paymentUrl;

            if (!Uri.TryCreate(paymentUrl, UriKind.Absolute, out var paymentUri))
                return paymentUrl;

            var builder = new UriBuilder(paymentUri)
            {
                Scheme = payApiBaseUri.Scheme,
                Host = payApiBaseUri.Host,
                Port = payApiBaseUri.IsDefaultPort ? -1 : payApiBaseUri.Port,
            };

            return builder.Uri.ToString();
        }
    }

    /// <summary>
    /// 处理支付回调
    /// </summary>
    /// <param name="httpContext">HTTP上下文</param>
    /// <returns>处理结果</returns>
    public async Task<string> HandlePayCallback(HttpContext httpContext)
    {
        try
        {
            _logger.LogInformation("收到TokenPay支付回调通知，请求内容长度：{ContentLength}", httpContext.Request.ContentLength);

            // 读取请求体 - 从当前HTTP请求中获取JSON数据
            var options = new JsonSerializerOptions();
            options.Converters.Add(new NumberToStringConverter());
            var requestBody = await httpContext.Request.ReadFromJsonAsync<Dictionary<string, string>>(options);

            _logger.LogInformation("收到TokenPay支付回调请求体：{RequestBody}", requestBody);

            if (requestBody == null)
            {
                _logger.LogWarning("收到空的TokenPay回调请求体");
                return "error";
            }

            // 1. 验证必填参数
            if (!requestBody.ContainsKey("OutOrderId") || !requestBody.ContainsKey("Status") || !requestBody.ContainsKey("Signature"))
            {
                _logger.LogWarning("TokenPay支付回调处理失败：必填参数缺失");
                return "error";
            }

            // 2. 验证签名
            string receivedSignature = requestBody["Signature"];
            requestBody.Remove("Signature");

            string calculatedSignature = StringHelper.GenerateSignature(requestBody, TokenPayApiToken);

            _logger.LogInformation($"TokenPay签名验证，接收签名：{receivedSignature}，计算签名：{calculatedSignature}");

            if (receivedSignature != calculatedSignature)
            {
                _logger.LogWarning($"TokenPay支付回调签名验证失败");
                return "error";
            }

            // 3. 处理支付结果
            string orderId = requestBody["OutOrderId"];
            string status = requestBody["Status"];
            string transactionId = requestBody.GetValueOrDefault("Id", "");

            _logger.LogInformation($"收到TokenPay支付回调，订单ID：{orderId}，状态：{status}，交易ID：{transactionId}");

            var processResult = await ProcessTokenPayCallback(orderId, status, transactionId);
            if (!processResult)
            {
                _logger.LogError($"TokenPay支付回调处理失败，订单ID：{orderId}");
                return "error";
            }

            _logger.LogInformation($"TokenPay支付回调处理成功，订单ID：{orderId}");
            return "ok";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"处理TokenPay支付回调时发生异常");
            return "error";
        }

        // 处理TokenPay支付回调业务逻辑的内部方法
        async Task<bool> ProcessTokenPayCallback(string orderId, string status, string transactionId)
        {
            try
            {
                _logger.LogInformation($"处理TokenPay支付回调业务逻辑，订单ID：{orderId}，状态：{status}，交易ID：{transactionId}");

                // 检查支付状态
                if (status != "1")
                {
                    _logger.LogWarning($"收到TokenPay支付失败回调，订单ID：{orderId}，状态：{status}");
                    return true; // 即使失败也返回true，表示已收到通知
                }

                // 使用工作单元进行事务处理
                using var uow = _fsql.CreateUnitOfWork();
                try
                {
                    // 查找待处理的充值订单
                    var transAction = await uow.Orm.Select<DTransAction>()
                        .Include(m => m.DAgent).Include(m => m.DMember)
                        .Where(m => m.Id == long.Parse(orderId) &&
                                (m.TransactionType == TransactionType.Recharge || m.TransactionType == TransactionType.AgentRecharge) &&
                                m.Status == TransactionStatus.Pending)
                        .ToOneAsync();

                    if (transAction == null)
                    {
                        _logger.LogWarning($"TokenPay订单不存在或状态不正确，订单ID：{orderId}");
                        return false;
                    }

                    // 更新订单状态为成功
                    // 充值赠送：BetAmount 存赠送金额，实际到账 = ActualAmount + BetAmount
                    var totalCredit = transAction.ActualAmount + transAction.BetAmount;
                    transAction.Status = TransactionStatus.Success;
                    transAction.BeforeAmount = (await uow.Orm.Select<DMember>().Where(m => m.Id == transAction.DMemberId).ToOneAsync())?.CreditAmount ?? 0;
                    transAction.AfterAmount = transAction.BeforeAmount + totalCredit;
                    transAction.TransactionTime = TimeHelper.UtcUnix();

                    // 更新订单状态
                    await uow.Orm.Update<DTransAction>().SetSource(transAction).Where(m => m.Id == transAction.Id).ExecuteAffrowsAsync();

                    // 更新关联交易的状态
                    if (transAction.RelatedTransActionId.HasValue)
                    {
                        await uow.Orm.Update<DTransAction>().Set(m => m.Status, TransactionStatus.Success).Where(m => m.Id == transAction.RelatedTransActionId).ExecuteAffrowsAsync();
                    }

                    // 查找并更新会员余额
                    var member = await uow.Orm.Select<DMember>().Where(m => m.Id == transAction.DMemberId).ToOneAsync();
                    if (member == null)
                    {
                        _logger.LogWarning($"会员不存在，会员ID：{transAction.DMemberId}");
                        return false;
                    }

                    // 更新会员余额（增加充值金额）
                    member.CreditAmount = transAction.AfterAmount;
                    await uow.Orm.Update<DMember>().SetSource(member).Where(m => m.Id == member.Id).ExecuteAffrowsAsync();

                    // 更新代理余额（减少代理游戏分，含赠送部分）
                    var agent = await uow.Orm.Select<DAgent>().Where(m => m.Id == transAction.DAgentId).ToOneAsync();
                    if (agent != null)
                    {
                        agent.GamePoints = agent.GamePoints - totalCredit;
                        await uow.Orm.Update<DAgent>().SetSource(agent).Where(m => m.Id == agent.Id).ExecuteAffrowsAsync();
                    }

                    _logger.LogInformation($"TokenPay订单处理成功，订单ID：{orderId}，会员：{member.Username}，余额已更新",
                        orderId, member.Username);

                    // 提交事务
                    uow.Commit();
                    _logger.LogInformation($"TokenPay订单事务提交成功，订单ID：{orderId}");

                    // 充值任务：按实际充值金额累加进度（每日累计充值>100即完成）
                    _ = _taskProgressService.UpdateTaskProgressAsync(transAction.DMemberId, "Recharge", (int)transAction.ActualAmount);
                }
                catch (Exception ex)
                {
                    // 事务回滚
                    uow.Rollback();
                    _logger.LogError(ex, $"处理TokenPay订单时发生异常，订单ID：{orderId}，事务已回滚");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"处理TokenPay支付回调业务逻辑时发生异常，订单ID：{orderId}");
                return false;
            }
        }
    }




}
