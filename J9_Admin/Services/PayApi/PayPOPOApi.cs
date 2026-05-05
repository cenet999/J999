using System.Globalization;
using System.Text;
using System.Text.Json;
using FreeScheduler;
using J9_Admin.API;
using J9_Admin.Utils;
using Microsoft.AspNetCore.Http.Extensions;
using RestSharp;

namespace J9_Admin.Services.PayApi;

/// <summary>
/// 青蛙系统四方支付
/// </summary>
public class PayPOPOApi
{
    private readonly ILogger<PayPOPOApi> _logger;
    private readonly FreeSqlCloud _fsql;
    private readonly TaskProgressService _taskProgressService;
    private readonly IConfiguration _configuration;

    private readonly string _payPOPOUrl;
    private readonly string _mchId;
    private readonly string _secretKey;

    public PayPOPOApi(ILogger<PayPOPOApi> logger, FreeSqlCloud fsql, TaskProgressService taskProgressService, IConfiguration configuration)
    {
        _logger = logger;
        _fsql = fsql;
        _taskProgressService = taskProgressService ?? throw new ArgumentNullException(nameof(taskProgressService));
        _configuration = configuration;

        _payPOPOUrl = (_configuration["Payment:POPO:Url"] ?? string.Empty).Trim().TrimEnd('/');
        _mchId = (_configuration["Payment:POPO:MchId"] ?? string.Empty).Trim();
        _secretKey = (_configuration["Payment:POPO:SecretKey"] ?? string.Empty).Trim();
        _logger.LogInformation("PayPOPOApi initialized");
    }

    /// <summary>
    /// 创建支付单
    /// </summary>
    public async Task<ApiResult> CreateOrder(HttpContext httpContext, string mchOrderNo, decimal amount, string returnUrl = "")
    {
        try
        {
            _logger.LogInformation("开始创建POPO支付订单，订单号：{MchOrderNo}，金额：{Amount}", mchOrderNo, amount);

            var configError = ValidatePayPOPOConfig();
            if (configError != null) return configError;

            if (string.IsNullOrWhiteSpace(mchOrderNo))
            {
                _logger.LogWarning("创建POPO订单失败：订单号不能为空");
                return ApiResult.Error.SetMessage("订单号不能为空");
            }

            if (!long.TryParse(mchOrderNo, out var orderId))
            {
                _logger.LogWarning("创建POPO订单失败：订单号格式不正确，订单号：{MchOrderNo}", mchOrderNo);
                return ApiResult.Error.SetMessage("订单号格式不正确");
            }

            if (amount < 0.01m)
            {
                _logger.LogWarning("创建POPO订单失败：金额不能小于0.01");
                return ApiResult.Error.SetMessage("金额不能小于0.01");
            }

            var transAction = await _fsql.Select<DTransAction>()
                .Include(t => t.DMember)
                .Include(t => t.DAgent)
                .Include(t => t.PayApi)
                .Where(m => m.Id == orderId &&
                            m.TransactionType == TransactionType.Recharge &&
                            m.Status == TransactionStatus.Pending)
                .ToOneAsync();

            if (transAction == null)
            {
                _logger.LogWarning("创建POPO订单失败：订单不存在或状态不正确，订单号：{MchOrderNo}", mchOrderNo);
                return ApiResult.Error.SetMessage("订单不存在");
            }

            if (string.IsNullOrWhiteSpace(transAction.DMember?.Username))
            {
                _logger.LogWarning("创建POPO订单失败：会员账号为空，订单号：{MchOrderNo}", mchOrderNo);
                return ApiResult.Error.SetMessage("会员账号不能为空");
            }

            var payCode = transAction.PayApi?.ChannelCode?.Trim();
            if (string.IsNullOrWhiteSpace(payCode))
            {
                _logger.LogWarning("创建POPO订单失败：订单未关联有效支付通道编码，订单号：{MchOrderNo}，PayApiId：{PayApiId}", mchOrderNo, transAction.PayApiId);
                return ApiResult.Error.SetMessage("支付通道编码不能为空");
            }

            var apiDomain = _configuration["APIDomain"]?.Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(apiDomain))
            {
                _logger.LogWarning("APIDomain 未配置，无法生成POPO支付回调 NotifyUrl");
                return ApiResult.Error.SetMessage("APIDomain 未配置");
            }

            var orderParams = new Dictionary<string, string>
            {
                ["mchId"] = _mchId,
                ["version"] = "1.0",
                ["requestTime"] = DateTime.Now.ToString("yyyyMMddHHmmss"),
                ["code"] = payCode,
                ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
                ["mchOrderNo"] = transAction.Id.ToString(),
                ["notifyUrl"] = $"{apiDomain}/api/trans/@PayPOPOCallback",
                ["title"] = "J9 Recharge",
                ["ip"] = IpHelper.GetClientIpAddress(httpContext, _logger),
                ["body"] = $"member:{transAction.DMember.Username}",
                ["device"] = "app",
                ["returnUrl"] = returnUrl
            };
            orderParams["sign"] = GeneratePayPOPOSignature(orderParams);

            var paymentUrl = await CallPayPOPOCreateOrder(orderParams);
            if (string.IsNullOrWhiteSpace(paymentUrl))
            {
                _logger.LogError("调用POPO支付接口失败，订单号：{MchOrderNo}", mchOrderNo);
                return ApiResult.Error.SetMessage("支付接口调用失败");
            }

            _logger.LogInformation("POPO订单创建成功，订单号：{MchOrderNo}，支付URL：{PaymentUrl}", mchOrderNo, paymentUrl);
            return ApiResult.Success.SetData(paymentUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建POPO支付订单时发生异常，订单号：{MchOrderNo}", mchOrderNo);
            return ApiResult.Error.SetMessage($"创建订单失败：{ex.Message}");
        }

        async Task<string?> CallPayPOPOCreateOrder(Dictionary<string, string> orderParams)
        {
            try
            {
                var createOrderUrl = $"{_payPOPOUrl}/pay/create";
                _logger.LogInformation("调用POPO接口，URL：{RequestUrl}", createOrderUrl);

                var client = new RestClient(new RestClientOptions(createOrderUrl));
                var request = new RestRequest("", Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddJsonBody(orderParams);

                var response = await client.ExecuteAsync(request);
                if (!response.IsSuccessful)
                {
                    _logger.LogError("POPO接口调用失败，状态码：{StatusCode}，响应内容：{Content}", response.StatusCode, response.Content);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(response.Content))
                {
                    _logger.LogError("POPO接口返回空内容");
                    return null;
                }

                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(response.Content);
                var code = jsonResponse.TryGetProperty("code", out var codeElement) ? codeElement.GetInt32() : 0;
                if (code != 200)
                {
                    var message = jsonResponse.TryGetProperty("msg", out var msgElement) ? msgElement.GetString() : response.Content;
                    _logger.LogError("POPO创建订单失败：{ErrorMessage}", message);
                    return null;
                }

                if (!jsonResponse.TryGetProperty("data", out var dataElement) ||
                    !dataElement.TryGetProperty("payUrl", out var payUrlElement))
                {
                    _logger.LogError("POPO创建订单响应缺少 data.payUrl，响应内容：{Content}", response.Content);
                    return null;
                }

                return payUrlElement.GetString();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "解析POPO响应JSON时发生异常");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调用POPO创建订单接口时发生异常");
                return null;
            }
        }
    }

    /// <summary>
    /// 处理支付回调
    /// </summary>
    public async Task<string> HandlePayCallback(HttpContext httpContext)
    {
        try
        {
            var request = httpContext.Request;
            request.EnableBuffering();

            if (request.Body.CanSeek)
                request.Body.Position = 0;

            using var reader = new StreamReader(
                request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            var rawRequestBody = await reader.ReadToEndAsync();

            if (request.Body.CanSeek)
                request.Body.Position = 0;

            var requestHeaders = httpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
            _logger.LogInformation(
                "收到POPO支付回调通知，请求信息：Method={RequestMethod}, Scheme={RequestScheme}, Host={RequestHost}, Path={RequestPath}, Query={RequestQuery}, DisplayUrl={RequestDisplayUrl}, ContentLength={RequestContentLength}, ContentType={RequestContentType}, RemoteIp={RemoteIp}, Headers={RequestHeaders}, Body={RequestBody}",
                httpContext.Request.Method,
                httpContext.Request.Scheme,
                httpContext.Request.Host.ToString(),
                httpContext.Request.Path.ToString(),
                httpContext.Request.QueryString.ToString(),
                httpContext.Request.GetDisplayUrl(),
                httpContext.Request.ContentLength,
                httpContext.Request.ContentType,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                requestHeaders,
                rawRequestBody);

            var options = new JsonSerializerOptions();
            options.Converters.Add(new NumberToStringConverter());

            Dictionary<string, string>? requestBody = null;
            if (string.Equals(httpContext.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    requestBody = await httpContext.Request.ReadFromJsonAsync<Dictionary<string, string>>(options);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "解析 POPO 回调 JSON 失败，将尝试从 Query 读取");
                }
            }

            if (requestBody == null || requestBody.Count == 0)
            {
                requestBody = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var kv in httpContext.Request.Query)
                {
                    if (kv.Value.Count > 0)
                        requestBody[kv.Key] = kv.Value.ToString();
                }
            }

            _logger.LogInformation("收到POPO支付回调参数：{RequestBody}", requestBody);

            var configError = ValidatePayPOPOConfig();
            if (configError != null) return "error";

            if (requestBody == null || requestBody.Count == 0)
            {
                _logger.LogWarning("收到空的POPO回调参数");
                return "error";
            }

            if (!requestBody.ContainsKey("mchOrderNo") || !requestBody.ContainsKey("status") || !requestBody.ContainsKey("sign"))
            {
                _logger.LogWarning("POPO支付回调处理失败：必填参数缺失");
                return "error";
            }

            var receivedSign = requestBody["sign"];
            var signParams = new Dictionary<string, string>(requestBody, StringComparer.Ordinal);
            signParams.Remove("sign");
            var calculatedSign = GeneratePayPOPOSignature(signParams);

            _logger.LogInformation("POPO签名验证，接收签名：{ReceivedSign}，计算签名：{CalculatedSign}", receivedSign, calculatedSign);

            if (!string.Equals(receivedSign, calculatedSign, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("POPO支付回调签名验证失败");
                return "error";
            }

            var orderId = requestBody["mchOrderNo"];
            var status = requestBody["status"];
            var sysOrderNo = requestBody.GetValueOrDefault("sysOrderNo", "");

            _logger.LogInformation("收到POPO支付回调，订单ID：{OrderId}，状态：{Status}，系统订单号：{SysOrderNo}", orderId, status, sysOrderNo);

            var processResult = await ProcessPayPOPOCallback(orderId, status, sysOrderNo);
            if (!processResult)
            {
                _logger.LogError("POPO支付回调处理失败，订单ID：{OrderId}", orderId);
                return "error";
            }

            _logger.LogInformation("POPO支付回调处理成功，订单ID：{OrderId}", orderId);
            return "success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理POPO支付回调时发生异常");
            return "error";
        }

        async Task<bool> ProcessPayPOPOCallback(string orderId, string status, string sysOrderNo)
        {
            try
            {
                _logger.LogInformation("处理POPO支付回调业务逻辑，订单ID：{OrderId}，状态：{Status}，系统订单号：{SysOrderNo}", orderId, status, sysOrderNo);

                if (status != "3")
                {
                    _logger.LogWarning("收到POPO非成功支付回调，订单ID：{OrderId}，状态：{Status}", orderId, status);
                    return true;
                }

                if (!long.TryParse(orderId, out var transActionId))
                {
                    _logger.LogWarning("POPO回调订单号格式不正确，订单ID：{OrderId}", orderId);
                    return false;
                }

                using var uow = _fsql.CreateUnitOfWork();
                try
                {
                    var transAction = await uow.Orm.Select<DTransAction>()
                        .Include(m => m.DAgent)
                        .Include(m => m.DMember)
                        .Where(m => m.Id == transActionId &&
                                    (m.TransactionType == TransactionType.Recharge || m.TransactionType == TransactionType.AgentRecharge) &&
                                    m.Status == TransactionStatus.Pending)
                        .ToOneAsync();

                    if (transAction == null)
                    {
                        _logger.LogWarning("POPO订单不存在或状态不正确，订单ID：{OrderId}", orderId);
                        return false;
                    }

                    var member = await uow.Orm.Select<DMember>().Where(m => m.Id == transAction.DMemberId).ToOneAsync();
                    if (member == null)
                    {
                        _logger.LogWarning("会员不存在，会员ID：{MemberId}", transAction.DMemberId);
                        return false;
                    }

                    var totalCredit = transAction.ActualAmount + transAction.BetAmount;
                    transAction.Status = TransactionStatus.Success;
                    transAction.BeforeAmount = member.CreditAmount;
                    transAction.AfterAmount = transAction.BeforeAmount + totalCredit;
                    transAction.TransactionTime = TimeHelper.UtcUnix();
                    if (!string.IsNullOrWhiteSpace(sysOrderNo))
                        transAction.BillNo = sysOrderNo;

                    await uow.Orm.Update<DTransAction>().SetSource(transAction).Where(m => m.Id == transAction.Id).ExecuteAffrowsAsync();

                    if (transAction.RelatedTransActionId.HasValue)
                    {
                        await uow.Orm.Update<DTransAction>()
                            .Set(m => m.Status, TransactionStatus.Success)
                            .Where(m => m.Id == transAction.RelatedTransActionId)
                            .ExecuteAffrowsAsync();
                    }

                    member.CreditAmount = transAction.AfterAmount;
                    await uow.Orm.Update<DMember>().SetSource(member).Where(m => m.Id == member.Id).ExecuteAffrowsAsync();

                    uow.Commit();
                    _logger.LogInformation("POPO订单事务提交成功，订单ID：{OrderId}，会员：{Username}，余额已更新", orderId, member.Username);

                    _ = _taskProgressService.UpdateTaskProgressAsync(transAction.DMemberId, "Recharge", (int)transAction.ActualAmount);
                    return true;
                }
                catch (Exception ex)
                {
                    uow.Rollback();
                    _logger.LogError(ex, "处理POPO订单时发生异常，订单ID：{OrderId}，事务已回滚", orderId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理POPO支付回调业务逻辑时发生异常，订单ID：{OrderId}", orderId);
                return false;
            }
        }
    }

    private ApiResult? ValidatePayPOPOConfig()
    {
        var missingKeys = new List<string>();
        if (string.IsNullOrWhiteSpace(_payPOPOUrl)) missingKeys.Add("Payment:POPO:Url");
        if (string.IsNullOrWhiteSpace(_mchId)) missingKeys.Add("Payment:POPO:MchId");
        if (string.IsNullOrWhiteSpace(_secretKey)) missingKeys.Add("Payment:POPO:SecretKey");

        if (missingKeys.Count == 0) return null;

        _logger.LogWarning("POPO支付配置缺失：{MissingKeys}", string.Join(", ", missingKeys));
        return ApiResult.Error.SetMessage("POPO支付配置不完整");
    }

    private string GeneratePayPOPOSignature(Dictionary<string, string> parameters)
    {
        var signParams = parameters
            .Where(p => !string.Equals(p.Key, "sign", StringComparison.Ordinal) && !string.IsNullOrEmpty(p.Value))
            .OrderBy(p => p.Key, StringComparer.Ordinal);

        var signString = string.Join("&", signParams.Select(p => $"{p.Key}={p.Value}")) + _secretKey;
        return StringHelper.ComputeMD5Hash(signString);
    }

}
