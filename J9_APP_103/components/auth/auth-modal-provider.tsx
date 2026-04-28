import { getMemberInfo } from '@/lib/api/auth';
import { apiOk, getToken, setUnauthorizedCallback } from '@/lib/api/request';
import { AuthModal, type AuthMode } from '@/components/auth/auth-modal';
import { usePathname, useRouter } from 'expo-router';
import * as React from 'react';

type AuthModalContextValue = {
  openAuthModal: (mode?: AuthMode) => void;
  closeAuthModal: () => void;
  isAuthenticated: boolean;
  displayName: string;
  refreshAuthState: () => Promise<void>;
  requireAuth: (mode?: AuthMode) => Promise<boolean>;
};

const AuthModalContext = React.createContext<AuthModalContextValue | null>(null);

export function AuthModalProvider({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const [visible, setVisible] = React.useState(false);
  const [mode, setMode] = React.useState<AuthMode>('login');
  const [isAuthenticated, setIsAuthenticated] = React.useState(false);
  const [displayName, setDisplayName] = React.useState('');

  const refreshAuthState = React.useCallback(async () => {
    const token = await getToken();

    if (!token) {
      setIsAuthenticated(false);
      setDisplayName('');
      return;
    }

    try {
      const result = await getMemberInfo();
      const member = result.data;
      const authenticated = apiOk(result) && Boolean(member);

      setIsAuthenticated(authenticated);

      if (!authenticated) {
        setDisplayName('');
        return;
      }

      const name =
        member?.Nickname ||
        member?.nickname ||
        member?.Username ||
        member?.username ||
        '';

      setDisplayName(String(name).trim());
    } catch {
      setIsAuthenticated(false);
      setDisplayName('');
    }
  }, []);

  React.useEffect(() => {
    refreshAuthState();
  }, [refreshAuthState]);

  React.useEffect(() => {
    setUnauthorizedCallback(() => {
      setIsAuthenticated(false);
      setDisplayName('');
      setVisible(false);
      router.replace('/login');
    });

    return () => {
      setUnauthorizedCallback(null);
    };
  }, [router]);

  const openAuthModal = React.useCallback((nextMode: AuthMode = 'login') => {
    setMode(nextMode);
    setVisible(true);
  }, []);

  const requireAuth = React.useCallback(
    async (nextMode: AuthMode = 'login') => {
      const token = await getToken();
      const authenticated = Boolean(token);
      setIsAuthenticated(authenticated);
      if (!authenticated) {
        setDisplayName('');
      }

      if (!authenticated) {
        const targetPath = nextMode === 'register' ? '/register' : '/login';

        if (pathname === targetPath) {
          setMode(nextMode);
          setVisible(true);
        } else {
          router.replace(targetPath);
        }
      }

      return authenticated;
    },
    [pathname, router]
  );

  const closeAuthModal = React.useCallback(() => {
    setVisible(false);
  }, []);

  return (
    <AuthModalContext.Provider
      value={{
        openAuthModal,
        closeAuthModal,
        isAuthenticated,
        displayName,
        refreshAuthState,
        requireAuth,
      }}>
      {children}
      <AuthModal
        visible={visible}
        mode={mode}
        onClose={closeAuthModal}
        onAuthSuccess={refreshAuthState}
      />
    </AuthModalContext.Provider>
  );
}

export function useAuthModal() {
  const context = React.useContext(AuthModalContext);

  if (!context) {
    throw new Error('useAuthModal must be used within AuthModalProvider');
  }

  return context;
}
