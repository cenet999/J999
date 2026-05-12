using System.ComponentModel;
using System.Reflection;

namespace J9_Admin.Utils;

/// <summary>
/// 枚举描述帮助类。
/// </summary>
public static class EnumDescriptionHelper
{
    /// <summary>
    /// 获取枚举上的 <see cref="DescriptionAttribute"/>；
    /// 如果没写 Description，就退回到枚举名。
    /// </summary>
    public static string GetDescription(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        var description = member?.GetCustomAttribute<DescriptionAttribute>();
        return description?.Description ?? value.ToString();
    }
}
