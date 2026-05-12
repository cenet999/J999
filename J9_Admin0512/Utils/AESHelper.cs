using System;
using System.Security.Cryptography;
using System.Text;

namespace J9_Admin.Utils
{
    /// <summary>
    /// AES加密解密工具类
    /// 使用AES/ECB/PKCS7Padding算法进行加密和解密
    /// </summary>
    public static class AESHelper
    {
        // 是否已初始化的标志
        private static bool initialized = false;

        // 加密算法名称
        private const string ALGORITHM = "AES";

        /// <summary>
        /// AES加密方法
        /// </summary>
        /// <param name="str">要加密的字符串</param>
        /// <param name="key">加密密钥（字节数组）</param>
        /// <returns>加密后的字节数组，如果加密失败返回null</returns>
        private static byte[] Aes256Encode(string str, byte[] key)
        {
            // 初始化加密环境
            Initialize();

            byte[] result = null;

            try
            {
                // 创建AES加密器
                using (Aes aes = Aes.Create())
                {
                    // 设置加密模式为ECB
                    aes.Mode = CipherMode.ECB;
                    // 设置填充模式为PKCS7
                    aes.Padding = PaddingMode.PKCS7;
                    // 设置密钥
                    aes.Key = key;

                    // 创建加密器
                    using (ICryptoTransform encryptor = aes.CreateEncryptor())
                    {
                        // 将字符串转换为UTF-8字节数组
                        byte[] inputBytes = Encoding.UTF8.GetBytes(str);
                        // 执行加密
                        result = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
                    }
                }
            }
            catch (Exception e)
            {
                // 打印异常堆栈信息
                Console.WriteLine($"加密过程中发生异常: {e.Message}");
                Console.WriteLine(e.StackTrace);
            }

            return result;
        }

        /// <summary>
        /// AES解密方法
        /// </summary>
        /// <param name="bytes">要解密的字节数组</param>
        /// <param name="key">解密密钥（字节数组）</param>
        /// <returns>解密后的字符串，如果解密失败返回空字符串</returns>
        private static string Aes256Decode(byte[] bytes, byte[] key)
        {
            // 初始化加密环境
            Initialize();

            string result = null;

            try
            {
                // 创建AES解密器
                using (Aes aes = Aes.Create())
                {
                    // 设置解密模式为ECB
                    aes.Mode = CipherMode.ECB;
                    // 设置填充模式为PKCS7
                    aes.Padding = PaddingMode.PKCS7;
                    // 设置密钥
                    aes.Key = key;

                    // 创建解密器
                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    {
                        // 执行解密
                        byte[] decoded = decryptor.TransformFinalBlock(bytes, 0, bytes.Length);
                        // 将解密后的字节数组转换为UTF-8字符串
                        result = Encoding.UTF8.GetString(decoded);
                    }
                }
            }
            catch (Exception e)
            {
                // 打印异常信息
                Console.WriteLine($"解密过程中发生异常: {e.Message}");
                Console.WriteLine(e.StackTrace);
            }

            return result ?? "";
        }

        /// <summary>
        /// 解密方法 - 接受Base64编码的字符串和字符串密钥
        /// </summary>
        /// <param name="strToDecrypt">要解密的Base64编码字符串</param>
        /// <param name="key">解密密钥字符串</param>
        /// <returns>解密后的原始字符串</returns>
        public static string Decrypt(string strToDecrypt, string key)
        {
            try
            {
                // 将Base64字符串解码为字节数组
                byte[] encryptedData = Convert.FromBase64String(strToDecrypt);

                // 将密钥字符串转换为字节数组
                byte[] keyBytes = Encoding.UTF8.GetBytes(key);

                // 调用AES解密方法
                string decryptData = Aes256Decode(encryptedData, keyBytes);

                return decryptData;
            }
            catch (Exception e)
            {
                // 打印异常堆栈信息
                Console.WriteLine($"解密过程中发生异常: {e.Message}");
                Console.WriteLine(e.StackTrace);
            }

            return "";
        }

        /// <summary>
        /// 加密方法 - 接受字符串并返回Base64编码的加密结果
        /// </summary>
        /// <param name="strToEncrypt">要加密的字符串</param>
        /// <param name="key">加密密钥字符串</param>
        /// <returns>Base64编码的加密结果字符串</returns>
        public static string Encrypt(string strToEncrypt, string key)
        {
            try
            {
                // 将密钥字符串转换为字节数组
                byte[] keyBytes = Encoding.UTF8.GetBytes(key);

                // 调用AES加密方法
                byte[] encryptedData = Aes256Encode(strToEncrypt, keyBytes);

                // 将加密结果转换为Base64字符串
                return Convert.ToBase64String(encryptedData);
            }
            catch (Exception e)
            {
                // 打印异常堆栈信息
                Console.WriteLine($"加密过程中发生异常: {e.Message}");
                Console.WriteLine(e.StackTrace);
                return "";
            }
        }

        /// <summary>
        /// 初始化方法
        /// 在C#中，加密提供程序通常由系统自动管理，无需手动添加
        /// </summary>
        public static void Initialize()
        {
            // 如果已经初始化过，直接返回
            if (initialized) return;

            // 在C#中，.NET Framework和.NET Core都内置了AES加密支持
            // 无需像Java中那样手动添加BouncyCastle提供程序
            // 这里只是设置初始化标志
            initialized = true;

            Console.WriteLine("AES加密工具已初始化完成");
        }
    }
}