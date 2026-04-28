# MS游戏配置管理系统

## 概述

这个系统提供了一个完整的游戏配置管理解决方案，包括JSON配置文件和C#管理类，方便初始化和调用各种游戏提供商的配置信息。

## 文件结构

- `MSGameConfig.json` - 游戏配置数据文件
- `MSGameConfigManager.cs` - 配置管理器类
- `MSGameConfigExample.cs` - 使用示例
- `MSGameType.cs` - 原有的游戏类型定义

## 主要功能

### 1. 游戏类型管理
- 支持7种游戏类型：真人(1)、捕鱼(2)、电子(3)、彩票(4)、体育(5)、棋牌(6)、电竞(7)
- 提供游戏类型名称查询功能

### 2. 提供商管理
- 支持60+个游戏提供商
- 区分正常、维护中、已下架状态
- 支持按提供商查询游戏信息

### 3. 游戏配置查询
- 根据提供商代码查询支持的游戏
- 根据游戏类型查询支持的提供商
- 获取特定游戏类型的游戏代码
- 检查提供商可用性

## 使用方法

### 1. 初始化配置管理器

```csharp
// 获取单例实例
var configManager = MSGameConfigManager.Instance;

// 加载配置文件
bool success = await configManager.LoadConfigAsync();
if (!success)
{
    Console.WriteLine("配置加载失败");
    return;
}
```

### 2. 查询游戏信息

```csharp
// 查询AG视讯支持的所有游戏
var agGames = configManager.GetGamesByProvider("AG");

// 查询所有支持真人游戏的提供商
var liveProviders = configManager.GetProvidersByGameType(1);

// 获取BBIN真人游戏的代码
string? gameCode = configManager.GetGameCode("BBIN", 1);

// 检查提供商是否可用
bool isAvailable = configManager.IsProviderAvailable("AG");
```

### 3. 创建游戏列表

```csharp
var gameList = new MSGameList();
gameList.gameList = new List<MSGame>
{
    new MSGame { apiCode = "AG", gameType = "1", gameCode = "0" },
    new MSGame { apiCode = "BBIN", gameType = "3", gameCode = "0" }
};
```

## 配置数据结构

### 游戏类型映射
```json
{
  "gameTypes": {
    "1": "真人",
    "2": "捕鱼",
    "3": "电子",
    "4": "彩票",
    "5": "体育",
    "6": "棋牌",
    "7": "电竞"
  }
}
```

### 提供商配置
```json
{
  "code": "AG",
  "name": "AG视讯",
  "status": null,
  "games": [
    {
      "gameType": 1,
      "gameCode": "0",
      "description": "真人"
    }
  ]
}
```

## 特殊说明

1. **动态游戏代码**: 部分游戏（如AG电子、AT电子等）的游戏代码需要从API动态获取，配置中标记为"请参考获取游戏列表gameCode字段"

2. **维护状态**: KA电子、JOKER电子、GMFX福星棋牌、AP爱棋牌等提供商当前处于维护中或已下架状态

3. **捕鱼游戏**: 捕鱼游戏分布在多个提供商中，每个提供商有不同的游戏代码

## 运行示例

```csharp
// 运行所有示例
await MSGameConfigExample.RunAllExamples();
```

这将展示：
- 配置初始化
- 游戏信息查询
- 游戏列表创建
- 批量游戏配置创建

## 注意事项

1. 配置文件路径默认为 `Services/GameApi/MSGameConfig.json`
2. 使用单例模式确保配置只加载一次
3. 所有查询方法都支持大小写不敏感的提供商代码
4. 建议在应用启动时初始化配置管理器
