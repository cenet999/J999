import AsyncStorage from '@react-native-async-storage/async-storage';
import { api, apiOk, type ApiResult } from '@/lib/api/request';
import { formatCny } from '@/lib/format-money';

const GAME_LIST_CACHE_PREFIX = '@j9_game_list_cache:';
const GAME_LIST_CACHE_TTL_MS = 2 * 24 * 60 * 60 * 1000;
const SHOULD_USE_GAME_LIST_CACHE = !(typeof __DEV__ !== 'undefined' && __DEV__);

interface GameListCachePayload {
  savedAt: number;
  result: ApiResult<BackendGame[]>;
}

export interface BackendGame {
  id?: number | string;
  Id?: number | string;
  gameCnName?: string;
  GameCnName?: string;
  gameName?: string;
  GameName?: string;
  icon?: string;
  Icon?: string;
  description?: string;
  Description?: string;
  gameUID?: string;
  GameUID?: string;
  apiCode?: string;
  ApiCode?: string;
  gameType?: number | string;
  GameType?: number | string;
  dGamePlatform?:
    | string
    | {
        name?: string;
        Name?: string;
      };
  DGamePlatform?:
    | string
    | {
        name?: string;
        Name?: string;
      };
}

function buildGameListCacheKey(queryString: string) {
  return `${GAME_LIST_CACHE_PREFIX}${queryString || '__default__'}`;
}

function isGameListCacheExpired(savedAt: number) {
  return !savedAt || Date.now() - savedAt > GAME_LIST_CACHE_TTL_MS;
}

async function getCachedGameList(
  cacheKey: string
): Promise<ApiResult<BackendGame[]> | null> {
  try {
    const raw = await AsyncStorage.getItem(cacheKey);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as Partial<GameListCachePayload>;
    const savedAt = typeof parsed.savedAt === 'number' ? parsed.savedAt : 0;

    if (isGameListCacheExpired(savedAt)) {
      await AsyncStorage.removeItem(cacheKey);
      return null;
    }

    return (parsed.result as ApiResult<BackendGame[]>) ?? null;
  } catch {
    return null;
  }
}

async function setCachedGameList(
  cacheKey: string,
  result: ApiResult<BackendGame[]>
) {
  try {
    await AsyncStorage.setItem(
      cacheKey,
      JSON.stringify({
        savedAt: Date.now(),
        result,
      } satisfies GameListCachePayload)
    );
  } catch {
    // ignore cache write failures
  }
}

export async function getGameList(
  keyword = '',
  type = 0,
  page = 1,
  limit = 30,
  sort = '',
  apiCode = ''
): Promise<ApiResult<BackendGame[]>> {
  const query = new URLSearchParams();

  if (keyword) query.set('keyword', keyword);
  if (type > 0) query.set('type', String(type));
  if (page > 1) query.set('page', String(page));
  if (limit > 0) query.set('limit', String(limit));
  if (sort) query.set('sort', sort);
  if (apiCode) query.set('apiCode', apiCode);

  const queryString = query.toString();
  const cacheKey = buildGameListCacheKey(queryString);
  if (SHOULD_USE_GAME_LIST_CACHE) {
    const cachedResult = await getCachedGameList(cacheKey);
    if (cachedResult) {
      return cachedResult;
    }
  }

  const result = await api.get<BackendGame[]>(
    `/api/game/@GetGameList${queryString ? `?${queryString}` : ''}`
  );

  if (SHOULD_USE_GAME_LIST_CACHE && result.success) {
    await setCachedGameList(cacheKey, result);
  }

  return result;
}

export async function startMsGame(
  playerId: string,
  gameId: string
): Promise<ApiResult<{ gameUrl?: string; apiCode?: string } | string>> {
  return api.get<{ gameUrl?: string; apiCode?: string } | string>(
    `/api/game/@StartMSGame?player_id=${encodeURIComponent(playerId)}&gameId=${encodeURIComponent(gameId)}`
  );
}

export async function startXhGame(
  playerId: string,
  gameId: string
): Promise<ApiResult<{ gameUrl?: string; apiCode?: string } | string>> {
  return api.get<{ gameUrl?: string; apiCode?: string } | string>(
    `/api/game/@StartXHGame?player_id=${encodeURIComponent(playerId)}&gameId=${encodeURIComponent(gameId)}`
  );
}

export async function recycleRecentMsGames(playerId: string): Promise<ApiResult<unknown>> {
  return api.post<unknown>(
    `/api/game/@RecycleRecentTransferInMSGames?player_id=${encodeURIComponent(playerId)}`
  );
}

export async function recycleRecentXhGames(playerId: string): Promise<ApiResult<unknown>> {
  return api.post<unknown>(
    `/api/game/@RecycleRecentTransferInXHGames?player_id=${encodeURIComponent(playerId)}`
  );
}

/** 从单条回收接口返回的 message 中解析转回主账户的金额（与 GameService 文案一致） */
export function parseRecycledCnyFromRecycleDetailMessage(message: string | undefined): number {
  if (!message) return 0;
  const text = message.trim();
  if (text.includes('余额为0')) return 0;
  const match = text.match(/转回余额\s*([\d.]+)\s*CNY/);
  if (!match) return 0;
  const value = Number.parseFloat(match[1]);
  return Number.isFinite(value) ? value : 0;
}

type RecycleBatchDataRow = {
  apiCode?: string;
  ApiCode?: string;
  message?: string;
  Message?: string;
};

function summarizeRecycleApiData(data: unknown): {
  lines: Array<{ apiCode: string; amountCny: number; message: string }>;
  totalCny: number;
} {
  const lines: Array<{ apiCode: string; amountCny: number; message: string }> = [];
  if (data === null || data === undefined || typeof data !== 'object') {
    return { lines, totalCny: 0 };
  }

  const root = data as Record<string, unknown>;
  const rawDetails = root.details ?? root.Details;
  if (!Array.isArray(rawDetails)) {
    return { lines, totalCny: 0 };
  }

  let totalCny = 0;
  for (const row of rawDetails) {
    if (row === null || row === undefined || typeof row !== 'object') continue;
    const d = row as RecycleBatchDataRow;
    const apiCode = String(d.apiCode ?? d.ApiCode ?? '').trim();
    const message = String(d.message ?? d.Message ?? '');
    const amountCny = parseRecycledCnyFromRecycleDetailMessage(message);
    lines.push({ apiCode, amountCny, message });
    totalCny += amountCny;
  }

  return { lines, totalCny };
}

export type RecycleRecentGamesResult = {
  ok: boolean;
  partial: boolean;
  message: string;
  /** 从 MS+XH 接口 data.details 汇总的本轮转回金额（CNY） */
  totalRecycledCny: number;
  details: Array<{
    platform: 'MS' | 'XH';
    ok: boolean;
    message: string;
    /** 该平台本轮解析出的转回金额合计（CNY） */
    recycledCny: number;
    /** 各 apiCode 一行 */
    lines: Array<{ apiCode: string; amountCny: number; message: string }>;
  }>;
};

/** 一行展示 MS / XH / 合计 转回金额，供 Toast 等使用 */
export function formatRecycleRecentGamesAmountLine(result: RecycleRecentGamesResult): string {
  const ms = result.details.find((d) => d.platform === 'MS')?.recycledCny ?? 0;
  const xh = result.details.find((d) => d.platform === 'XH')?.recycledCny ?? 0;
  if (result.totalRecycledCny > 0) {
    return `MS ${formatCny(ms)}，XH ${formatCny(xh)}，合计 ${formatCny(result.totalRecycledCny)}`;
  }
  return `MS ${formatCny(0)}，XH ${formatCny(0)}，合计 ${formatCny(0)}`;
}

export async function recycleRecentGames(
  playerId: string
): Promise<RecycleRecentGamesResult> {
  const settled = await Promise.allSettled([
    recycleRecentMsGames(playerId),
    recycleRecentXhGames(playerId),
  ]);

  const details: RecycleRecentGamesResult['details'] = settled.map((item, index) => {
    const platform = index === 0 ? 'MS' : 'XH';

    if (item.status === 'fulfilled') {
      const { lines, totalCny } = summarizeRecycleApiData(item.value.data);
      return {
        platform,
        ok: apiOk(item.value),
        message: item.value.message || `${platform} 回收请求已完成`,
        recycledCny: totalCny,
        lines,
      };
    }

    const fallbackMessage =
      item.reason instanceof Error ? item.reason.message : `${platform} 回收请求失败`;
    return {
      platform,
      ok: false,
      message: fallbackMessage,
      recycledCny: 0,
      lines: [],
    };
  });

  const successCount = details.filter((item) => item.ok).length;
  const ok = successCount > 0;
  const partial = ok && successCount < details.length;
  const message = details
    .map((item) => `${item.platform}：${item.message}`)
    .join('；');
  const totalRecycledCny = details.reduce((sum, item) => sum + item.recycledCny, 0);

  return {
    ok,
    partial,
    message,
    totalRecycledCny,
    details,
  };
}
