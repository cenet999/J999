using FreeSql.DataAnnotations;
using System.ComponentModel;

/// <summary>
/// 游戏信息
/// </summary>
[Table(Name = "ddd_game")]
[Index("idx_game_uid", nameof(GameUID))]
[Index("idx_game_name", nameof(GameName))]
public partial class DGame : EntityModified
{

    /// <summary>
    /// 游戏名称英文
    /// </summary>
    public string GameName { get; set; }

    /// <summary>
    /// 游戏中文名
    /// </summary>
    public string GameCnName { get; set; }

    /// <summary>
    /// 游戏描述
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// 游戏代码
    /// </summary>
    public string GameUID { get; set; }

    /// <summary>
    /// 游戏类型
    /// </summary>
    public GameType GameType { get; set; }

    /// <summary>
    /// 游戏Code
    /// </summary>
    public string GameCode { get; set; }

    /// <summary>
    /// 图标
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否推荐
    /// </summary>
    public bool IsRecommended { get; set; }

    /// <summary>
    /// 接口代码
    /// </summary>
    public string ApiCode { get; set; }

    /// <summary>
    /// 游戏评分(如4.8)
    /// </summary>
    public decimal Rating { get; set; }

    /// <summary>
    /// 在玩人数(用于排序和展示热度)
    /// </summary>
    public int PlayerCount { get; set; }

    /// <summary>
    /// 点击次数
    /// </summary>
    public int ClickCount { get; set; }

    /// <summary>
    /// 测试是否通过（接口/联调等）
    /// </summary>
    public bool IsTestPassed { get; set; }

    /// <summary>
    /// 测试时间
    /// </summary>
    public DateTime? TestTime { get; set; }

}

public partial class DGame
{
    /// <summary>
    /// 游戏平台类型
    /// </summary>
    public DGamePlatform DGamePlatform { get; set; }

    /// <summary>
    /// 游戏平台ID
    /// </summary>
    public long DGamePlatformId { get; set; }

}

//1真人,2捕鱼,3电子,4彩票,5体育,6棋牌,7电竞
public enum GameType
{
    [Description("真人")]
    Live = 1,
    [Description("捕鱼")]
    Fishing = 2,
    [Description("电子")]
    Electronic = 3,
    [Description("彩票")]
    Lottery = 4,
    [Description("体育")]
    Sports = 5,
    [Description("棋牌")]
    Card = 6,
    [Description("电竞")]
    Other = 7
}
