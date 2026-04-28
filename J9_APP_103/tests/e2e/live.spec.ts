import { expect, test, type Page } from '@playwright/test';

const username = process.env.J9_E2E_LIVE_USERNAME;
const password = process.env.J9_E2E_LIVE_PASSWORD;

async function loginWithRealAccount(page: Page) {
  test.skip(!username || !password, '需要设置 J9_E2E_LIVE_USERNAME / J9_E2E_LIVE_PASSWORD');

  await page.goto('/login');
  await page.getByPlaceholder('请输入 11 位手机号').fill(username!);
  await page.getByPlaceholder('请输入登录密码').fill(password!);
  await page.getByText('登录账户').last().click();
  await expect(page).toHaveURL(/\/mine$/);
}

test.describe.serial('真实接口 Live Smoke', () => {
  test('公共页面可访问', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('body')).toContainText(/登录|注册|公告|游戏/);

    await page.goto('/about');
    await expect(page.locator('body')).toContainText('关于我们');

    await page.goto('/help-center');
    await expect(page.locator('body')).toContainText('帮助中心');

    await page.goto('/notice');
    await expect(page.locator('body')).toContainText('平台公告');
  });

  test('真实账号可以登录并加载会员核心页面', async ({ page }) => {
    await loginWithRealAccount(page);

    await expect(page.locator('body')).toContainText(username!);

    await page.goto('/earn');
    await expect(page.locator('body')).toContainText('每日签到');
    await expect(page.locator('body')).toContainText('每日任务');

    await page.goto('/tasks');
    await expect(page.locator('body')).toContainText('每日任务');

    await page.goto('/task-points-calendar');
    await expect(page.locator('body')).toContainText('任务积分日历');

    await page.goto('/activity');
    await expect(page.locator('body')).toContainText(/活动|暂无进行中的活动|活动加载失败/);
  });

  test('真实账号可以加载账户与记录相关页面', async ({ page }) => {
    await loginWithRealAccount(page);

    await page.goto('/transactions');
    await expect(page.locator('body')).toContainText('交易明细');

    await page.goto('/rebate');
    await expect(page.locator('body')).toContainText('返水中心');

    await page.goto('/invite-friends');
    await expect(page.locator('body')).toContainText('邀请好友');

    await page.goto('/messages');
    await expect(page.locator('body')).toContainText('消息通知');

    await page.goto('/chat');
    await expect(page.locator('body')).toContainText(/在线处理中|暂无会话记录|客服/);

    await page.goto('/bind-info');
    await expect(page.locator('body')).toContainText('系统设置');

    await page.goto('/change-password');
    await expect(page.locator('body')).toContainText('修改密码');

    await page.goto('/deposit');
    await expect(page.locator('body')).toContainText('存款中心');
  });
});
