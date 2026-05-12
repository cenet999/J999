using System.ComponentModel.DataAnnotations;

namespace J9_Admin.API.DTOs;

/// <summary>
/// 登录请求体
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// 会员账号
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 登录密码
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;
}
