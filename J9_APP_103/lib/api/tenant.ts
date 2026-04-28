import AsyncStorage from '@react-native-async-storage/async-storage';
import * as React from 'react';

import { api, FRONTEND_CACHE_ENABLED } from './request';

export type TenantInfo = {
  id: string;
  title: string;
  host?: string | null;
  logo?: string | null;
};

/** 当接口不可用或首次加载时展示的兜底名称 */
export const DEFAULT_TENANT_TITLE = '俱乐部';

const CACHE_KEY = '@tenant_info_v1';
const CACHE_TTL_MS = 7 * 24 * 60 * 60 * 1000; // 7 天

type CacheShape = {
  data: TenantInfo;
  expireAt: number;
};

let memoryCache: TenantInfo | null = null;
let inflight: Promise<TenantInfo | null> | null = null;
const listeners = new Set<(info: TenantInfo) => void>();

async function readDiskCache(): Promise<TenantInfo | null> {
  if (!FRONTEND_CACHE_ENABLED) {
    try {
      await AsyncStorage.removeItem(CACHE_KEY);
    } catch {
      // ignore
    }
    return null;
  }

  try {
    const raw = await AsyncStorage.getItem(CACHE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as CacheShape;
    if (!parsed?.data?.title || typeof parsed.expireAt !== 'number') return null;
    if (Date.now() > parsed.expireAt) return null;
    return parsed.data;
  } catch {
    return null;
  }
}

async function writeDiskCache(data: TenantInfo): Promise<void> {
  if (!FRONTEND_CACHE_ENABLED) {
    try {
      await AsyncStorage.removeItem(CACHE_KEY);
    } catch {
      // ignore
    }
    return;
  }

  try {
    const payload: CacheShape = { data, expireAt: Date.now() + CACHE_TTL_MS };
    await AsyncStorage.setItem(CACHE_KEY, JSON.stringify(payload));
  } catch {
    // 忽略存储异常（例如 Web 隐身模式 QuotaExceeded）
  }
}

async function fetchTenantFromApi(): Promise<TenantInfo | null> {
  const result = await api.get<TenantInfo[]>('/api/login/@GetTenantInfo');
  if (!result.success) return null;

  const list = Array.isArray(result.data) ? result.data : [];
  const first = list[0];
  if (!first || !first.title) return null;

  return {
    id: String(first.id ?? ''),
    title: first.title,
    host: first.host ?? null,
    logo: first.logo ?? null,
  };
}

function notify(info: TenantInfo) {
  listeners.forEach((listener) => {
    try {
      listener(info);
    } catch {
      // 单个订阅者出错不影响其他订阅者
    }
  });
}

/**
 * 获取租户信息：命中本地缓存（7 天）则直接返回；否则从后端拉取并写入缓存。
 */
export async function getTenantInfo(options?: {
  forceRefresh?: boolean;
}): Promise<TenantInfo | null> {
  if (!options?.forceRefresh && FRONTEND_CACHE_ENABLED && memoryCache) {
    return memoryCache;
  }

  if (!options?.forceRefresh && FRONTEND_CACHE_ENABLED) {
    const cached = await readDiskCache();
    if (cached) {
      memoryCache = cached;
      return cached;
    }
  }

  if (inflight) return inflight;

  inflight = (async () => {
    try {
      const fresh = await fetchTenantFromApi();
      if (fresh) {
        memoryCache = fresh;
        await writeDiskCache(fresh);
        notify(fresh);
      }
      return fresh;
    } finally {
      inflight = null;
    }
  })();

  return inflight;
}

/**
 * 只需要站点名称时的便捷方法，自带兜底值。
 */
export async function getTenantTitle(): Promise<string> {
  const info = await getTenantInfo();
  return info?.title || DEFAULT_TENANT_TITLE;
}

/**
 * 强制绕过缓存重新拉取，同时更新缓存和所有订阅组件。
 */
export async function refreshTenantInfo(): Promise<TenantInfo | null> {
  return getTenantInfo({ forceRefresh: true });
}

/**
 * 清空内存与本地缓存，主要用于调试或站点切换。
 */
export async function clearTenantCache(): Promise<void> {
  memoryCache = null;
  try {
    await AsyncStorage.removeItem(CACHE_KEY);
  } catch {
    // ignore
  }
}

/**
 * 组件内订阅租户标题；首次渲染使用内存缓存/兜底值，异步拿到真实值后自动刷新。
 */
export function useTenantTitle(): string {
  const [title, setTitle] = React.useState<string>(
    memoryCache?.title ?? DEFAULT_TENANT_TITLE
  );

  React.useEffect(() => {
    let cancelled = false;

    const listener = (info: TenantInfo) => {
      if (!cancelled && info.title) setTitle(info.title);
    };
    listeners.add(listener);

    (async () => {
      const info = await getTenantInfo();
      if (!cancelled && info?.title) setTitle(info.title);
    })();

    return () => {
      cancelled = true;
      listeners.delete(listener);
    };
  }, []);

  return title;
}
