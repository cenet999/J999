using FreeSql.DataAnnotations;

/// <summary>
/// 游戏平台
/// </summary>
[Table(Name = "ddd_game_platform")]
public partial class DGamePlatform : EntityModified
{

    /// <summary>
    /// 显示名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 平台状态
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 排序
    /// </summary>
    public int Sort { get; set; }



}


