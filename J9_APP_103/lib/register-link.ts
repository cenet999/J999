import { Platform } from 'react-native';
import * as Linking from 'expo-linking';

export function parseAgentIdParam(agentId: string | string[] | undefined): number {
  const raw = typeof agentId === 'string' ? agentId : agentId?.[0];
  if (raw == null || String(raw).trim() === '') return 0;

  const parsed = Number(raw);
  if (!Number.isFinite(parsed) || parsed <= 0 || parsed > Number.MAX_SAFE_INTEGER) {
    return 0;
  }

  return Math.floor(parsed);
}

export function parseAgentNameParam(agentName: string | string[] | undefined): string {
  const raw = typeof agentName === 'string' ? agentName : agentName?.[0];
  return raw == null ? '' : String(raw).trim();
}

export function buildRegisterInviteLink(
  inviteCode: string,
  agentId?: number,
  agentName?: string
): string {
  if (!inviteCode) return '';

  const queryParams: Record<string, string> = {
    invite: inviteCode,
  };

  if (agentId && agentId > 0) {
    queryParams.agentId = String(agentId);
  }

  const trimmedName = agentName?.trim();
  if (trimmedName) {
    queryParams.agentName = trimmedName;
  }

  if (Platform.OS === 'web' && typeof window !== 'undefined') {
    const url = new URL(`${window.location.origin}/register`);
    Object.entries(queryParams).forEach(([key, value]) => {
      url.searchParams.set(key, value);
    });
    return url.toString();
  }

  return Linking.createURL('/register', { queryParams });
}

export function buildHomeInviteLink(
  inviteCode: string,
  agentId?: number,
  agentName?: string
): string {
  if (!inviteCode) return '';

  const queryParams: Record<string, string> = {
    invite: inviteCode,
  };

  if (agentId && agentId > 0) {
    queryParams.agentId = String(agentId);
  }

  const trimmedName = agentName?.trim();
  if (trimmedName) {
    queryParams.agentName = trimmedName;
  }

  if (Platform.OS === 'web' && typeof window !== 'undefined') {
    const url = new URL(`${window.location.origin}/`);
    Object.entries(queryParams).forEach(([key, value]) => {
      url.searchParams.set(key, value);
    });
    return url.toString();
  }

  return Linking.createURL('/', { queryParams });
}
