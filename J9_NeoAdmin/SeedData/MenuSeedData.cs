namespace J9_NeoAdmin.SeedData
{
    /// <summary>
    /// 菜单种子数据 - 用于初始化系统菜单
    /// </summary>
    public static class MenuSeedData
    {
        /// <summary>
        /// 初始化捕鱼娱乐后台管理系统的菜单数据
        /// </summary>
        /// <param name="fsql">FreeSql实例</param>
        public static void Initialize(IFreeSql fsql)
        {
            NormalizeNullMenuTextFields(fsql);
            RemoveLegacyNoAdminMenus(fsql);
            EnsureCrudButtons(fsql);
            UpgradeFontAwesomeMenuIcons(fsql);
            NormalizeLegacyMenuIcons(fsql);

            var rootMenus = new[]
            {
                CreateUserAgentMenu(),
                CreateGameActivityMenu(),
                CreateFinanceMenu(),
                CreateOperationMenu(),
                CreateBlogMenu(),
                CreateApiMenu()
            };

            System.Action<SysMenu> insertMenu = menu => fsql.Insert(menu).ExecuteAffrows();

            // 递归检查菜单树：不存在则补齐
            foreach (var rootMenu in rootMenus)
            {
                EnsureMenuRecursive(fsql, insertMenu, rootMenu, null);
            }
        }

        /// <summary>
        /// 递归补齐菜单：当前节点不存在则新增，存在则继续检查其子节点
        /// </summary>
        private static void EnsureMenuRecursive(
            IFreeSql fsql,
            System.Action<SysMenu> insertMenu,
            SysMenu targetMenu,
            object parentId)
        {
            var currentMenu = FindMenuByParent(fsql, targetMenu, parentId);

            if (currentMenu == null)
            {
                var newMenu = CreateMenuWithoutChildren(targetMenu);
                SetParentIdIfSupported(newMenu, parentId);
                insertMenu(newMenu);
                currentMenu = FindMenuByParent(fsql, targetMenu, parentId);
            }

            if (currentMenu == null || targetMenu.Children == null || targetMenu.Children.Count == 0)
            {
                return;
            }

            var currentMenuId = GetMenuId(currentMenu);
            foreach (var child in targetMenu.Children)
            {
                EnsureMenuRecursive(fsql, insertMenu, child, currentMenuId);
            }
        }

        private static SysMenu FindMenuByParent(IFreeSql fsql, SysMenu menu, object parentId)
        {
            var sameNodeMenus = fsql.Select<SysMenu>()
                .Where(a => a.Label == menu.Label && a.Path == menu.Path && a.Type == menu.Type)
                .ToList();

            if (sameNodeMenus.Count == 0)
            {
                return null;
            }

            var parentIdProperty = GetParentIdProperty();
            if (parentIdProperty == null)
            {
                return sameNodeMenus.FirstOrDefault();
            }

            return sameNodeMenus.FirstOrDefault(x =>
            {
                var currentParentId = parentIdProperty.GetValue(x);
                if (parentId == null)
                {
                    return IsNullOrDefault(currentParentId);
                }

                return AreIdEqual(currentParentId, parentId);
            });
        }

        private static SysMenu CreateMenuWithoutChildren(SysMenu source)
        {
            return new SysMenu
            {
                Label = source.Label ?? "",
                Path = source.Path ?? "",
                Description = source.Description ?? "",
                Sort = source.Sort,
                Type = source.Type,
                Icon = source.Icon ?? "",
                IsHidden = source.IsHidden
            };
        }

        private static object GetMenuId(SysMenu menu)
        {
            var idProperty = typeof(SysMenu).GetProperty("Id")
                             ?? typeof(SysMenu).GetProperty("SysMenuId")
                             ?? typeof(SysMenu).GetProperty("MenuId");

            return idProperty?.GetValue(menu);
        }

        private static System.Reflection.PropertyInfo GetParentIdProperty()
        {
            return typeof(SysMenu).GetProperty("ParentId")
                   ?? typeof(SysMenu).GetProperty("Pid")
                   ?? typeof(SysMenu).GetProperty("ParentMenuId")
                   ?? typeof(SysMenu).GetProperty("ParentSysMenuId");
        }

        private static void SetParentIdIfSupported(SysMenu menu, object parentId)
        {
            var parentIdProperty = GetParentIdProperty();
            if (parentIdProperty == null || !parentIdProperty.CanWrite)
            {
                return;
            }

            if (parentId == null)
            {
                if (System.Nullable.GetUnderlyingType(parentIdProperty.PropertyType) != null)
                {
                    parentIdProperty.SetValue(menu, null);
                }

                return;
            }

            try
            {
                var targetType = System.Nullable.GetUnderlyingType(parentIdProperty.PropertyType)
                                 ?? parentIdProperty.PropertyType;

                var converted = targetType.IsAssignableFrom(parentId.GetType())
                    ? parentId
                    : System.Convert.ChangeType(parentId, targetType);

                parentIdProperty.SetValue(menu, converted);
            }
            catch
            {
                // 父ID类型无法转换时，忽略并保持默认值
            }
        }

        private static bool AreIdEqual(object left, object right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.ToString(), right.ToString(), System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNullOrDefault(object value)
        {
            if (value == null)
            {
                return true;
            }

            if (value is string s)
            {
                return string.IsNullOrWhiteSpace(s);
            }

            try
            {
                return value.Equals(System.Activator.CreateInstance(value.GetType()));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 将 SysMenu 字符串字段中的 NULL 归一化为空串。
        /// NeoAdmin MenuMatchesSearch 对 Label/Path/Description 调用 Contains，NULL 会触发 NullReferenceException 并断开 Blazor 电路。
        /// </summary>
        private static void NormalizeNullMenuTextFields(IFreeSql fsql)
        {
            fsql.Update<SysMenu>()
                .Set(a => a.Label, "")
                .Where(a => a.Label == null)
                .ExecuteAffrows();

            fsql.Update<SysMenu>()
                .Set(a => a.Path, "")
                .Where(a => a.Path == null)
                .ExecuteAffrows();

            fsql.Update<SysMenu>()
                .Set(a => a.Icon, "")
                .Where(a => a.Icon == null)
                .ExecuteAffrows();

            fsql.Update<SysMenu>()
                .Set(a => a.Description, "")
                .Where(a => a.Description == null)
                .ExecuteAffrows();
        }

        /// <summary>
        /// 清理 NoAdmin 时代的 Admin/* 系统菜单。
        /// 与 NeoAdmin 的 /admin/* 路径在搜索归一化后大小写不敏感冲突（如 Admin/Menu 与 /admin/menu）。
        /// </summary>
        private static void RemoveLegacyNoAdminMenus(IFreeSql fsql)
        {
            var legacyRootIds = fsql.Select<SysMenu>()
                .Where(a => a.Path.StartsWith("Admin/"))
                .ToList(a => a.Id);

            if (legacyRootIds.Count > 0)
            {
                var legacyIds = CollectDescendantMenuIds(fsql, legacyRootIds);
                RemoveMenuRelations(fsql, legacyIds);
                fsql.Delete<SysMenu>().Where(a => legacyIds.Contains(a.Id)).ExecuteAffrows();
            }

            // 同父级 + Path 重复页面菜单，保留 Id 最小的一条。
            // 按钮菜单（add/edit/remove 等）在不同父级下 Path 可重复，不能按 Path 全局去重。
            var duplicateIds = fsql.Select<SysMenu>()
                .Where(a => a.Path != null && a.Path != "" && a.Type != SysMenuType.按钮)
                .ToList()
                .GroupBy(a => $"{a.ParentId}|{a.Path}", System.StringComparer.OrdinalIgnoreCase)
                .SelectMany(g => g.OrderBy(a => a.Id).Skip(1).Select(a => a.Id))
                .ToList();

            if (duplicateIds.Count > 0)
            {
                RemoveMenuRelations(fsql, duplicateIds);
                fsql.Delete<SysMenu>().Where(a => duplicateIds.Contains(a.Id)).ExecuteAffrows();
            }
        }

        private static System.Collections.Generic.HashSet<long> CollectDescendantMenuIds(
            IFreeSql fsql,
            System.Collections.Generic.IEnumerable<long> rootIds)
        {
            var all = new System.Collections.Generic.HashSet<long>(rootIds);
            var queue = new System.Collections.Generic.Queue<long>(rootIds);
            while (queue.Count > 0)
            {
                var parentId = queue.Dequeue();
                var childIds = fsql.Select<SysMenu>()
                    .Where(a => a.ParentId == parentId)
                    .ToList(a => a.Id);
                foreach (var childId in childIds)
                {
                    if (all.Add(childId))
                    {
                        queue.Enqueue(childId);
                    }
                }
            }

            return all;
        }

        private static void RemoveMenuRelations(IFreeSql fsql, System.Collections.Generic.IEnumerable<long> menuIds)
        {
            var ids = menuIds.ToList();
            if (ids.Count == 0)
            {
                return;
            }

            fsql.Delete<SysRoleMenu>().Where(a => ids.Contains(a.MenuId)).ExecuteAffrows();
        }

        /// <summary>
        /// 为「增删改查」类型页面补齐 add/edit/remove 按钮。
        /// NeoAdmin 框架页（/admin/*）依赖这些按钮记录做权限判断；若被误删会导致操作列为空。
        /// </summary>
        private static void EnsureCrudButtons(IFreeSql fsql)
        {
            var crudPages = fsql.Select<SysMenu>()
                .Where(a => a.Type == SysMenuType.增删改查)
                .ToList();

            foreach (var page in crudPages)
            {
                EnsureCrudButton(fsql, page, "add", "添加", 301);
                EnsureCrudButton(fsql, page, "edit", "编辑", 302);
                EnsureCrudButton(fsql, page, "remove", "删除", 303);
            }
        }

        private static void EnsureCrudButton(
            IFreeSql fsql,
            SysMenu parent,
            string path,
            string label,
            int sort)
        {
            var exists = fsql.Select<SysMenu>()
                .Any(a => a.ParentId == parent.Id && a.Path == path);

            if (exists)
            {
                return;
            }

            fsql.Insert(new SysMenu
            {
                ParentId = parent.Id,
                Label = label,
                Path = path,
                Sort = sort,
                Type = SysMenuType.按钮,
                IsSystem = parent.IsSystem,
                IsHidden = false,
                Icon = "",
                Description = ""
            }).ExecuteAffrows();
        }

        /// <summary>
        /// 将数据库中旧版 Font Awesome 图标（fas fa-*）升级为 NeoUI Lucide 图标名。
        /// </summary>
        private static void UpgradeFontAwesomeMenuIcons(IFreeSql fsql)
        {
            var iconMap = FontAwesomeToLucideMap;
            var legacyMenus = fsql.Select<SysMenu>()
                .Where(a => a.Icon != null && a.Icon.Contains("fa-"))
                .ToList();

            foreach (var menu in legacyMenus)
            {
                if (iconMap.TryGetValue(menu.Icon.Trim(), out var lucideIcon))
                {
                    fsql.Update<SysMenu>()
                        .Set(a => a.Icon, lucideIcon)
                        .Where(a => a.Id == menu.Id)
                        .ExecuteAffrows();
                }
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> FontAwesomeToLucideMap =
            new(System.StringComparer.OrdinalIgnoreCase)
            {
                ["fas fa-users-cog"] = "users",
                ["fas fa-user-tie"] = "user-cog",
                ["fas fa-user-plus"] = "user-plus",
                ["fas fa-user-edit"] = "user-pen",
                ["fas fa-user-friends"] = "users",
                ["fas fa-user"] = "user",
                ["fas fa-gamepad"] = "gamepad-2",
                ["fas fa-server"] = "server",
                ["fas fa-dice"] = "dices",
                ["fas fa-check-circle"] = "circle-check",
                ["fas fa-money-bill-wave"] = "banknote",
                ["fas fa-money-bill"] = "banknote",
                ["fas fa-credit-card"] = "credit-card",
                ["fas fa-receipt"] = "receipt",
                ["fas fa-bullhorn"] = "megaphone",
                ["fas fa-flag"] = "flag",
                ["fas fa-comments"] = "messages-square",
                ["fas fa-images"] = "images",
                ["fas fa-star"] = "star",
                ["fas fa-tasks"] = "list-checks",
                ["fas fa-clipboard-list"] = "clipboard-list",
                ["fas fa-box-open"] = "package-open",
                ["fas fa-shield-alt"] = "shield",
                ["fas fa-blog"] = "book-open",
                ["fas fa-folder"] = "folder",
                ["fas fa-rss"] = "rss",
                ["fas fa-file-alt"] = "file-text",
                ["fas fa-tags"] = "tags",
                ["fas fa-comment"] = "message-square",
                ["fas fa-thumbs-up"] = "thumbs-up",
                ["fas fa-bookmark"] = "bookmark",
                ["fas fa-sign-in-alt"] = "log-in",
                ["fas fa-sign-out-alt"] = "log-out",
                ["fas fa-key"] = "key",
                ["fas fa-image"] = "image",
                ["fas fa-wallet"] = "wallet",
                ["fas fa-handshake"] = "handshake",
                ["fas fa-unlock-alt"] = "unlock",
                ["fas fa-calendar-check"] = "calendar-check",
                ["fas fa-info-circle"] = "info",
                ["fas fa-search"] = "search",
                ["fas fa-database"] = "database",
                ["fas fa-building"] = "building",
                ["fas fa-list"] = "list",
                ["fas fa-list-alt"] = "list",
                ["fas fa-play-circle"] = "circle-play",
                ["fas fa-stop-circle"] = "circle-stop",
                ["fas fa-recycle"] = "recycle",
                ["fas fa-history"] = "history",
                ["fas fa-coins"] = "coins",
                ["fas fa-exchange-alt"] = "arrow-left-right",
                ["fas fa-calendar-alt"] = "calendar",
                ["fas fa-shopping-cart"] = "shopping-cart",
                ["fas fa-minus-circle"] = "circle-minus",
                ["fas fa-percentage"] = "percent",
                ["fas fa-chart-line"] = "chart-line",
                ["fas fa-sync-alt"] = "refresh-cw",
                ["fas fa-reply"] = "reply",
                ["fas fa-paper-plane"] = "send",
                ["fas fa-envelope-open-text"] = "mail-check",
                ["fas fa-envelope-open"] = "mail-open",
                ["fas fa-trash-alt"] = "trash-2",
                ["fas fa-clock"] = "clock",
                ["fas fa-gift"] = "gift",
                ["fas fa-chart-bar"] = "chart-bar",
                ["fas fa-code"] = "code",
                ["fas fa-gears"] = "settings",
                ["fa-home"] = "house",
                ["home"] = "house",
            };

        /// <summary>
        /// 将数据库中已迁移但仍使用无效 Lucide 别名的图标名修正为有效名称。
        /// </summary>
        private static void NormalizeLegacyMenuIcons(IFreeSql fsql)
        {
            var legacyMap = new System.Collections.Generic.Dictionary<string, string>(
                System.StringComparer.OrdinalIgnoreCase)
            {
                ["home"] = "house",
            };

            foreach (var pair in legacyMap)
            {
                fsql.Update<SysMenu>()
                    .Set(a => a.Icon, pair.Value)
                    .Where(a => a.Icon == pair.Key)
                    .ExecuteAffrows();
            }
        }

        // =====================================================
        // 后台管理菜单（侧边栏展示）
        // =====================================================

        /// <summary>
        /// 用户与代理管理菜单
        /// </summary>
        private static SysMenu CreateUserAgentMenu()
        {
            return new SysMenu
            {
                Label = "用户与代理",
                Path = "",
                Sort = 10,
                Type = SysMenuType.菜单,
                Icon = "users",
                Children = new List<SysMenu>
                {
                    new SysMenu
                    {
                        Label = "会员列表", Path = "Ddd/DMember", Sort = 101, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "user"
                    },
                    new SysMenu
                    {
                        Label = "代理列表", Path = "Ddd/DAgent", Sort = 102, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "user-cog"
                    },
                    new SysMenu
                    {
                        Label = "代理结算", Path = "Ddd/DAgentSettlement", Sort = 103, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "user-cog"
                    }
                }
            };
        }

        /// <summary>
        /// 游戏与活动菜单
        /// </summary>
        private static SysMenu CreateGameActivityMenu()
        {
            return new SysMenu
            {
                Label = "游戏与活动",
                Path = "",
                Sort = 20,
                Type = SysMenuType.菜单,
                Icon = "gamepad-2",
                Children = new List<SysMenu>
                {
                    new SysMenu
                    {
                        Label = "游戏平台", Path = "Ddd/DGamePlatform", Sort = 201, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "server"
                    },
                    new SysMenu
                    {
                        Label = "游戏编辑", Path = "Ddd/DGame", Sort = 202, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "dices"
                    },
                    new SysMenu
                    {
                        Label = "已审游戏", Path = "Ddd/DGameReviewed", Sort = 203, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "circle-check"
                    }
                }
            };
        }

        /// <summary>
        /// 财务与交易菜单
        /// </summary>
        private static SysMenu CreateFinanceMenu()
        {
            return new SysMenu
            {
                Label = "财务与交易",
                Path = "",
                Sort = 30,
                Type = SysMenuType.菜单,
                Icon = "banknote",
                Children = new List<SysMenu>
                {
                    new SysMenu
                    {
                        Label = "支付通道", Path = "Ddd/DPayApi", Sort = 301, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "credit-card"
                    },
                    new SysMenu
                    {
                        Label = "交易记录", Path = "Ddd/DTransAction", Sort = 302, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "receipt"
                    }
                }
            };
        }

        /// <summary>
        /// 运营与消息菜单
        /// </summary>
        private static SysMenu CreateOperationMenu()
        {
            return new SysMenu
            {
                Label = "运营与消息",
                Path = "",
                Sort = 40,
                Type = SysMenuType.菜单,
                Icon = "megaphone",
                Children = new List<SysMenu>
                {
                    new SysMenu
                    {
                        Label = "平台公告", Path = "Ddd/DNotice", Sort = 401, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "flag"
                    },
                    new SysMenu
                    {
                        Label = "用户消息", Path = "Ddd/DMessages", Sort = 402, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "messages-square"
                    },
                    new SysMenu
                    {
                        Label = "轮播图", Path = "Ddd/DBanner", Sort = 403, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "images"
                    },
                    new SysMenu
                    {
                        Label = "活动列表", Path = "Ddd/DEvent", Sort = 404, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "star"
                    },
                    new SysMenu
                    {
                        Label = "每日任务", Path = "Ddd/DTask", Sort = 405, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "list-checks"
                    },
                    new SysMenu
                    {
                        Label = "会员任务记录", Path = "Ddd/DMemberTask", Sort = 406, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "clipboard-list"
                    },
                    new SysMenu
                    {
                        Label = "会员宝箱记录", Path = "Ddd/DMemberChest", Sort = 407, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "package-open"
                    }
                }
            };
        }

        /// <summary>
        /// 博客管理菜单
        /// </summary>
        private static SysMenu CreateBlogMenu()
        {
            return new SysMenu
            {
                Label = "博客管理",
                Path = "",
                Sort = 45,
                Type = SysMenuType.菜单,
                Icon = "book-open",
                Children = new List<SysMenu>
                {
                    new SysMenu
                    {
                        Label = "分类", Path = "Blog/Classify", Sort = 451, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "folder"
                    },
                    new SysMenu
                    {
                        Label = "频道", Path = "Blog/Channel", Sort = 452, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "rss"
                    },
                    new SysMenu
                    {
                        Label = "文章", Path = "Blog/Article", Sort = 453, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "file-text"
                    },
                    new SysMenu
                    {
                        Label = "标签", Path = "Blog/Tag2", Sort = 454, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "tags"
                    },
                    new SysMenu
                    {
                        Label = "评论", Path = "Blog/Comment", Sort = 455, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "message-square"
                    },
                    new SysMenu
                    {
                        Label = "用户点赞", Path = "Blog/UserLike", Sort = 456, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "thumbs-up"
                    },
                    new SysMenu
                    {
                        Label = "收藏", Path = "Blog/Collection", Sort = 457, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "bookmark"
                    }
                }
            };
        }

        // =====================================================
        // Api 隐藏菜单（权限控制用）
        // =====================================================

        /// <summary>
        /// 创建API隐藏菜单，用于前端接口权限控制
        /// </summary>
        private static SysMenu CreateApiMenu()
        {
            return new SysMenu
            {
                Label = "Api",
                Path = "",
                Sort = 0,
                Type = SysMenuType.菜单,
                IsHidden = true,
                Children = new List<SysMenu>
                {
                    // LoginService - Route: api/login
                    new SysMenu
                    {
                        Label = "Login",
                        Path = "login",
                        Sort = 100,
                        Type = SysMenuType.菜单,
                        Icon = "log-in",
                        Children = new List<SysMenu>
                        {
                            new SysMenu { Label = "Register", Path = "Register", Sort = 101, Type = SysMenuType.按钮, Icon = "user-plus" },
                            new SysMenu { Label = "Login", Path = "Login", Sort = 102, Type = SysMenuType.按钮, Icon = "log-in" },
                            new SysMenu { Label = "Logout", Path = "Logout", Sort = 103, Type = SysMenuType.按钮, Icon = "log-out" },
                            new SysMenu { Label = "Check", Path = "Check", Sort = 104, Type = SysMenuType.按钮, Icon = "circle-check" },
                            new SysMenu { Label = "ChangePassword", Path = "ChangePassword", Sort = 105, Type = SysMenuType.按钮, Icon = "key" },
                            new SysMenu { Label = "UploadAvatar", Path = "UploadAvatar", Sort = 106, Type = SysMenuType.按钮, Icon = "image" },
                            new SysMenu { Label = "UpdateMemberInfo", Path = "UpdateMemberInfo", Sort = 107, Type = SysMenuType.按钮, Icon = "user-pen" },
                            new SysMenu { Label = "GetBalance", Path = "GetBalance", Sort = 108, Type = SysMenuType.按钮, Icon = "wallet" },
                            new SysMenu { Label = "ApplyAgent", Path = "ApplyAgent", Sort = 109, Type = SysMenuType.按钮, Icon = "handshake" },
                            new SysMenu { Label = "ResetPassword", Path = "ResetPassword", Sort = 110, Type = SysMenuType.按钮, Icon = "unlock" },
                            new SysMenu { Label = "PlayerCheckIn", Path = "PlayerCheckIn", Sort = 111, Type = SysMenuType.按钮, Icon = "calendar-check" },
                            new SysMenu { Label = "GetAgentInfo", Path = "GetAgentInfo", Sort = 112, Type = SysMenuType.按钮, Icon = "info" },
                            new SysMenu { Label = "GetAgentInfo2", Path = "GetAgentInfo2", Sort = 113, Type = SysMenuType.按钮, Icon = "search" },
                            new SysMenu { Label = "GetInviteCenter", Path = "GetInviteCenter", Sort = 114, Type = SysMenuType.按钮, Icon = "users" },
                            new SysMenu { Label = "InitDbData", Path = "InitDbData", Sort = 115, Type = SysMenuType.按钮, Icon = "database" },
                            new SysMenu
                            {
                                Label = "ChangeWithdrawPassword", Path = "ChangeWithdrawPassword", Sort = 116,
                                Type = SysMenuType.按钮,
                                Icon = "key"
                            },
                            new SysMenu
                            {
                                Label = "GetTenantInfo", Path = "GetTenantInfo", Sort = 117,
                                Type = SysMenuType.按钮,
                                Icon = "building"
                            },
                        }
                    },

                    // GameService - Route: api/game
                    new SysMenu
                    {
                        Label = "Game",
                        Path = "game",
                        Sort = 200,
                        Type = SysMenuType.菜单,
                        Icon = "gamepad-2",
                        Children = new List<SysMenu>
                        {
                            new SysMenu { Label = "GetGameList", Path = "GetGameList", Sort = 201, Type = SysMenuType.按钮, Icon = "list" },
                            new SysMenu { Label = "GetMSGameList", Path = "GetMSGameList", Sort = 211, Type = SysMenuType.按钮, Icon = "list" },
                            new SysMenu { Label = "StartMSGame", Path = "StartMSGame", Sort = 212, Type = SysMenuType.按钮, Icon = "circle-play" },
                            new SysMenu { Label = "EndMSGame", Path = "EndMSGame", Sort = 213, Type = SysMenuType.按钮, Icon = "circle-stop" },
                            new SysMenu
                            {
                                Label = "RecycleRecentTransferInMSGames",
                                Path = "RecycleRecentTransferInMSGames",
                                Sort = 214,
                                Type = SysMenuType.按钮,
                                Icon = "recycle"
                            },
                            new SysMenu { Label = "GetMSGameHistory", Path = "GetMSGameHistory", Sort = 215, Type = SysMenuType.按钮, Icon = "history" },
                            new SysMenu { Label = "GetMSGameBalance", Path = "GetMSGameBalance", Sort = 216, Type = SysMenuType.按钮, Icon = "coins" },
                            new SysMenu { Label = "StartXHGame", Path = "StartXHGame", Sort = 217, Type = SysMenuType.按钮, Icon = "circle-play" },
                            new SysMenu { Label = "EndXHGame", Path = "EndXHGame", Sort = 218, Type = SysMenuType.按钮, Icon = "circle-stop" },
                            new SysMenu
                            {
                                Label = "RecycleRecentTransferInXHGames",
                                Path = "RecycleRecentTransferInXHGames",
                                Sort = 219,
                                Type = SysMenuType.按钮,
                                Icon = "recycle"
                            },
                            new SysMenu { Label = "GetXHGameHistory", Path = "GetXHGameHistory", Sort = 220, Type = SysMenuType.按钮, Icon = "history" },
                            new SysMenu { Label = "GetXHGameList", Path = "GetXHGameList", Sort = 221, Type = SysMenuType.按钮, Icon = "list" },
                            new SysMenu { Label = "GetXHGameBalance", Path = "GetXHGameBalance", Sort = 222, Type = SysMenuType.按钮, Icon = "coins" },
                        }
                    },

                    // TransActionService - Route: api/trans
                    new SysMenu
                    {
                        Label = "Trans",
                        Path = "trans",
                        Sort = 300,
                        Type = SysMenuType.菜单,
                        Icon = "arrow-left-right",
                        Children = new List<SysMenu>
                        {
                            new SysMenu { Label = "GetTransActionList", Path = "GetTransActionList", Sort = 301, Type = SysMenuType.按钮, Icon = "list" },
                            new SysMenu
                            {
                                Label = "GetTransActionMonthSummary", Path = "GetTransActionMonthSummary", Sort = 302,
                                Type = SysMenuType.按钮,
                                Icon = "calendar"
                            },
                            new SysMenu { Label = "CreateMemberRechargeOrder", Path = "CreateMemberRechargeOrder", Sort = 303, Type = SysMenuType.按钮, Icon = "shopping-cart" },
                            new SysMenu
                            {
                                Label = "PlayerWithdraw", Path = "PlayerWithdraw", Sort = 304,
                                Type = SysMenuType.按钮,
                                Icon = "circle-minus"
                            },
                            new SysMenu
                            {
                                Label = "PlayerRebate", Path = "PlayerRebate", Sort = 305,
                                Type = SysMenuType.按钮,
                                Icon = "percent"
                            },
                            new SysMenu
                            {
                                Label = "GetPayApiList", Path = "GetPayApiList", Sort = 306,
                                Type = SysMenuType.按钮,
                                Icon = "list"
                            },
                            new SysMenu
                            {
                                Label = "GetRecentPlayerActivity", Path = "GetRecentPlayerActivity", Sort = 307,
                                Type = SysMenuType.按钮,
                                Icon = "chart-line"
                            },
                            new SysMenu
                            {
                                Label = "SyncBetHistoryToDatabaseAsync", Path = "SyncBetHistoryToDatabaseAsync", Sort = 308,
                                Type = SysMenuType.按钮,
                                Icon = "refresh-cw"
                            },
                            // 支付0 - TokenPay
                            new SysMenu
                            {
                                Label = "CreatePay0Order", Path = "CreatePay0Order", Sort = 311,
                                Type = SysMenuType.按钮,
                                Icon = "banknote"
                            },
                            new SysMenu
                            {
                                Label = "Pay0Callback", Path = "Pay0Callback", Sort = 312,
                                Type = SysMenuType.按钮,
                                Icon = "reply"
                            },
                            // POPO支付 - 青蛙系统四方支付
                            new SysMenu
                            {
                                Label = "CreatePayPOPOOrder", Path = "CreatePayPOPOOrder", Sort = 313,
                                Type = SysMenuType.按钮,
                                Icon = "banknote"
                            },
                            new SysMenu
                            {
                                Label = "PayPOPOCallback", Path = "PayPOPOCallback", Sort = 314,
                                Type = SysMenuType.按钮,
                                Icon = "reply"
                            },
                        }
                    },

                    // MessageService - Route: api/message
                    new SysMenu
                    {
                        Label = "Message",
                        Path = "message",
                        Sort = 400,
                        Type = SysMenuType.菜单,
                        Icon = "messages-square",
                        Children = new List<SysMenu>
                        {
                            new SysMenu
                            {
                                Label = "GetMessages", Path = "GetMessages", Sort = 401,
                                Type = SysMenuType.按钮,
                                Icon = "list"
                            },
                            new SysMenu
                            {
                                Label = "SendMessage", Path = "SendMessage", Sort = 402,
                                Type = SysMenuType.按钮,
                                Icon = "send"
                            },
                            new SysMenu
                            {
                                Label = "MarkAsRead", Path = "MarkAsRead", Sort = 403,
                                Type = SysMenuType.按钮,
                                Icon = "mail-open"
                            },
                            new SysMenu
                            {
                                Label = "MarkAllAsRead", Path = "MarkAllAsRead", Sort = 404,
                                Type = SysMenuType.按钮,
                                Icon = "mail-check"
                            },
                            new SysMenu
                            {
                                Label = "DeleteMessage", Path = "DeleteMessage", Sort = 405,
                                Type = SysMenuType.按钮,
                                Icon = "trash-2"
                            },
                        }
                    },

                    // NoticeService - Route: api/notice
                    new SysMenu
                    {
                        Label = "Notice",
                        Path = "notice",
                        Sort = 500,
                        Type = SysMenuType.菜单,
                        Icon = "megaphone",
                        Children = new List<SysMenu>
                        {
                            new SysMenu
                            {
                                Label = "GetNotices", Path = "GetNotices", Sort = 501,
                                Type = SysMenuType.按钮,
                                Icon = "list"
                            },
                        }
                    },

                    // BannerService - Route: api/banner
                    new SysMenu
                    {
                        Label = "Banner",
                        Path = "banner",
                        Sort = 600,
                        Type = SysMenuType.菜单,
                        Icon = "images",
                        Children = new List<SysMenu>
                        {
                            new SysMenu
                            {
                                Label = "GetBanners", Path = "GetBanners", Sort = 601,
                                Type = SysMenuType.按钮,
                                Icon = "list"
                            },
                        }
                    },

                    // EventService - Route: api/event
                    new SysMenu
                    {
                        Label = "Event",
                        Path = "event",
                        Sort = 700,
                        Type = SysMenuType.菜单,
                        Icon = "star",
                        Children = new List<SysMenu>
                        {
                            new SysMenu
                            {
                                Label = "GetCheckInStatus", Path = "GetCheckInStatus", Sort = 701,
                                Type = SysMenuType.按钮,
                                Icon = "calendar-check"
                            },
                            new SysMenu
                            {
                                Label = "GetTimeLimitedEvents", Path = "GetTimeLimitedEvents", Sort = 702,
                                Type = SysMenuType.按钮,
                                Icon = "clock"
                            },
                            new SysMenu
                            {
                                Label = "GetDailyTasks", Path = "GetDailyTasks", Sort = 703,
                                Type = SysMenuType.按钮,
                                Icon = "list-checks"
                            },
                            new SysMenu
                            {
                                Label = "ClaimDailyTask", Path = "ClaimDailyTask", Sort = 704,
                                Type = SysMenuType.按钮,
                                Icon = "gift"
                            },
                            new SysMenu
                            {
                                Label = "ClaimActivityChest", Path = "ClaimActivityChest", Sort = 705,
                                Type = SysMenuType.按钮,
                                Icon = "package-open"
                            },
                            new SysMenu
                            {
                                Label = "GetMonthlyCheckIn", Path = "GetMonthlyCheckIn", Sort = 706,
                                Type = SysMenuType.按钮,
                                Icon = "calendar"
                            },
                            new SysMenu
                            {
                                Label = "GetMonthlyTaskActivity", Path = "GetMonthlyTaskActivity", Sort = 707,
                                Type = SysMenuType.按钮,
                                Icon = "chart-bar"
                            },
                        }
                    },
                },
                Icon = "code",
            };
        }

        /// <summary>
        /// 获取增删改按钮
        /// </summary>
        private static List<SysMenu> GetCrudButtons(params SysMenu[] additionalButtons)
        {
            return new[]
            {
                new SysMenu { Label = "添加", Path = "add", Sort = 901, Type = SysMenuType.按钮, Icon = "", Description = "" },
                new SysMenu { Label = "编辑", Path = "edit", Sort = 902, Type = SysMenuType.按钮, Icon = "", Description = "" },
                new SysMenu { Label = "删除", Path = "remove", Sort = 903, Type = SysMenuType.按钮, Icon = "", Description = "" }
            }.Concat(additionalButtons).ToList();
        }
    }
}
