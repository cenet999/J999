using System;
using System.Security.Cryptography;
using System.Text;

namespace J9_Admin.Utils
{
    /// <summary>
    /// String扩展方法类
    /// 提供字符串相关的扩展功能
    /// </summary>
    public static class StringHelper
    {
        /// <summary>
        /// 生成指定长度的随机字符串
        /// </summary>
        /// <param name="length">要生成的随机字符串长度，默认为5位</param>
        /// <returns>返回生成的随机字符串</returns>
        public static string GetRandomString(int length = 5)
        {
            // 定义可用于生成随机字符串的字符集
            // 包含大小写字母和数字，避免容易混淆的字符如0、O、1、l等
            const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";

            // 创建Random对象用于生成随机数
            // 使用当前时间作为种子，确保每次运行结果不同
            Random random = new Random();

            // 创建StringBuilder用于构建结果字符串
            // StringBuilder在拼接字符串时性能更好
            StringBuilder result = new StringBuilder();

            // 循环生成指定长度的随机字符
            for (int i = 0; i < length; i++)
            {
                // 生成0到字符集长度-1之间的随机索引
                int randomIndex = random.Next(chars.Length);

                // 根据随机索引从字符集中选择字符并添加到结果中
                result.Append(chars[randomIndex]);
            }

            // 返回生成的随机字符串
            return result.ToString();
        }


        /// <summary>
        /// 生成TokenPay签名
        /// </summary>
        /// <param name="parameters">参数字典</param>
        /// <param name="secretKey">密钥</param>
        /// <returns>MD5签名（小写）</returns>
        public static string GenerateSignature(Dictionary<string, string> parameters, string secretKey)
        {
            // 1. 过滤空值参数
            var filteredParams = parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .OrderBy(p => p.Key, StringComparer.Ordinal);

            // 2. 拼接参数字符串
            var paramString = string.Join("&", filteredParams.Select(p => $"{p.Key}={p.Value}"));

            // 3. 拼接密钥
            var signString = paramString + secretKey;

            // 4. 计算MD5（小写）
            return StringHelper.ComputeMD5Hash(signString);
        }


        /// <summary>
        /// 计算MD5哈希值（小写）
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <returns>MD5哈希值（小写）</returns>
        public static string ComputeMD5Hash(string input)
        {
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);

                // 转换为小写16进制字符串
                var sb = new StringBuilder();
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("x2")); // 使用小写x2
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// 验证TokenPay签名
        /// </summary>
        /// <param name="parameters">参数字典（包含Signature字段）</param>
        /// <param name="secretKey">密钥</param>
        /// <returns>签名是否正确</returns>
        public static bool VerifySignature(Dictionary<string, string> parameters, string secretKey)
        {
            if (!parameters.ContainsKey("Signature"))
            {
                return false;
            }

            // 获取接收到的签名
            var receivedSignature = parameters["Signature"];

            // 移除Signature字段
            var paramsWithoutSignature = new Dictionary<string, string>(parameters);
            paramsWithoutSignature.Remove("Signature");

            // 计算签名
            var calculatedSignature = GenerateSignature(paramsWithoutSignature, secretKey);

            // 比较签名（忽略大小写）
            return string.Equals(receivedSignature, calculatedSignature, StringComparison.OrdinalIgnoreCase);
        }


    }
}
