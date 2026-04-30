import AsyncStorage from '@react-native-async-storage/async-storage';
import { Platform } from 'react-native';

const DEV_API =
  typeof process !== 'undefined' && process.env?.EXPO_PUBLIC_API_URL
    ? process.env.EXPO_PUBLIC_API_URL
    : 'http://localhost:5231';

const PROD_API = 'https://bc.moneysb.com';

function normalizeBaseUrl(url: string) {
  return url.replace(/\/+$/, '');
}

export const BASE_URL = normalizeBaseUrl(__DEV__ ? DEV_API : PROD_API);
export const FRONTEND_CACHE_ENABLED =
  typeof process !== 'undefined' &&
  process.env?.EXPO_PUBLIC_DISABLE_FRONTEND_CACHE === 'true'
    ? false
    : true;

const TOKEN_KEY = '@auth_token';
const REQUEST_TIMEOUT_MS = 60000;
const MAX_RETRY_COUNT = 1;
const RETRY_DELAY_MS = 800;

let onUnauthorizedCallback: (() => void) | null = null;

type RequestOptionsWithParams = RequestInit & {
  params?: Record<string, string | number | boolean | null | undefined>;
};

export interface ApiResult<T = unknown> {
  success?: boolean;
  code?: number;
  message?: string;
  data?: T;
}

const PROD_API_MISSING_MESSAGE = '未配置生产环境接口地址';

export function setUnauthorizedCallback(callback: (() => void) | null) {
  onUnauthorizedCallback = callback;
}

export function apiOk(result: ApiResult | null | undefined) {
  if (!result) return false;

  if (result.code !== undefined && result.code !== null) {
    const code = Number(result.code);
    return !Number.isNaN(code) && (code === 0 || code === 200);
  }

  return Boolean(result.success);
}

function getConfiguredBaseUrl() {
  if (BASE_URL) {
    return BASE_URL;
  }

  if (!__DEV__) {
    console.error(PROD_API_MISSING_MESSAGE);
  }

  return '';
}

export async function getToken(): Promise<string | null> {
  try {
    return await AsyncStorage.getItem(TOKEN_KEY);
  } catch (error) {
    console.error('获取 token 失败:', error);
    return null;
  }
}

export async function setToken(token: string): Promise<void> {
  try {
    await AsyncStorage.setItem(TOKEN_KEY, token);
  } catch (error) {
    console.error('保存 token 失败:', error);
  }
}

export async function clearToken(): Promise<void> {
  try {
    await AsyncStorage.removeItem(TOKEN_KEY);
  } catch (error) {
    console.error('清除 token 失败:', error);
  }
}

async function handleUnauthorized() {
  try {
    await AsyncStorage.removeItem(TOKEN_KEY);
    if (onUnauthorizedCallback) {
      setTimeout(() => {
        onUnauthorizedCallback?.();
      }, 0);
    }
  } catch (error) {
    console.error('清除登录信息失败:', error);
  }
}

export function toAbsoluteUrl(url?: string | null) {
  if (!url) return '';
  if (url.startsWith('http://') || url.startsWith('https://')) return url;
  const baseUrl = getConfiguredBaseUrl();
  if (!baseUrl) return '';
  if (url.startsWith('/')) return `${baseUrl}${url}`;
  return `${baseUrl}/${url}`;
}

function sleep(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError';
}

function getMethod(options: RequestInit) {
  return String(options.method ?? 'GET').toUpperCase();
}

function shouldRetry(method: string, attempt: number, status?: number) {
  if (attempt >= MAX_RETRY_COUNT) return false;
  if (method !== 'GET') return false;
  if (status === undefined) return true;
  return [408, 429, 502, 503, 504].includes(status);
}

export async function request<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<ApiResult<T>> {
  try {
    const baseUrl = getConfiguredBaseUrl();
    if (!baseUrl) {
      return {
        success: false,
        code: -1,
        message: PROD_API_MISSING_MESSAGE,
      };
    }

    const token = await getToken();
    const method = getMethod(options);
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      'X-Client-Platform': Platform.OS,
      ...(options.headers as Record<string, string>),
    };

    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    if (!FRONTEND_CACHE_ENABLED && method === 'GET') {
      headers['Cache-Control'] = 'no-cache, no-store, max-age=0';
      headers.Pragma = 'no-cache';
      headers.Expires = '0';
    }

    let url = `${baseUrl}${endpoint}`;
    const requestOptions = options as RequestOptionsWithParams;

    if (requestOptions.params && Object.keys(requestOptions.params).length > 0) {
      const query = Object.entries(requestOptions.params)
        .filter(([, value]) => value !== null && value !== undefined && value !== '')
        .map(
          ([key, value]) =>
            `${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`
        )
        .join('&');

      if (query) {
        url += endpoint.includes('?') ? `&${query}` : `?${query}`;
      }
    }

    const { params: _ignoredParams, ...fetchOptions } = requestOptions;

    for (let attempt = 0; attempt <= MAX_RETRY_COUNT; attempt += 1) {
      const controller =
        typeof AbortController !== 'undefined' && !fetchOptions.signal
          ? new AbortController()
          : null;
      const timeoutId =
        controller != null
          ? setTimeout(() => {
              controller.abort();
            }, REQUEST_TIMEOUT_MS)
          : null;

      try {
        const response = await fetch(url, {
          ...fetchOptions,
          ...(Platform.OS === 'web' && method === 'GET' ? { cache: 'no-store' as RequestCache } : null),
          headers,
          signal: fetchOptions.signal ?? controller?.signal,
        });

        if (timeoutId != null) {
          clearTimeout(timeoutId);
        }

        if (!response.ok) {
          if (response.status === 401) {
            await handleUnauthorized();
          }

          if (shouldRetry(method, attempt, response.status)) {
            await sleep(RETRY_DELAY_MS * (attempt + 1));
            continue;
          }

          let errorMessage = `请求失败: ${response.status} ${response.statusText}`;
          try {
            const errorData = (await response.json()) as Record<string, unknown>;
            if (typeof errorData.message === 'string' && errorData.message) {
              errorMessage = errorData.message;
            } else if (typeof errorData.Message === 'string' && errorData.Message) {
              errorMessage = errorData.Message;
            }
          } catch {
            // ignore parse error
          }

          return {
            success: false,
            code: response.status,
            message: errorMessage,
          };
        }

        if (response.status === 204) {
          return {
            success: false,
            code: 204,
            message: '服务器未返回有效数据（无内容），请稍后重试',
          };
        }

        const rawText = await response.text();
        if (!rawText || !rawText.trim()) {
          return {
            success: false,
            code: response.status,
            message: '服务器未返回有效数据，请稍后重试',
          };
        }

        let rawData: Record<string, unknown>;

        try {
          rawData = JSON.parse(rawText) as Record<string, unknown>;
        } catch {
          return {
            success: false,
            code: response.status,
            message: rawText.slice(0, 200) || '服务器返回格式异常',
          };
        }

        const data = (
          rawData && typeof rawData === 'object'
            ? {
                ...rawData,
                success: rawData.success ?? rawData.Success,
                code: rawData.code ?? rawData.Code,
                message: rawData.message ?? rawData.Message,
                data: rawData.data ?? rawData.Data,
              }
            : {}
        ) as ApiResult<T>;

        const rawCode = data.code;
        if (rawCode !== undefined && rawCode !== null && String(rawCode) !== '') {
          const code = typeof rawCode === 'number' ? rawCode : Number(rawCode);
          if (!Number.isNaN(code)) {
            data.success = code === 0 || code === 200;
          } else {
            data.success = false;
          }
        } else if (data.success === undefined) {
          data.success = false;
        }

        if (
          Number(data.code) === 401 ||
          Number(data.code) === 8888 ||
          (data.success === false &&
            typeof data.message === 'string' &&
            data.message.includes('未登录'))
        ) {
          await handleUnauthorized();
        }

        return data as ApiResult<T>;
      } catch (error) {
        if (timeoutId != null) {
          clearTimeout(timeoutId);
        }

        if (shouldRetry(method, attempt)) {
          await sleep(RETRY_DELAY_MS * (attempt + 1));
          continue;
        }

        console.error('API 请求失败:', error);
        return {
          success: false,
          code: -1,
          message: isAbortError(error)
            ? '请求超时，请稍后重试'
            : '网络连接异常，请检查网络后重试',
        };
      }
    }

    return {
      success: false,
      code: -1,
      message: '网络连接异常，请检查网络后重试',
    };
  } catch (error) {
    console.error('API 请求失败:', error);
    return {
      success: false,
      code: -1,
      message: '网络连接异常，请检查网络后重试',
    };
  }
}

export const api = {
  get: <T>(endpoint: string, options?: RequestInit) =>
    request<T>(endpoint, { ...options, method: 'GET' }),
  post: <T>(endpoint: string, body?: unknown, options?: RequestInit) =>
    request<T>(endpoint, {
      ...options,
      method: 'POST',
      body: body !== undefined && body !== null ? JSON.stringify(body) : undefined,
    }),
};
