import { getMemberInfo } from '@/lib/api/auth';
import { getToken } from '@/lib/api/request';
import React, { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { AppState, type AppStateStatus } from 'react-native';

type BalanceContextValue = {
  memberInfo: Record<string, any> | null;
  refreshBalance: () => Promise<void>;
  loading: boolean;
};

const BalanceContext = createContext<BalanceContextValue | null>(null);
const POLL_INTERVAL_MS = 25_000;

export function BalanceProvider({ children }: { children: React.ReactNode }) {
  const [memberInfo, setMemberInfo] = useState<Record<string, any> | null>(null);
  const [loading, setLoading] = useState(false);

  const doFetch = useCallback(async () => {
    const token = await getToken();
    if (!token) {
      setMemberInfo(null);
      return;
    }

    try {
      const result = await getMemberInfo();
      if (result.success && result.data) {
        setMemberInfo(result.data as Record<string, any>);
      }
    } catch {
      // ignore
    }
  }, []);

  const refreshBalance = useCallback(async () => {
    const token = await getToken();
    if (!token) {
      setMemberInfo(null);
      return;
    }

    setLoading(true);
    try {
      await doFetch();
    } finally {
      setLoading(false);
    }
  }, [doFetch]);

  useEffect(() => {
    let mounted = true;

    const tick = async () => {
      if (!mounted || AppState.currentState !== 'active') return;
      const token = await getToken();
      if (!token) {
        setMemberInfo(null);
        return;
      }

      void doFetch();
    };

    const interval = setInterval(tick, POLL_INTERVAL_MS);

    if (AppState.currentState === 'active') {
      void doFetch();
    }

    const sub = AppState.addEventListener('change', (state: AppStateStatus) => {
      if (state === 'active') {
        void doFetch();
      }
    });

    return () => {
      mounted = false;
      clearInterval(interval);
      sub.remove();
    };
  }, [doFetch]);

  return (
    <BalanceContext.Provider value={{ memberInfo, refreshBalance, loading }}>
      {children}
    </BalanceContext.Provider>
  );
}

export function useBalance() {
  const context = useContext(BalanceContext);
  if (!context) {
    throw new Error('useBalance must be used within BalanceProvider');
  }
  return context;
}

export function useBalanceOptional() {
  return useContext(BalanceContext);
}
