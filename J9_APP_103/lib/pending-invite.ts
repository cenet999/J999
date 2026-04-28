import AsyncStorage from '@react-native-async-storage/async-storage';
import { parseAgentIdParam, parseAgentNameParam } from '@/lib/register-link';

const STORAGE_KEY = '@j9_pending_invite';
const PENDING_INVITE_TTL_MS = 7 * 24 * 60 * 60 * 1000;

export interface PendingInvitePayload {
  invite: string;
  agentId: number;
  agentName: string;
  savedAt: number;
}

function isExpired(savedAt: number) {
  return Date.now() - savedAt > PENDING_INVITE_TTL_MS;
}

export async function getPendingInvite(): Promise<PendingInvitePayload | null> {
  try {
    const raw = await AsyncStorage.getItem(STORAGE_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as Partial<PendingInvitePayload>;
    const savedAt = typeof parsed.savedAt === 'number' ? parsed.savedAt : 0;

    if (!savedAt || isExpired(savedAt)) {
      await AsyncStorage.removeItem(STORAGE_KEY);
      return null;
    }

    return {
      invite: typeof parsed.invite === 'string' ? parsed.invite : '',
      agentId:
        typeof parsed.agentId === 'number' && Number.isFinite(parsed.agentId)
          ? parsed.agentId
          : 0,
      agentName: typeof parsed.agentName === 'string' ? parsed.agentName : '',
      savedAt,
    };
  } catch {
    return null;
  }
}

export function mergeInviteFromUrlAndStored(
  url: {
    invite?: string | string[];
    agentId?: string | string[];
    agentName?: string | string[];
  },
  stored: PendingInvitePayload | null
): { invite: string; agentId: number; agentName: string } {
  const hasInviteKey = url.invite !== undefined;
  const urlInvite = hasInviteKey
    ? (typeof url.invite === 'string' ? url.invite : url.invite?.[0]) ?? ''
    : '';

  return {
    invite: hasInviteKey ? urlInvite : stored?.invite ?? '',
    agentId: url.agentId !== undefined ? parseAgentIdParam(url.agentId) : stored?.agentId ?? 0,
    agentName:
      url.agentName !== undefined
        ? parseAgentNameParam(url.agentName)
        : stored?.agentName ?? '',
  };
}

export async function setPendingInvite(payload: {
  invite: string;
  agentId: number;
  agentName: string;
}): Promise<void> {
  await AsyncStorage.setItem(
    STORAGE_KEY,
    JSON.stringify({
      ...payload,
      savedAt: Date.now(),
    } satisfies PendingInvitePayload)
  );
}

export async function clearPendingInvite(): Promise<void> {
  try {
    await AsyncStorage.removeItem(STORAGE_KEY);
  } catch {
    // ignore
  }
}

export async function applyInviteParamsFromUrlToStorage(url: {
  invite?: string | string[];
  agentId?: string | string[];
  agentName?: string | string[];
}): Promise<void> {
  if (
    url.invite === undefined &&
    url.agentId === undefined &&
    url.agentName === undefined
  ) {
    return;
  }

  const stored = await getPendingInvite();
  const merged = mergeInviteFromUrlAndStored(url, stored);

  if (merged.invite || merged.agentId > 0 || merged.agentName) {
    await setPendingInvite(merged);
  } else {
    await clearPendingInvite();
  }
}
