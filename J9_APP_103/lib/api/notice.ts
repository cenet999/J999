import AsyncStorage from '@react-native-async-storage/async-storage';
import {
  api,
  FRONTEND_CACHE_ENABLED,
  type ApiResult,
} from '@/lib/api/request';

const NOTICE_CACHE_KEY = '@j9_notice_cache';
const NOTICE_CACHE_TTL_MS = 2 * 24 * 60 * 60 * 1000;

interface NoticeCachePayload {
  savedAt: number;
  result: ApiResult<Notice[]>;
}

export interface Notice {
  id?: number | string;
  Id?: number | string;
  title?: string;
  Title?: string;
  content?: string;
  Content?: string;
  createdTime?: string;
  CreatedTime?: string;
}

function isNoticeCacheExpired(savedAt: number) {
  return !savedAt || Date.now() - savedAt > NOTICE_CACHE_TTL_MS;
}

async function getCachedNotices(): Promise<ApiResult<Notice[]> | null> {
  if (!FRONTEND_CACHE_ENABLED) {
    try {
      await AsyncStorage.removeItem(NOTICE_CACHE_KEY);
    } catch {
      // ignore
    }
    return null;
  }

  try {
    const raw = await AsyncStorage.getItem(NOTICE_CACHE_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as Partial<NoticeCachePayload>;
    const savedAt = typeof parsed.savedAt === 'number' ? parsed.savedAt : 0;

    if (isNoticeCacheExpired(savedAt)) {
      await AsyncStorage.removeItem(NOTICE_CACHE_KEY);
      return null;
    }

    return (parsed.result as ApiResult<Notice[]>) ?? null;
  } catch {
    return null;
  }
}

async function setCachedNotices(result: ApiResult<Notice[]>) {
  if (!FRONTEND_CACHE_ENABLED) {
    try {
      await AsyncStorage.removeItem(NOTICE_CACHE_KEY);
    } catch {
      // ignore
    }
    return;
  }

  try {
    await AsyncStorage.setItem(
      NOTICE_CACHE_KEY,
      JSON.stringify({
        savedAt: Date.now(),
        result,
      } satisfies NoticeCachePayload)
    );
  } catch {
    // ignore cache write failures
  }
}

export async function getNotices(): Promise<ApiResult<Notice[]>> {
  const cachedResult = await getCachedNotices();
  if (cachedResult) {
    return cachedResult;
  }

  const result = await api.get<Notice[]>('/api/notice/@GetNotices');
  if (result.success) {
    await setCachedNotices(result);
  }

  return result;
}
