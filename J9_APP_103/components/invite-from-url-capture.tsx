import { applyInviteParamsFromUrlToStorage } from '@/lib/pending-invite';
import { useGlobalSearchParams } from 'expo-router';
import { useEffect } from 'react';

export function InviteFromUrlCapture() {
  const { invite, agentId, agentName } = useGlobalSearchParams<{
    invite?: string | string[];
    agentId?: string | string[];
    agentName?: string | string[];
  }>();

  useEffect(() => {
    void applyInviteParamsFromUrlToStorage({ invite, agentId, agentName });
  }, [invite, agentId, agentName]);

  return null;
}
