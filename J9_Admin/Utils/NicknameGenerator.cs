namespace J9_Admin.Utils;

/// <summary>
/// 注册时生成「形容词 + 名词」风格随机昵称
/// </summary>
public static class NicknameGenerator
{
    private static readonly string[] Adjectives =
    [
        "善良的", "快乐的", "神秘的", "勇敢的", "可爱的", "帅气的", "温柔的", "活泼的",
        "聪明的", "幸运的", "潇洒的", "沉稳的", "热情的", "慵懒的", "呆萌的", "高冷的",
        "佛系的", "闪光的", "傲娇的", "开朗的", "安静的", "调皮的", "憨厚的", "机灵的",
        "淡定的", "元气满满的", "酷酷的", "软萌的", "闪亮的", "自由的",
    ];

    private static readonly string[] Nouns =
    [
        "萝卜", "熊猫", "老虎", "小猫", "海豚", "兔子", "狐狸", "企鹅", "松鼠", "小鹿",
        "锦鲤", "凤凰", "巨龙", "西瓜", "草莓", "芒果", "柠檬", "云朵", "星星", "月亮",
        "旅人", "侠客", "骑士", "法师", "射手", "船长", "探险家", "收藏家", "美食家", "舞者",
        "画家", "歌者", "渔夫", "园丁", "工匠", "学者", "游侠", "驯龙师",
    ];

    /// <summary>
    /// 随机组合一个昵称，例如「善良的萝卜」
    /// </summary>
    public static string Generate()
    {
        var adjective = Adjectives[Random.Shared.Next(Adjectives.Length)];
        var noun = Nouns[Random.Shared.Next(Nouns.Length)];
        return adjective + noun;
    }
}
