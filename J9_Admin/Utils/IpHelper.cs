using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace J9_Admin.Utils
{
    /// <summary>
    /// IP地址帮助类
    /// 提供获取客户端真实IP地址的功能
    /// </summary>
    public static class IpHelper
    {
        /// <summary>
        /// 获取客户端真实IP地址
        /// 支持多种代理服务器和CDN场景
        /// </summary>
        /// <param name="httpContext">HTTP上下文对象</param>
        /// <param name="logger">日志记录器（可选）</param>
        /// <returns>返回客户端真实IP地址，获取失败时返回"unknown"</returns>
        public static string GetClientIpAddress(HttpContext httpContext, ILogger? logger = null)
        {
            try
            {
                // 检查输入参数
                if (httpContext == null)
                {
                    logger?.LogWarning("HttpContext为null，无法获取客户端IP地址");
                    return "unknown";
                }

                // 优先从 X-Forwarded-For 头获取（适用于代理服务器场景）
                // 这个头部通常包含客户端的真实IP地址
                var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedFor))
                {
                    // X-Forwarded-For 可能包含多个IP，格式：clientIP, proxy1IP, proxy2IP
                    // 取第一个IP（真实客户端IP）
                    var firstIp = forwardedFor.Split(',')[0].Trim();
                    if (!string.IsNullOrEmpty(firstIp) && IsValidIpAddress(firstIp))
                    {
                        logger?.LogInformation("从X-Forwarded-For头获取到IP地址: {IpAddress}", firstIp);
                        return firstIp;
                    }
                }

                // 从 X-Real-IP 头获取（Nginx代理常用）
                // 这个头部通常只包含一个IP地址
                var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
                if (!string.IsNullOrEmpty(realIp) && IsValidIpAddress(realIp))
                {
                    logger?.LogInformation("从X-Real-IP头获取到IP地址: {IpAddress}", realIp);
                    return realIp;
                }

                // 从 CF-Connecting-IP 头获取（Cloudflare代理专用）
                // Cloudflare会在这个头部设置原始客户端IP
                var cfConnectingIp = httpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
                if (!string.IsNullOrEmpty(cfConnectingIp) && IsValidIpAddress(cfConnectingIp))
                {
                    logger?.LogInformation("从CF-Connecting-IP头获取到IP地址: {IpAddress}", cfConnectingIp);
                    return cfConnectingIp;
                }

                // 从 X-Original-Forwarded-For 头获取（某些代理服务器使用）
                var originalForwardedFor = httpContext.Request.Headers["X-Original-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(originalForwardedFor))
                {
                    var firstOriginalIp = originalForwardedFor.Split(',')[0].Trim();
                    if (!string.IsNullOrEmpty(firstOriginalIp) && IsValidIpAddress(firstOriginalIp))
                    {
                        logger?.LogInformation("从X-Original-Forwarded-For头获取到IP地址: {IpAddress}", firstOriginalIp);
                        return firstOriginalIp;
                    }
                }

                // 最后从 RemoteIpAddress 获取（直连场景）
                var remoteIp = httpContext.Connection.RemoteIpAddress;
                if (remoteIp != null)
                {
                    // 如果是IPv6的IPv4映射地址，转换为IPv4格式
                    if (remoteIp.IsIPv4MappedToIPv6)
                    {
                        var ipv4 = remoteIp.MapToIPv4().ToString();
                        logger?.LogInformation("从RemoteIpAddress获取到IPv4映射地址: {IpAddress}", ipv4);
                        return ipv4;
                    }

                    var ipAddress = remoteIp.ToString();
                    logger?.LogInformation("从RemoteIpAddress获取到IP地址: {IpAddress}", ipAddress);
                    return ipAddress;
                }

                // 如果都获取不到，返回默认值
                logger?.LogWarning("无法获取客户端IP地址，返回默认值");
                return "unknown";
            }
            catch (Exception ex)
            {
                logger?.LogInformation(ex, "获取客户端IP地址时发生异常");
                return "unknown";
            }
        }

        /// <summary>
        /// 验证IP地址格式是否有效
        /// </summary>
        /// <param name="ipAddress">要验证的IP地址字符串</param>
        /// <returns>如果IP地址格式有效返回true，否则返回false</returns>
        private static bool IsValidIpAddress(string ipAddress)
        {
            // 检查是否为空或空白字符串
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            // 使用IPAddress.TryParse验证IP地址格式
            // 支持IPv4和IPv6格式
            return IPAddress.TryParse(ipAddress, out _);
        }

        /// <summary>
        /// 获取客户端IP地址（简化版本，不需要日志）
        /// </summary>
        /// <param name="httpContext">HTTP上下文对象</param>
        /// <returns>返回客户端真实IP地址</returns>
        public static string GetClientIpAddress(HttpContext httpContext)
        {
            return GetClientIpAddress(httpContext, null);
        }

        /// <summary>
        /// 判断IP地址是否为内网地址
        /// </summary>
        /// <param name="ipAddress">要判断的IP地址</param>
        /// <returns>如果是内网地址返回true，否则返回false</returns>
        public static bool IsPrivateIpAddress(string ipAddress)
        {
            try
            {
                if (!IPAddress.TryParse(ipAddress, out var ip))
                    return false;

                // 只处理IPv4地址
                if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    return false;

                var bytes = ip.GetAddressBytes();

                // 检查是否为私有IP地址范围
                // 10.0.0.0/8 (10.0.0.0 - 10.255.255.255)
                if (bytes[0] == 10)
                    return true;

                // 172.16.0.0/12 (172.16.0.0 - 172.31.255.255)
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    return true;

                // 192.168.0.0/16 (192.168.0.0 - 192.168.255.255)
                if (bytes[0] == 192 && bytes[1] == 168)
                    return true;

                // 127.0.0.0/8 (127.0.0.0 - 127.255.255.255) - 本地回环地址
                if (bytes[0] == 127)
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取前端服务器域名
        /// 支持多种代理场景和域名来源
        /// </summary>
        /// <param name="httpContext">HTTP上下文对象</param>
        /// <param name="logger">日志记录器（可选）</param>
        /// <returns>返回前端服务器域名，获取失败时返回当前请求的Host</returns>
        public static string GetFrontendDomain(HttpContext httpContext, ILogger? logger = null)
        {
            try
            {
                // 检查输入参数
                if (httpContext == null)
                {
                    logger?.LogWarning("HttpContext为null，无法获取前端域名");
                    return "unknown";
                }

                // 优先从 X-Forwarded-Host 头获取（适用于反向代理场景）
                // 这个头部通常包含原始请求的Host信息
                var forwardedHost = httpContext.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedHost))
                {
                    // X-Forwarded-Host 可能包含多个域名，格式：originalHost, proxy1Host, proxy2Host
                    // 取第一个域名（原始前端域名）
                    var firstHost = forwardedHost.Split(',')[0].Trim();
                    if (!string.IsNullOrEmpty(firstHost) && IsValidDomain(firstHost))
                    {
                        logger?.LogInformation("从X-Forwarded-Host头获取到前端域名: {Domain}", firstHost);
                        return firstHost;
                    }
                }

                // 从 X-Original-Host 头获取（某些代理服务器使用）
                var originalHost = httpContext.Request.Headers["X-Original-Host"].FirstOrDefault();
                if (!string.IsNullOrEmpty(originalHost) && IsValidDomain(originalHost))
                {
                    logger?.LogInformation("从X-Original-Host头获取到前端域名: {Domain}", originalHost);
                    return originalHost;
                }

                // 从 Origin 头获取（适用于跨域请求场景）
                // Origin头包含发起请求的源站信息
                var origin = httpContext.Request.Headers["Origin"].FirstOrDefault();
                if (!string.IsNullOrEmpty(origin))
                {
                    try
                    {
                        var uri = new Uri(origin);
                        var domain = uri.Host;
                        if (!string.IsNullOrEmpty(domain) && IsValidDomain(domain))
                        {
                            logger?.LogInformation("从Origin头获取到前端域名: {Domain}", domain);
                            return domain;
                        }
                    }
                    catch (UriFormatException)
                    {
                        logger?.LogWarning("Origin头格式无效: {Origin}", origin);
                    }
                }

                // 从 Referer 头获取（作为备用选项）
                // Referer头包含引用页面的URL信息
                var referer = httpContext.Request.Headers["Referer"].FirstOrDefault();
                if (!string.IsNullOrEmpty(referer))
                {
                    try
                    {
                        var uri = new Uri(referer);
                        var domain = uri.Host;
                        if (!string.IsNullOrEmpty(domain) && IsValidDomain(domain))
                        {
                            logger?.LogInformation("从Referer头获取到前端域名: {Domain}", domain);
                            return domain;
                        }
                    }
                    catch (UriFormatException)
                    {
                        logger?.LogWarning("Referer头格式无效: {Referer}", referer);
                    }
                }

                // 最后从当前请求的Host获取（直连场景）
                var requestHost = httpContext.Request.Host.Host;
                if (!string.IsNullOrEmpty(requestHost) && IsValidDomain(requestHost))
                {
                    logger?.LogInformation("从Request.Host获取到域名: {Domain}", requestHost);
                    return requestHost;
                }

                // 如果都获取不到，返回默认值
                logger?.LogWarning("无法获取前端域名，返回默认值");
                return "unknown";
            }
            catch (Exception ex)
            {
                logger?.LogInformation(ex, "获取前端域名时发生异常");
                return "unknown";
            }
        }

        /// <summary>
        /// 验证域名格式是否有效
        /// </summary>
        /// <param name="domain">要验证的域名字符串</param>
        /// <returns>如果域名格式有效返回true，否则返回false</returns>
        private static bool IsValidDomain(string domain)
        {
            // 检查是否为空或空白字符串
            if (string.IsNullOrWhiteSpace(domain))
                return false;

            try
            {
                // 尝试创建Uri来验证域名格式
                // 如果域名不包含协议，添加http前缀进行验证
                var uriString = domain.StartsWith("http://") || domain.StartsWith("https://")
                    ? domain
                    : $"http://{domain}";

                var uri = new Uri(uriString);

                // 检查Host部分是否有效
                return !string.IsNullOrEmpty(uri.Host) &&
                       uri.Host.Contains('.') && // 域名应该包含点号
                       !uri.Host.StartsWith('.') && // 不能以点号开头
                       !uri.Host.EndsWith('.'); // 不能以点号结尾
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取前端服务器域名（简化版本，不需要日志）
        /// </summary>
        /// <param name="httpContext">HTTP上下文对象</param>
        /// <returns>返回前端服务器域名</returns>
        public static string GetFrontendDomain(HttpContext httpContext)
        {
            return GetFrontendDomain(httpContext, null);
        }
    }
}
