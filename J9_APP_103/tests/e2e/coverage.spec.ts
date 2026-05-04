import { expect, test, type Page } from '@playwright/test';

import { mockAppApis } from './fixtures';

const loginSuccessResponse = {
  code: 200,
  success: true,
  data: 'token-full-coverage',
};

const authenticatedMemberResponse = {
  code: 200,
  success: true,
  data: {
    Id: '1001',
    Username: '13800138000',
    Nickname: '测试会员',
    CreditAmount: 256.5,
    ActivityPoint: 88,
    VipLevel: 3,
    RebateAmount: 12.34,
    PhoneNumber: '13800138000',
    Telegram: 'j9_e2e',
    USDTAddress: 'TRC20-E2E-ADDRESS',
  },
};

async function loginAsMember(page: Page, extraMocks: Record<string, unknown> = {}) {
  await mockAppApis(page, {
    loginResponse: loginSuccessResponse,
    memberCheckResponse: authenticatedMemberResponse,
    ...extraMocks,
  });

  await page.goto('/login');
  await page.getByPlaceholder('请输入 11 位手机号').fill('13800138000');
  await page.getByPlaceholder('请输入登录密码').fill('correct-password');
  await page.getByText('登录账户').last().click();
  await expect(page).toHaveURL(/\/mine$/);
}

test('公共页面可以正常打开并展示核心内容', async ({ page }) => {
  await mockAppApis(page);

  await page.goto('/about');
  await expect(page.getByText('关于我们').first()).toBeVisible();
  await expect(page.getByText('J9 Club', { exact: true })).toBeVisible();
  await expect(page.getByText('安全保障')).toBeVisible();

  await page.goto('/help-center');
  await expect(page.getByText('帮助中心').first()).toBeVisible();
  await expect(page.getByText('常见问题', { exact: true })).toBeVisible();
  await expect(page.getByText('联系我们')).toBeVisible();

  await page.goto('/notice');
  await expect(page.getByText('平台公告').first()).toBeVisible();
  await expect(page.getByText('首条测试公告')).toBeVisible();
});

test('登录后福利相关页面可以完整加载', async ({ page }) => {
  await loginAsMember(page);

  await page.goto('/earn');
  await expect(page.getByText('每日签到')).toBeVisible();
  await expect(page.getByText('积分宝箱')).toBeVisible();
  await expect(page.getByText('每日任务')).toBeVisible();

  await page.goto('/tasks');
  await expect(page.getByText('每日任务').first()).toBeVisible();
  await expect(page.getByText('登录奖励')).toBeVisible();
  await expect(page.getByText('充值一次')).toBeVisible();

  await page.goto('/task-points-calendar');
  await expect(page.getByText('任务积分日历')).toBeVisible();
  await expect(page.getByText('本月累计任务积分')).toBeVisible();
  await expect(page.getByText('有积分天数')).toBeVisible();

  await page.goto('/activity');
  await expect(page.getByText('连胜挑战赛')).toBeVisible();
  await expect(page.getByText('活动进行中')).toBeVisible();
});

test('登录后资金页面可以加载并完成基础交互', async ({ page }) => {
  await loginAsMember(page, {
    playerRebateResponse: {
      code: 200,
      success: true,
      data: true,
    },
  });

  await page.goto('/deposit');
  await expect(page.getByText('存款中心').first()).toBeVisible();
  await page.getByText('自定义金额').click();
  await page.getByPlaceholder('请输入充值金额').fill('1');
  await page.getByText('确认充值 ¥1').click();
  await expect(page.getByText('金额过低')).toBeVisible();

  await page.goto('/transactions');
  await expect(page.getByText('交易明细').first()).toBeVisible();
  await expect(page.getByText('筛选与概览')).toBeVisible();
  await expect(page.getByText('交易记录（2笔）')).toBeVisible();
  await expect(page.getByText('充值到账')).toBeVisible();

  await page.goto('/rebate');
  await expect(page.getByText('返水中心').first()).toBeVisible();
  await expect(page.getByText('当前可领返水')).toBeVisible();
  await expect(page.getByText('最近返水记录')).toBeVisible();
  await page.getByText('提交返水申请').click();
  await expect(page.getByText('返水已结算到账')).toBeVisible();
});

test('登录后消息与客服流程可以正常工作', async ({ page }) => {
  await loginAsMember(page, {
    markAllAsReadResponse: {
      code: 200,
      success: true,
      data: true,
    },
    sendMessageResponse: {
      code: 200,
      success: true,
      data: true,
    },
  });

  await page.goto('/messages');
  await expect(page.getByText('消息通知').first()).toBeVisible();
  await expect(page.getByText('全部已读')).toBeVisible();
  await expect(page.getByText('系统维护通知：今晚 23:30 将进行短时维护。')).toBeVisible();
  await page.getByText('全部已读').click();
  await expect(page.getByText('全部已读')).not.toBeVisible();
  await page.getByText('联系客服').click();

  await expect(page).toHaveURL(/\/chat$/);
  await expect(page.getByText('在线处理中')).toBeVisible();
  await expect(page.getByPlaceholder('请输入您的问题或需求...')).toBeVisible();
  await page.getByPlaceholder('请输入您的问题或需求...').fill('我想咨询充值问题');
  await page.getByPlaceholder('请输入您的问题或需求...').press('Enter');
  await expect(page.getByPlaceholder('请输入您的问题或需求...')).toHaveValue('');
});

test('登录后邀请与设置页面可以正常打开', async ({ page }) => {
  await loginAsMember(page, {
    updateMemberInfoResponse: {
      code: 200,
      success: true,
      data: true,
    },
  });

  await page.goto('/invite-friends');
  await expect(page.getByText('邀请好友').first()).toBeVisible();
  await expect(page.getByText('我的邀请码')).toBeVisible();
  await expect(page.getByText('J9E2E')).toBeVisible();
  await expect(page.getByText('邀请记录', { exact: true })).toBeVisible();

  await page.goto('/bind-info');
  await expect(page.getByText('系统设置').first()).toBeVisible();
  await expect(page.getByText('资料设置')).toBeVisible();
  await page.getByPlaceholder('请输入 Telegram 账号').fill('updated_e2e_tg');
  await page.getByText('更新信息').click();
  await expect(page.getByText('资料已经更新好了')).toBeVisible();

  await page.goto('/change-password');
  await expect(page.getByText('修改密码').first()).toBeVisible();
  await expect(page.getByText('修改登录密码', { exact: true })).toBeVisible();
  await expect(page.getByText('修改提现密码', { exact: true })).toBeVisible();
});
