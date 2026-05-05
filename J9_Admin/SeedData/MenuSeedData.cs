
using BootstrapBlazor.Components;

namespace J9_Admin.SeedData
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
        public static void Initialize(FreeSqlCloud fsql)
        {
            var repo = fsql.GetAggregateRootRepository<SysMenu>();

            var rootMenus = new[]
            {
                CreateUserAgentMenu(),
                CreateGameActivityMenu(),
                CreateFinanceMenu(),
                CreateOperationMenu(),
                CreateBlogMenu(),
                CreateApiMenu()
            };

            System.Action<SysMenu> insertMenu = menu => repo.Insert(menu);

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
            FreeSqlCloud fsql,
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

        private static SysMenu FindMenuByParent(FreeSqlCloud fsql, SysMenu menu, object parentId)
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
                Label = source.Label,
                Path = source.Path,
                Sort = source.Sort,
                Type = source.Type,
                Icon = source.Icon,
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
                Icon = "fas fa-users-cog",
                Children = new List<SysMenu>
                {
                    new SysMenu
                    {
                        Label = "会员列表", Path = "Ddd/DMember", Sort = 101, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-user"
                    },
                    new SysMenu
                    {
                        Label = "代理列表", Path = "Ddd/DAgent", Sort = 102, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-user-tie"
                    },
                    new SysMenu
                    {
                        Label = "代理结算", Path = "Ddd/DAgentSettlement", Sort = 103, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-user-tie"
                    },
                    new SysMenu
                    {
                        Label = "IP白名单", Path = "Ddd/IpWhitelist", Sort = 104, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-shield-alt"
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
                Icon = "fas fa-gamepad",
                Children = new List<SysMenu>
                {
                    new SysMenu
                    {
                        Label = "游戏平台", Path = "Ddd/DGamePlatform", Sort = 201, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-server"
                    },
                    new SysMenu
                    {
                        Label = "游戏编辑", Path = "Ddd/DGame", Sort = 202, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-dice"
                    },
                    new SysMenu
                    {
                        Label = "已审游戏", Path = "Ddd/DGameReviewed", Sort = 203, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-check-circle"
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
                Icon = "fas fa-money-bill-wave",
                Children = new List<SysMenu>
                {
                    new SysMenu
                    {
                        Label = "支付通道", Path = "Ddd/DPayApi", Sort = 301, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-credit-card"
                    },
                    new SysMenu
                    {
                        Label = "交易记录", Path = "Ddd/DTransAction", Sort = 302, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-receipt"
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
                Icon = "fas fa-bullhorn",
                Children = new List<SysMenu>
                {
                    new SysMenu
                    {
                        Label = "平台公告", Path = "Ddd/DNotice", Sort = 401, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-flag"
                    },
                    new SysMenu
                    {
                        Label = "用户消息", Path = "Ddd/DMessages", Sort = 402, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-comments"
                    },
                    new SysMenu
                    {
                        Label = "轮播图", Path = "Ddd/DBanner", Sort = 403, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-images"
                    },
                    new SysMenu
                    {
                        Label = "活动列表", Path = "Ddd/DEvent", Sort = 404, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-star"
                    },
                    new SysMenu
                    {
                        Label = "每日任务", Path = "Ddd/DTask", Sort = 405, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-tasks"
                    },
                    new SysMenu
                    {
                        Label = "会员任务记录", Path = "Ddd/DMemberTask", Sort = 406, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-clipboard-list"
                    },
                    new SysMenu
                    {
                        Label = "会员宝箱记录", Path = "Ddd/DMemberChest", Sort = 407, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-box-open"
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
                Icon = "fas fa-blog",
                Children = new List<SysMenu>
                {
                    new SysMenu
                    {
                        Label = "分类", Path = "Blog/Classify", Sort = 451, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-folder"
                    },
                    new SysMenu
                    {
                        Label = "频道", Path = "Blog/Channel", Sort = 452, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-rss"
                    },
                    new SysMenu
                    {
                        Label = "文章", Path = "Blog/Article", Sort = 453, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-file-alt"
                    },
                    new SysMenu
                    {
                        Label = "标签", Path = "Blog/Tag2", Sort = 454, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-tags"
                    },
                    new SysMenu
                    {
                        Label = "评论", Path = "Blog/Comment", Sort = 455, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-comment"
                    },
                    new SysMenu
                    {
                        Label = "用户点赞", Path = "Blog/UserLike", Sort = 456, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-thumbs-up"
                    },
                    new SysMenu
                    {
                        Label = "收藏", Path = "Blog/Collection", Sort = 457, Type = SysMenuType.菜单,
                        Children = GetCrudButtons(), Icon = "fas fa-bookmark"
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
                        Icon = "fas fa-sign-in-alt",
                        Children = new List<SysMenu>
                        {
                            new SysMenu { Label = "Register", Path = "Register", Sort = 101, Type = SysMenuType.按钮, Icon = "fas fa-user-plus" },
                            new SysMenu { Label = "Login", Path = "Login", Sort = 102, Type = SysMenuType.按钮, Icon = "fas fa-sign-in-alt" },
                            new SysMenu { Label = "Logout", Path = "Logout", Sort = 103, Type = SysMenuType.按钮, Icon = "fas fa-sign-out-alt" },
                            new SysMenu { Label = "Check", Path = "Check", Sort = 104, Type = SysMenuType.按钮, Icon = "fas fa-check-circle" },
                            new SysMenu { Label = "ChangePassword", Path = "ChangePassword", Sort = 105, Type = SysMenuType.按钮, Icon = "fas fa-key" },
                            new SysMenu { Label = "UploadAvatar", Path = "UploadAvatar", Sort = 106, Type = SysMenuType.按钮, Icon = "fas fa-image" },
                            new SysMenu { Label = "UpdateMemberInfo", Path = "UpdateMemberInfo", Sort = 107, Type = SysMenuType.按钮, Icon = "fas fa-user-edit" },
                            new SysMenu { Label = "GetBalance", Path = "GetBalance", Sort = 108, Type = SysMenuType.按钮, Icon = "fas fa-wallet" },
                            new SysMenu { Label = "ApplyAgent", Path = "ApplyAgent", Sort = 109, Type = SysMenuType.按钮, Icon = "fas fa-handshake" },
                            new SysMenu { Label = "ResetPassword", Path = "ResetPassword", Sort = 110, Type = SysMenuType.按钮, Icon = "fas fa-unlock-alt" },
                            new SysMenu { Label = "PlayerCheckIn", Path = "PlayerCheckIn", Sort = 111, Type = SysMenuType.按钮, Icon = "fas fa-calendar-check" },
                            new SysMenu { Label = "GetAgentInfo", Path = "GetAgentInfo", Sort = 112, Type = SysMenuType.按钮, Icon = "fas fa-info-circle" },
                            new SysMenu { Label = "GetAgentInfo2", Path = "GetAgentInfo2", Sort = 113, Type = SysMenuType.按钮, Icon = "fas fa-search" },
                            new SysMenu { Label = "GetInviteCenter", Path = "GetInviteCenter", Sort = 114, Type = SysMenuType.按钮, Icon = "fas fa-user-friends" },
                            new SysMenu { Label = "InitDbData", Path = "InitDbData", Sort = 115, Type = SysMenuType.按钮, Icon = "fas fa-database" },
                            new SysMenu
                            {
                                Label = "ChangeWithdrawPassword", Path = "ChangeWithdrawPassword", Sort = 116,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-key"
                            },
                            new SysMenu
                            {
                                Label = "GetTenantInfo", Path = "GetTenantInfo", Sort = 117,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-building"
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
                        Icon = "fas fa-gamepad",
                        Children = new List<SysMenu>
                        {
                            new SysMenu { Label = "GetGameList", Path = "GetGameList", Sort = 201, Type = SysMenuType.按钮, Icon = "fas fa-list" },
                            new SysMenu { Label = "GetMSGameList", Path = "GetMSGameList", Sort = 211, Type = SysMenuType.按钮, Icon = "fas fa-list" },
                            new SysMenu { Label = "StartMSGame", Path = "StartMSGame", Sort = 212, Type = SysMenuType.按钮, Icon = "fas fa-play-circle" },
                            new SysMenu { Label = "EndMSGame", Path = "EndMSGame", Sort = 213, Type = SysMenuType.按钮, Icon = "fas fa-stop-circle" },
                            new SysMenu
                            {
                                Label = "RecycleRecentTransferInMSGames",
                                Path = "RecycleRecentTransferInMSGames",
                                Sort = 214,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-recycle"
                            },
                            new SysMenu { Label = "GetMSGameHistory", Path = "GetMSGameHistory", Sort = 215, Type = SysMenuType.按钮, Icon = "fas fa-history" },
                            new SysMenu { Label = "GetMSGameBalance", Path = "GetMSGameBalance", Sort = 216, Type = SysMenuType.按钮, Icon = "fas fa-coins" },
                            new SysMenu { Label = "StartXHGame", Path = "StartXHGame", Sort = 217, Type = SysMenuType.按钮, Icon = "fas fa-play-circle" },
                            new SysMenu { Label = "EndXHGame", Path = "EndXHGame", Sort = 218, Type = SysMenuType.按钮, Icon = "fas fa-stop-circle" },
                            new SysMenu
                            {
                                Label = "RecycleRecentTransferInXHGames",
                                Path = "RecycleRecentTransferInXHGames",
                                Sort = 219,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-recycle"
                            },
                            new SysMenu { Label = "GetXHGameHistory", Path = "GetXHGameHistory", Sort = 220, Type = SysMenuType.按钮, Icon = "fas fa-history" },
                            new SysMenu { Label = "GetXHGameList", Path = "GetXHGameList", Sort = 221, Type = SysMenuType.按钮, Icon = "fas fa-list" },
                            new SysMenu { Label = "GetXHGameBalance", Path = "GetXHGameBalance", Sort = 222, Type = SysMenuType.按钮, Icon = "fas fa-coins" },
                        }
                    },

                    // TransActionService - Route: api/trans
                    new SysMenu
                    {
                        Label = "Trans",
                        Path = "trans",
                        Sort = 300,
                        Type = SysMenuType.菜单,
                        Icon = "fas fa-exchange-alt",
                        Children = new List<SysMenu>
                        {
                            new SysMenu { Label = "GetTransActionList", Path = "GetTransActionList", Sort = 301, Type = SysMenuType.按钮, Icon = "fas fa-list" },
                            new SysMenu
                            {
                                Label = "GetTransActionMonthSummary", Path = "GetTransActionMonthSummary", Sort = 302,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-calendar-alt"
                            },
                            new SysMenu { Label = "CreateMemberRechargeOrder", Path = "CreateMemberRechargeOrder", Sort = 303, Type = SysMenuType.按钮, Icon = "fas fa-shopping-cart" },
                            new SysMenu
                            {
                                Label = "PlayerWithdraw", Path = "PlayerWithdraw", Sort = 304,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-minus-circle"
                            },
                            new SysMenu
                            {
                                Label = "PlayerRebate", Path = "PlayerRebate", Sort = 305,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-percentage"
                            },
                            new SysMenu
                            {
                                Label = "GetPayApiList", Path = "GetPayApiList", Sort = 306,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-list-alt"
                            },
                            new SysMenu
                            {
                                Label = "GetRecentPlayerActivity", Path = "GetRecentPlayerActivity", Sort = 307,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-chart-line"
                            },
                            new SysMenu
                            {
                                Label = "SyncBetHistoryToDatabaseAsync", Path = "SyncBetHistoryToDatabaseAsync", Sort = 308,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-sync-alt"
                            },
                            // 支付0 - TokenPay
                            new SysMenu
                            {
                                Label = "CreatePay0Order", Path = "CreatePay0Order", Sort = 311,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-money-bill"
                            },
                            new SysMenu
                            {
                                Label = "Pay0Callback", Path = "Pay0Callback", Sort = 312,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-reply"
                            },
                            // POPO支付 - 青蛙系统四方支付
                            new SysMenu
                            {
                                Label = "CreatePayPOPOOrder", Path = "CreatePayPOPOOrder", Sort = 313,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-money-bill"
                            },
                            new SysMenu
                            {
                                Label = "PayPOPOCallback", Path = "PayPOPOCallback", Sort = 314,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-reply"
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
                        Icon = "fas fa-comments",
                        Children = new List<SysMenu>
                        {
                            new SysMenu
                            {
                                Label = "GetMessages", Path = "GetMessages", Sort = 401,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-list"
                            },
                            new SysMenu
                            {
                                Label = "SendMessage", Path = "SendMessage", Sort = 402,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-paper-plane"
                            },
                            new SysMenu
                            {
                                Label = "MarkAsRead", Path = "MarkAsRead", Sort = 403,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-envelope-open"
                            },
                            new SysMenu
                            {
                                Label = "MarkAllAsRead", Path = "MarkAllAsRead", Sort = 404,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-envelope-open-text"
                            },
                            new SysMenu
                            {
                                Label = "DeleteMessage", Path = "DeleteMessage", Sort = 405,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-trash-alt"
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
                        Icon = "fas fa-bullhorn",
                        Children = new List<SysMenu>
                        {
                            new SysMenu
                            {
                                Label = "GetNotices", Path = "GetNotices", Sort = 501,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-list"
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
                        Icon = "fas fa-images",
                        Children = new List<SysMenu>
                        {
                            new SysMenu
                            {
                                Label = "GetBanners", Path = "GetBanners", Sort = 601,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-list"
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
                        Icon = "fas fa-star",
                        Children = new List<SysMenu>
                        {
                            new SysMenu
                            {
                                Label = "GetCheckInStatus", Path = "GetCheckInStatus", Sort = 701,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-calendar-check"
                            },
                            new SysMenu
                            {
                                Label = "GetTimeLimitedEvents", Path = "GetTimeLimitedEvents", Sort = 702,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-clock"
                            },
                            new SysMenu
                            {
                                Label = "GetDailyTasks", Path = "GetDailyTasks", Sort = 703,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-tasks"
                            },
                            new SysMenu
                            {
                                Label = "ClaimDailyTask", Path = "ClaimDailyTask", Sort = 704,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-gift"
                            },
                            new SysMenu
                            {
                                Label = "ClaimActivityChest", Path = "ClaimActivityChest", Sort = 705,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-box-open"
                            },
                            new SysMenu
                            {
                                Label = "GetMonthlyCheckIn", Path = "GetMonthlyCheckIn", Sort = 706,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-calendar-alt"
                            },
                            new SysMenu
                            {
                                Label = "GetMonthlyTaskActivity", Path = "GetMonthlyTaskActivity", Sort = 707,
                                Type = SysMenuType.按钮,
                                Icon = "fas fa-chart-bar"
                            },
                        }
                    },
                },
                Icon = "fas fa-code",
            };
        }

        /// <summary>
        /// 获取增删改按钮
        /// </summary>
        private static List<SysMenu> GetCrudButtons(params SysMenu[] additionalButtons)
        {
            return new[]
            {
                new SysMenu { Label = "添加", Path = "add", Sort = 901, Type = SysMenuType.按钮 },
                new SysMenu { Label = "编辑", Path = "edit", Sort = 902, Type = SysMenuType.按钮 },
                new SysMenu { Label = "删除", Path = "remove", Sort = 903, Type = SysMenuType.按钮 }
            }.Concat(additionalButtons).ToList();
        }
    }
}
