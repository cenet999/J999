import { Linking, Platform } from 'react-native';
import { api, ApiResult } from './request';

/** 兼容后端 camelCase / PascalCase */
function pickTransactionId(data: unknown): number | null {
  if (data == null || typeof data !== 'object') return null;
  const o = data as Record<string, unknown>;
  const a = o.TransactionId;
  const b = o.transactionId;
  if (typeof a === 'number' && Number.isFinite(a)) return a;
  if (typeof b === 'number' && Number.isFinite(b)) return b;
  if (typeof a === 'string' && /^\d+$/.test(a)) return Number(a);
  if (typeof b === 'string' && /^\d+$/.test(b)) return Number(b);
  return null;
}

/** CreatePay0Order 的 data 一般为支付 URL 字符串 */
function pickPaymentUrl(data: unknown): string | null {
  if (data == null) return null;
  if (typeof data === 'string' && data.trim().length > 0) return data.trim();
  if (typeof data === 'object') {
    const o = data as Record<string, unknown>;
    for (const k of ['url', 'paymentUrl', 'payUrl', 'link', 'href']) {
      const v = o[k];
      if (typeof v === 'string' && v.trim().length > 0) return v.trim();
    }
  }
  return null;
}

async function openPayUrlInBrowser(url: string): Promise<boolean> {
  if (Platform.OS === 'web') {
    if (typeof window !== 'undefined') {
      try {
        // 新标签页有时会被浏览器拦截或链接过期，当前页也一起跳转做兜底。
        window.open(url, '_blank', 'noopener,noreferrer');
      } catch {
        // 忽略弹窗失败，继续用当前页跳转。
      }
      window.location.assign(url);
      return true;
    }
    return false;
  }
  try {
    const supported = await Linking.canOpenURL(url);
    if (supported) {
      await Linking.openURL(url);
      return true;
    }
    await Linking.openURL(url);
    return true;
  } catch {
    return false;
  }
}

/**
 * 玩家动态数据类型
 */
export interface PlayerActivity {
  memberName: string;
  memberAvatar: string;
  transactionType: string;
  transactionTypeValue: number;
  gameName: string;
  gameIcon: string;
  actualAmount: number;
  betAmount: number;
  transactionTime: string;
  description: string;
}

/**
 * 获取最近玩家动态（首页展示用）
 * @param count 返回条数，默认20
 * @param type 交易类型筛选（可选）：Bet=2, Recharge=4, Login=13, CheckIn=14, Register=15
 */
export async function getRecentPlayerActivity(count: number = 20, type?: number): Promise<ApiResult<PlayerActivity[]>> {
  let url = `/api/trans/@GetRecentPlayerActivity?count=${count}`;
  if (type !== undefined && type !== null) {
    url += `&type=${type}`;
  }
  return api.get(url);
}

/**
 * 交易记录数据类型
 */
export interface TransactionRecord {
  id: number;
  /** 关联游戏的接口代码（列表含 Include DGame 时返回，用于回收游戏余额） */
  apiCode?: string | null;
  transactionType: number;
  transactionTime: string;
  beforeAmount: number;
  afterAmount: number;
  betAmount: number;
  actualAmount: number;
  currencyCode: string;
  /** 业务单号，可能很长；列表 UI 应单行省略展示 */
  serialNumber: string;
  /** 游戏局号 / 局维度标识，可能很长；列表 UI 应单行省略展示 */
  gameRound: string;
  status: number;
  description: string;
  relatedTransActionId: number;
  isRebate: boolean;
  createdTime: string;
  modifiedTime: string;
  dMemberId: number;
}

/** 本月充值 / 提现汇总：income 为成功充值（Recharge）合计，expense 为成功提现（Withdraw）合计 */
export interface TransactionMonthSummary {
  income: number;
  expense: number;
}

export interface TransactionSyncResult {
  success?: boolean;
  message?: string | null;
  remoteFetched: number;
  inserted: number;
  updated: number;
  skippedNoSerial: number;
  pagesDone: number;
}

function normalizeTransactionSyncResult(data: unknown): TransactionSyncResult {
  if (data == null || typeof data !== 'object') {
    return {
      remoteFetched: 0,
      inserted: 0,
      updated: 0,
      skippedNoSerial: 0,
      pagesDone: 0,
    };
  }

  const o = data as Record<string, unknown>;
  const toNumber = (value: unknown) => {
    const num = Number(value ?? 0);
    return Number.isFinite(num) ? num : 0;
  };

  return {
    success:
      typeof (o.success ?? o.Success) === 'boolean'
        ? Boolean(o.success ?? o.Success)
        : undefined,
    message:
      typeof (o.message ?? o.Message) === 'string'
        ? String(o.message ?? o.Message)
        : null,
    remoteFetched: toNumber(o.remoteFetched ?? o.RemoteFetched),
    inserted: toNumber(o.inserted ?? o.Inserted),
    updated: toNumber(o.updated ?? o.Updated),
    skippedNoSerial: toNumber(o.skippedNoSerial ?? o.SkippedNoSerial),
    pagesDone: toNumber(o.pagesDone ?? o.PagesDone),
  };
}

function normalizeMonthSummary(data: unknown): TransactionMonthSummary {
  if (data == null || typeof data !== 'object') return { income: 0, expense: 0 };
  const o = data as Record<string, unknown>;
  const income = Number(o.income ?? o.Income ?? 0);
  const expense = Number(o.expense ?? o.Expense ?? 0);
  return {
    income: Number.isFinite(income) ? income : 0,
    expense: Number.isFinite(expense) ? expense : 0,
  };
}

/**
 * 本月已成功充值、已成功提现合计（与列表筛选无关，始终统计本月全部成功充值/提现）
 */
export async function getTransactionMonthSummary(): Promise<ApiResult<TransactionMonthSummary>> {
  const url = `/api/trans/@GetTransActionMonthSummary`;
  const res = await api.get<TransactionMonthSummary | Record<string, unknown>>(url);
  if (res.success && res.data != null) {
    return { ...res, data: normalizeMonthSummary(res.data) };
  }
  return res as ApiResult<TransactionMonthSummary>;
}

/**
 * 获取用户的交易记录列表
 * @param page 页码，默认1
 * @param pageSize 分页大小，默认500空载不设限
 * @param transactionType 交易类型过滤（可选）
 * @param transactionStatus 交易状态过滤（可选，与后端 TransactionStatus 枚举值一致）
 */
export async function getTransactionList(
  page: number = 1,
  pageSize: number = 100,
  transactionType?: number,
  transactionStatus?: number
): Promise<ApiResult<TransactionRecord[]>> {
  let url = `/api/trans/@GetTransActionList?page=${page}&pageSize=${pageSize}`;
  if (transactionType !== undefined && transactionType !== null) {
    url += `&transactionType=${transactionType}`;
  }
  if (transactionStatus !== undefined && transactionStatus !== null) {
    url += `&transactionStatus=${transactionStatus}`;
  }
  return api.get(url);
}

/**
 * 同步当前登录会员近 7 天的投注记录到本地交易表
 */
export async function syncBetHistoryToDatabase(): Promise<ApiResult<TransactionSyncResult>> {
  const res = await api.post<TransactionSyncResult | Record<string, unknown>>(
    `/api/trans/@SyncBetHistoryToDatabaseAsync`
  );
  if (res.success && res.data != null) {
    return { ...res, data: normalizeTransactionSyncResult(res.data) };
  }
  return res as ApiResult<TransactionSyncResult>;
}

/**
 * 会员自助充值：创建充值订单
 * @param amount 充值金额（人民币）
 * @returns 订单ID
 */
export async function createRechargeOrder(
  amount: number
): Promise<ApiResult<{ TransactionId?: number; transactionId?: number }>> {
  return api.post(`/api/trans/@CreateMemberRechargeOrder?amount=${amount}`);
}

/**
 * 获取 TokenPay 支付链接（对应后端 CreatePay0Order）
 * @param orderId 充值订单ID
 * @returns 支付链接（在 data 中，一般为字符串）
 */
export async function getPay0Url(orderId: number): Promise<ApiResult<string>> {
  return api.get(`/api/trans/@CreatePay0Order?orderId=${orderId}`);
}

/**
 * 创建充值订单后，立即请求 CreatePay0Order 拿到跳转链接并打开（Web 新标签页 / 原生外链）
 * @returns ok 为 false 时 message 为错误说明
 */
export async function createRechargeOrderAndOpenPayUrl(
  amount: number
): Promise<{ ok: boolean; message?: string }> {
  const orderRes = await createRechargeOrder(amount);
  if (!orderRes.success || !orderRes.data) {
    return { ok: false, message: orderRes.message || '创建订单失败' };
  }
  const transactionId = pickTransactionId(orderRes.data);
  if (transactionId == null) {
    return { ok: false, message: '创建订单失败：未返回订单号' };
  }

  const payRes = await getPay0Url(transactionId);
  if (!payRes.success) {
    return { ok: false, message: payRes.message || '获取支付链接失败' };
  }
  const payUrl = pickPaymentUrl(payRes.data);
  if (!payUrl) {
    return { ok: false, message: '获取支付链接失败：链接为空' };
  }

  const opened = await openPayUrlInBrowser(payUrl);
  if (!opened) {
    return { ok: false, message: '无法打开支付页面，请复制链接到浏览器' };
  }
  return { ok: true };
}

/**
 * 会员提现
 * @param Username 会员账号（手机号）
 * @param amount 提现金额
 * @param withdrawPassword 提现密码
 * @param description 提现方式描述
 */
export async function playerWithdraw(
  Username: string,
  amount: number,
  withdrawPassword: string,
  description: string = 'USDT提现'
): Promise<ApiResult<{ Message?: string; Balance?: number; TransactionId?: number }>> {
  return api.post(
    `/api/trans/@PlayerWithdraw?Username=${encodeURIComponent(Username)}&amount=${amount}&withdrawPassword=${encodeURIComponent(withdrawPassword)}&description=${encodeURIComponent(description)}`
  );
}

/**
 * 申请返水（将当前未返水的成功投注按代理返水比例结算入账）
 * @param Username 会员账号（手机号）
 */
export async function playerRebate(Username: string): Promise<ApiResult<unknown>> {
  return api.post(`/api/trans/@PlayerRebate?Username=${encodeURIComponent(Username)}`);
}
