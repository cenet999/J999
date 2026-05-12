using System.ComponentModel.DataAnnotations;

namespace J9_Admin.API.DTOs;

/// <summary>
/// 注册请求体
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// 手机号账号
    /// </summary>
    [Required]
    [MinLength(4)]
    [RegularExpression(@"^1[3-9]\d{9}$", ErrorMessage = "请输入有效的手机号码")]
    public string Username { get; set; }

    /// <summary>
    /// 登录密码
    /// </summary>
    [Required]
    [MinLength(4)]
    public string Password { get; set; }

    /// <summary>
    /// 设备指纹
    /// </summary>
    [Required]
    [MinLength(10)]
    public string BrowserFingerprint { get; set; }

    /// <summary>
    /// 代理编号。传 0 时由服务端替换为默认代理。若同时传 <see cref="AgentName"/>，以代理名为准。
    /// </summary>
    [Required]
    public long AgentId { get; set; }

    /// <summary>
    /// 代理名（与后台「代理管理」中配置一致）。非空时优先按名称解析代理，忽略 <see cref="AgentId"/>。
    /// </summary>
    [StringLength(100)]
    public string? AgentName { get; set; }

    /// <summary>
    /// 邀请码
    /// </summary>
    public string InviteCode { get; set; } = "";
}

/// <summary>
/// 登录请求体
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// 会员账号
    /// </summary>
    [Required]
    public string Username { get; set; }

    /// <summary>
    /// 登录密码
    /// </summary>
    [Required]
    public string Password { get; set; }
}

/// <summary>
/// 上传头像请求
/// </summary>
public class UploadAvatarRequest
{
    /// <summary>
    /// 头像内容
    /// </summary>
    [Required(ErrorMessage = "头像数据不能为空")]
    public string Avatar { get; set; } = string.Empty;
}