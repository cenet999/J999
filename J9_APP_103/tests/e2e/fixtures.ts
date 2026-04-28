import type { Page, Route } from '@playwright/test';

const tenantResponse = {
  code: 200,
  success: true,
  data: [
    {
      id: 'tenant-1',
      title: '九游俱乐部',
      host: 'j9.local',
      logo: null,
    },
  ],
};

const noticeResponse = {
  code: 200,
  success: true,
  data: [
    {
      id: 'notice-1',
      title: '首条测试公告',
      content: '<p>Playwright 冒烟测试公告</p>',
      createdTime: '2026-04-23 12:00:00',
    },
  ],
};

const gameResponse = {
  code: 200,
  success: true,
  data: [
    {
      id: 'game-1',
      gameCnName: '麻将胡了',
      apiCode: 'PG',
      gameType: 3,
      dGamePlatform: 'PG',
    },
    {
      id: 'game-2',
      gameCnName: '德州扑克',
      apiCode: 'KY',
      gameType: 6,
      dGamePlatform: 'KY',
    },
    {
      id: 'game-3',
      gameCnName: '财神捕鱼',
      apiCode: 'JDB',
      gameType: 2,
      dGamePlatform: 'JDB',
    },
  ],
};

const messageResponse = {
  code: 200,
  success: true,
  data: [
    {
      id: 1,
      dMemberId: 1001,
      senderRole: 'System',
      content: '系统维护通知：今晚 23:30 将进行短时维护。',
      sentAt: '2026-04-23 09:30:00',
      status: '未读',
      senderIp: '127.0.0.1',
    },
    {
      id: 2,
      dMemberId: 1001,
      senderRole: 'Agent',
      content: '您好，您提交的问题已经收到，客服正在处理中。',
      sentAt: '2026-04-23 10:00:00',
      status: '未读',
      senderIp: '127.0.0.1',
    },
  ],
};

const inviteCenterResponse = {
  code: 200,
  success: true,
  data: {
    agentId: 8,
    agentName: '渠道A',
    inviteCode: 'J9E2E',
    totalInvites: 12,
    todayInvites: 2,
    totalInviteTaskReward: 188,
    myRank: 3,
    myInviteCount: 12,
    records: [
      {
        displayName: '138****8000',
        registeredAt: '2026-04-23 09:20:00',
      },
    ],
    leaderboard: [
      {
        rank: 1,
        displayName: '榜一用户',
        inviteCount: 20,
        isCurrentUser: false,
      },
      {
        rank: 3,
        displayName: '我的账号',
        inviteCount: 12,
        isCurrentUser: true,
      },
    ],
  },
};

const checkInStatusResponse = {
  code: 200,
  success: true,
  data: {
    activityPoint: 18,
    continuousDays: 3,
    isTodayChecked: false,
    today: '2026-04-23',
    todayWeek: '周四',
    checkInDays: [
      { day: '一', date: '04-21', reward: '2积分', checked: true, isToday: false },
      { day: '二', date: '04-22', reward: '4积分', checked: true, isToday: false },
      { day: '三', date: '04-23', reward: '6积分', checked: false, isToday: true },
    ],
  },
};

const dailyTasksResponse = {
  code: 200,
  success: true,
  data: {
    tasks: [
      {
        id: 'task-login',
        title: '登录奖励',
        description: '每日登录一次即可领取',
        icon: '',
        jumpPath: '/mine',
        rewardAmount: 2,
        activityPoint: 5,
        currentValue: 1,
        targetValue: 1,
        status: 1,
      },
      {
        id: 'task-recharge',
        title: '充值一次',
        description: '完成一次充值',
        icon: '',
        jumpPath: '/deposit',
        rewardAmount: 8,
        activityPoint: 10,
        currentValue: 0,
        targetValue: 1,
        status: 0,
      },
    ],
    totalActivityPoint: 28,
    claimedChests: [20],
  },
};

const timeLimitedEventsResponse = {
  code: 200,
  success: true,
  data: [
    {
      name: '连胜挑战赛',
      desc: '完成指定连胜目标可领取额外奖励。',
      image: '',
      progress: 68,
      total: 100,
      timeLeft: '2天',
    },
  ],
};

const monthlyTaskActivityResponse = {
  code: 200,
  success: true,
  data: {
    year: 2026,
    month: 4,
    days: [
      { date: '2026-04-21', taskActivityPoint: 6 },
      { date: '2026-04-22', taskActivityPoint: 8 },
      { date: '2026-04-23', taskActivityPoint: 14 },
    ],
    monthTotal: 28,
    activeDays: 3,
  },
};

const transactionListResponse = {
  code: 200,
  success: true,
  data: [
    {
      id: 101,
      transactionType: 4,
      transactionTime: '2026-04-23 10:00:00',
      beforeAmount: 100,
      afterAmount: 600,
      betAmount: 0,
      actualAmount: 500,
      currencyCode: 'CNY',
      serialNumber: 'TX101',
      gameRound: '',
      status: 0,
      description: '充值到账',
      relatedTransActionId: 0,
      isRebate: false,
      createdTime: '2026-04-23 10:00:00',
      modifiedTime: '2026-04-23 10:00:00',
      dMemberId: 1001,
      apiCode: 'PG',
    },
    {
      id: 102,
      transactionType: 11,
      transactionTime: '2026-04-22 12:00:00',
      beforeAmount: 600,
      afterAmount: 612.34,
      betAmount: 1234,
      actualAmount: 12.34,
      currencyCode: 'CNY',
      serialNumber: 'TX102',
      gameRound: '',
      status: 0,
      description: '周返水',
      relatedTransActionId: 0,
      isRebate: true,
      createdTime: '2026-04-22 12:00:00',
      modifiedTime: '2026-04-22 12:00:00',
      dMemberId: 1001,
      apiCode: 'PG',
    },
  ],
};

const transactionMonthSummaryResponse = {
  code: 200,
  success: true,
  data: {
    income: 888,
    expense: 256,
  },
};

async function fulfillJson(route: Route, payload: unknown) {
  await route.fulfill({
    status: 200,
    contentType: 'application/json; charset=utf-8',
    body: JSON.stringify(payload),
  });
}

type MockAppApiOptions = {
  loginResponse?: unknown;
  registerResponse?: unknown;
  memberCheckResponse?: unknown;
  tenantInfoResponse?: unknown;
  noticeListResponse?: unknown;
  gameListResponse?: unknown;
  startMsGameResponse?: unknown;
  startXhGameResponse?: unknown;
  messagesResponse?: unknown;
  markAllAsReadResponse?: unknown;
  markAsReadResponse?: unknown;
  deleteMessageResponse?: unknown;
  sendMessageResponse?: unknown;
  inviteCenterResponse?: unknown;
  checkInStatusResponse?: unknown;
  dailyTasksResponse?: unknown;
  claimDailyTaskResponse?: unknown;
  claimActivityChestResponse?: unknown;
  timeLimitedEventsResponse?: unknown;
  monthlyTaskActivityResponse?: unknown;
  transactionListResponse?: unknown;
  transactionMonthSummaryResponse?: unknown;
  playerRebateResponse?: unknown;
  createMemberRechargeOrderResponse?: unknown;
  createPay0OrderResponse?: unknown;
  changePasswordResponse?: unknown;
  changeWithdrawPasswordResponse?: unknown;
  updateMemberInfoResponse?: unknown;
};

const defaultMemberCheckResponse = {
  code: 401,
  success: false,
  message: '未登录',
  data: null,
};

export async function mockAppApis(page: Page, options: MockAppApiOptions = {}) {
  await page.addInitScript(() => {
    (
      window as Window & { __J9_E2E_SKIP_EXTERNAL_GAME_NAV__?: boolean }
    ).__J9_E2E_SKIP_EXTERNAL_GAME_NAV__ = true;
  });

  await page.route('**/api/login/@GetTenantInfo', (route) =>
    fulfillJson(route, options.tenantInfoResponse ?? tenantResponse)
  );
  await page.route('**/api/notice/@GetNotices', (route) =>
    fulfillJson(route, options.noticeListResponse ?? noticeResponse)
  );
  await page.route('**/api/game/@GetGameList**', (route) =>
    fulfillJson(route, options.gameListResponse ?? gameResponse)
  );
  await page.route('**/api/login/@Check', (route) =>
    fulfillJson(route, options.memberCheckResponse ?? defaultMemberCheckResponse)
  );
  await page.route('**/api/message/@GetMessages', (route) =>
    fulfillJson(route, options.messagesResponse ?? messageResponse)
  );
  await page.route('**/api/login/@GetInviteCenter', (route) =>
    fulfillJson(route, options.inviteCenterResponse ?? inviteCenterResponse)
  );
  await page.route('**/api/event/@GetCheckInStatus', (route) =>
    fulfillJson(route, options.checkInStatusResponse ?? checkInStatusResponse)
  );
  await page.route('**/api/event/@GetDailyTasks', (route) =>
    fulfillJson(route, options.dailyTasksResponse ?? dailyTasksResponse)
  );
  await page.route('**/api/event/@GetTimeLimitedEvents', (route) =>
    fulfillJson(route, options.timeLimitedEventsResponse ?? timeLimitedEventsResponse)
  );
  await page.route('**/api/event/@GetMonthlyTaskActivity**', (route) =>
    fulfillJson(route, options.monthlyTaskActivityResponse ?? monthlyTaskActivityResponse)
  );
  await page.route('**/api/trans/@GetTransActionList**', (route) =>
    fulfillJson(route, options.transactionListResponse ?? transactionListResponse)
  );
  await page.route('**/api/trans/@GetTransActionMonthSummary', (route) =>
    fulfillJson(route, options.transactionMonthSummaryResponse ?? transactionMonthSummaryResponse)
  );

  if (options.loginResponse !== undefined) {
    await page.route('**/api/login/@Login**', (route) => fulfillJson(route, options.loginResponse));
  }

  if (options.registerResponse !== undefined) {
    await page.route('**/api/login/@Register', (route) =>
      fulfillJson(route, options.registerResponse)
    );
  }

  if (options.startMsGameResponse !== undefined) {
    await page.route('**/api/game/@StartMSGame**', (route) =>
      fulfillJson(route, options.startMsGameResponse)
    );
  }

  if (options.startXhGameResponse !== undefined) {
    await page.route('**/api/game/@StartXHGame**', (route) =>
      fulfillJson(route, options.startXhGameResponse)
    );
  }

  if (options.markAllAsReadResponse !== undefined) {
    await page.route('**/api/message/@MarkAllAsRead', (route) =>
      fulfillJson(route, options.markAllAsReadResponse)
    );
  }

  if (options.markAsReadResponse !== undefined) {
    await page.route('**/api/message/@MarkAsRead**', (route) =>
      fulfillJson(route, options.markAsReadResponse)
    );
  }

  if (options.deleteMessageResponse !== undefined) {
    await page.route('**/api/message/@DeleteMessage**', (route) =>
      fulfillJson(route, options.deleteMessageResponse)
    );
  }

  if (options.sendMessageResponse !== undefined) {
    await page.route('**/api/message/@SendMessage**', (route) =>
      fulfillJson(route, options.sendMessageResponse)
    );
  }

  if (options.claimDailyTaskResponse !== undefined) {
    await page.route('**/api/event/@ClaimDailyTask', (route) =>
      fulfillJson(route, options.claimDailyTaskResponse)
    );
  }

  if (options.claimActivityChestResponse !== undefined) {
    await page.route('**/api/event/@ClaimActivityChest', (route) =>
      fulfillJson(route, options.claimActivityChestResponse)
    );
  }

  if (options.playerRebateResponse !== undefined) {
    await page.route('**/api/trans/@PlayerRebate**', (route) =>
      fulfillJson(route, options.playerRebateResponse)
    );
  }

  if (options.createMemberRechargeOrderResponse !== undefined) {
    await page.route('**/api/trans/@CreateMemberRechargeOrder**', (route) =>
      fulfillJson(route, options.createMemberRechargeOrderResponse)
    );
  }

  if (options.createPay0OrderResponse !== undefined) {
    await page.route('**/api/trans/@CreatePay0Order**', (route) =>
      fulfillJson(route, options.createPay0OrderResponse)
    );
  }

  if (options.changePasswordResponse !== undefined) {
    await page.route('**/api/login/@ChangePassword**', (route) =>
      fulfillJson(route, options.changePasswordResponse)
    );
  }

  if (options.changeWithdrawPasswordResponse !== undefined) {
    await page.route('**/api/login/@ChangeWithdrawPassword**', (route) =>
      fulfillJson(route, options.changeWithdrawPasswordResponse)
    );
  }

  if (options.updateMemberInfoResponse !== undefined) {
    await page.route('**/api/login/@UpdateMemberInfo**', (route) =>
      fulfillJson(route, options.updateMemberInfoResponse)
    );
  }
}
