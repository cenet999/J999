https://nm.800800.win/

admin,admin

http://103.119.12.121:3009/

---
变更记录（实体/后台）：
- DGame（ddd_game）：增加 IsTestPassed（测试是否通过）、TestTime（测试时间）；游戏管理页 _DGame.razor 已增加列表列与编辑项；列表筛选增加「测试通过」「接口代码」（接口代码选项来自当前库中去重 ApiCode）；「测试启动」成功写库后自动 `adminTable.Load()` 刷新列表。数据库由 FreeSql 结构同步时自动加列，请按环境执行同步或迁移。
- GameService：`StartMSGameAdminTestAsync` / `StartXHGameAdminTestAsync`（[NonAction]，仅供后台注入）用于游戏列表「测试启动」——不从主钱包上分；游戏侧余额查询失败时按 0 继续；允许已禁用游戏拉取启动链接；不计入 PlayGame 任务。会员端仍走原 `StartMSGame` / `StartXHGame` HTTP 接口。