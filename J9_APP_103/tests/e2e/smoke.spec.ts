import { expect, test } from '@playwright/test';

import { mockAppApis } from './fixtures';

test('首页能加载并打开登录弹窗', async ({ page }) => {
  await mockAppApis(page);

  await page.goto('/');

  await expect(page.getByText('首条测试公告')).toBeVisible();
  await expect(page.getByText('麻将胡了')).toBeVisible();
  await expect(page.getByText('财神捕鱼')).toBeVisible();

  await page.getByText('登录', { exact: true }).first().click();

  await expect(page.getByText('会员登录').last()).toBeVisible();
  await expect(page.getByPlaceholder('请输入 11 位手机号')).toBeVisible();
  await expect(page.getByPlaceholder('请输入登录密码')).toBeVisible();
});

test('登录页和注册页都能正常打开', async ({ page }) => {
  await mockAppApis(page);

  await page.goto('/login');
  await expect(page.getByText('打开登录窗口')).toBeVisible();
  await expect(page.getByText('会员登录').first()).toBeVisible();
  await expect(page.getByText('登录账户').last()).toBeVisible();

  await page.goto('/register');
  await expect(page.getByText('打开注册窗口')).toBeVisible();
  await expect(page.getByText('开户注册').first()).toBeVisible();
  await expect(page.getByText('提交注册').last()).toBeVisible();
});

test('登录失败时显示错误提示', async ({ page }) => {
  await mockAppApis(page, {
    loginResponse: {
      code: 400,
      success: false,
      message: '账号或密码错误',
      data: null,
    },
  });

  await page.goto('/login');

  await page.getByPlaceholder('请输入 11 位手机号').fill('13800138000');
  await page.getByPlaceholder('请输入登录密码').fill('wrong-password');
  await page.getByText('登录账户').last().click();

  await expect(page.getByText('账号或密码错误')).toBeVisible();
});

test('登录成功后会进入 mine 并显示会员信息', async ({ page }) => {
  await mockAppApis(page, {
    loginResponse: {
      code: 200,
      success: true,
      data: 'token-login-success',
    },
    memberCheckResponse: {
      code: 200,
      success: true,
      data: {
        Id: '1001',
        Username: '13800138000',
        Nickname: '测试会员',
        CreditAmount: 256.5,
        ActivityPoint: 88,
        VipLevel: 3,
      },
    },
  });

  await page.goto('/login');

  await page.getByPlaceholder('请输入 11 位手机号').fill('13800138000');
  await page.getByPlaceholder('请输入登录密码').fill('correct-password');
  await page.getByText('登录账户').last().click();

  await expect(page).toHaveURL(/\/mine$/);
  await expect(page.getByText('测试会员')).toBeVisible();
  await expect(page.getByText('UID: 1001')).toBeVisible();
  await expect(page.getByText('VIP 3')).toBeVisible();
});

test('注册页会拦截密码不一致和手机号格式错误', async ({ page }) => {
  await mockAppApis(page);

  await page.goto('/register');

  const phoneInput = page.getByPlaceholder('请输入手机号');
  const passwordInput = page.getByPlaceholder('设置登录密码');
  const confirmPasswordInput = page.getByPlaceholder('再输一次密码');
  const submitButton = page.getByText('提交注册').last();

  await phoneInput.fill('12345');
  await passwordInput.fill('abcd');
  await confirmPasswordInput.fill('abcd1234');
  await submitButton.click();

  await expect(page.getByText('密码不一致')).toBeVisible();
  await expect(page.getByText('两次输入的密码需保持一致。')).toBeVisible();

  await confirmPasswordInput.fill('abcd');
  await submitButton.click();

  await expect(page.getByText('手机号格式错误')).toBeVisible();
  await expect(page.getByText('请输入正确的 11 位手机号。')).toBeVisible();
});

test('注册成功后会自动登录并进入 mine', async ({ page }) => {
  await mockAppApis(page, {
    registerResponse: {
      code: 200,
      success: true,
      data: true,
    },
    loginResponse: {
      code: 200,
      success: true,
      data: 'token-register-success',
    },
    memberCheckResponse: {
      code: 200,
      success: true,
      data: {
        Id: '2002',
        Username: '13900139000',
        Nickname: '新会员',
        CreditAmount: 0,
        ActivityPoint: 0,
        VipLevel: 1,
      },
    },
  });

  await page.goto('/register');

  await page.getByPlaceholder('请输入手机号').fill('13900139000');
  await page.getByPlaceholder('设置登录密码').fill('abcd1234');
  await page.getByPlaceholder('再输一次密码').fill('abcd1234');
  await page.getByText('提交注册').last().click();

  await expect(page).toHaveURL(/\/mine$/);
  await expect(page.getByText('新会员')).toBeVisible();
  await expect(page.getByText('UID: 2002')).toBeVisible();
});

test('未登录访问 mine 会自动跳转到 login', async ({ page }) => {
  await mockAppApis(page);

  await page.goto('/mine');

  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByText('打开登录窗口')).toBeVisible();
});

test('游戏启动页缺少参数时显示错误提示', async ({ page }) => {
  await mockAppApis(page);

  await page.goto('/game-launch');

  await expect(page.getByText('缺少游戏参数')).toBeVisible();
  await expect(page.getByText('请返回后重试')).toBeVisible();
});

test('游戏启动成功时会进入打开外部地址阶段', async ({ page }) => {
  await mockAppApis(page, {
    memberCheckResponse: {
      code: 200,
      success: true,
      data: {
        Id: '3003',
        Username: '13700137000',
      },
    },
    startMsGameResponse: {
      code: 200,
      success: true,
      data: {
        gameUrl: 'https://example.com/game',
      },
    },
  });

  await page.goto(
    '/game-launch?gameId=game-1&title=%E9%BA%BB%E5%B0%86%E8%83%A1%E4%BA%86&dGamePlatform=PG'
  );

  await expect(page.getByText('正在进入 麻将胡了')).toBeVisible();
  await expect(page.getByText('正在打开外部游戏地址')).toBeVisible();
  await expect(page.getByText('游戏地址已获取，正在为您打开外部页面。')).toBeVisible();
});

test('首页公告弹窗可以打开和关闭', async ({ page }) => {
  await mockAppApis(page);

  await page.goto('/');

  await page.getByText('首条测试公告').click();

  await expect(page.getByText('平台公告')).toBeVisible();
  await expect(page.getByText('Playwright 冒烟测试公告')).toBeVisible();

  await page.getByText('×').click();

  await expect(page.getByText('平台公告')).not.toBeVisible();
});
