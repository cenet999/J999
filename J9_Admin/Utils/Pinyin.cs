using NPinyin;
using System.Text;

namespace J9_Admin.Utils;

public static class PinyinHelper
{
    /// <summary>
    /// 将汉字转换为大写全拼
    /// </summary>
    /// <param name="chineseText">输入的汉字字符串</param>
    /// <returns>转换后的全拼（大写）</returns>
    public static string ToPinyinUpper(string chineseText)
    {
        if (string.IsNullOrEmpty(chineseText))
        {
            return string.Empty;
        }

        // 使用 Pinyin4Net 将汉字转换为拼音
        string pinyin = Pinyin.GetPinyin(chineseText);

        // 转换为大写
        return pinyin.ToUpper();
    }

    /// <summary>
    /// 获取汉字的首字母组合（大写）
    /// </summary>
    /// <param name="chineseText">输入的汉字字符串</param>
    /// <returns>首字母组合（大写）</returns>
    public static string GetFirstLetters(string chineseText)
    {
        if (string.IsNullOrEmpty(chineseText))
        {
            return string.Empty;
        }

        // 初始化结果字符串
        StringBuilder result = new StringBuilder();

        // 遍历每个汉字
        foreach (char c in chineseText)
        {
            // 获取单个汉字的拼音
            string pinyin = Pinyin.GetPinyin(c.ToString());

            // 提取首字母并转换为大写
            if (!string.IsNullOrEmpty(pinyin))
            {
                result.Append(pinyin.Substring(0, 1).ToUpper());
            }
        }

        return result.ToString();
    }
}