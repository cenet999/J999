import 'react-native-gesture-handler';
import '../global.css';

import { InviteFromUrlCapture } from '@/components/invite-from-url-capture';
import { AuthModalProvider } from '@/components/auth/auth-modal-provider';
import { ToastProvider } from '@/components/ui/toast';
import { UpdateModal, type UpdateStatus } from '@/components/ui/update-modal';
import { getToken } from '@/lib/api/request';
import { DEFAULT_TENANT_TITLE, useTenantTitle } from '@/lib/api/tenant';
import { BalanceProvider } from '@/lib/contexts/balance-context';
import { NAV_THEME } from '@/lib/theme';
import { ThemeProvider } from '@react-navigation/native';
import { PortalHost } from '@rn-primitives/portal';
import { Stack, usePathname, useRouter } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { verifyInstallation } from 'nativewind';
import * as Updates from 'expo-updates';
import { useColorScheme } from 'nativewind';
import * as React from 'react';
import { Platform } from 'react-native';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { SafeAreaProvider, initialWindowMetrics } from 'react-native-safe-area-context';

export {
  // Catch any errors thrown by the Layout component.
  ErrorBoundary,
} from 'expo-router';

const AUTH_REQUIRED_PATHS = [
  '/activity',
  '/deposit',
  '/earn',
  '/mine',
  '/tasks',
  '/task-points-calendar',
  '/transactions',
  '/invite-friends',
  '/rebate',
  '/change-password',
  '/bind-info',
  '/messages',
  '/chat',
];

function isAuthRequiredPath(pathname: string) {
  return AUTH_REQUIRED_PATHS.some((item) => pathname === item || pathname.startsWith(`${item}/`));
}

const bottomTabScreenOptions =
  Platform.OS === 'ios'
    ? {
        animation: 'none' as const,
      }
    : undefined;

function AuthRouteGuard() {
  const pathname = usePathname();
  const router = useRouter();

  React.useEffect(() => {
    if (!isAuthRequiredPath(pathname)) return;

    let cancelled = false;

    (async () => {
      const token = await getToken();
      if (!token && !cancelled) {
        router.replace('/login');
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [pathname, router]);

  return null;
}

export default function RootLayout() {
  const { colorScheme } = useColorScheme();
  const router = useRouter();
  const pathname = usePathname();
  const tenantTitle = useTenantTitle();
  const [updateModalVisible, setUpdateModalVisible] = React.useState(false);
  const [updateStatus, setUpdateStatus] = React.useState<UpdateStatus>('checking');
  const [updateProgress, setUpdateProgress] = React.useState(0);
  const [updateErrorMessage, setUpdateErrorMessage] = React.useState<string>();

  React.useEffect(() => {
    if (!__DEV__) return;

    try {
      verifyInstallation();
    } catch (error) {
      console.error('NativeWind verifyInstallation failed:', error);
    }
  }, []);

  React.useEffect(() => {
    if (Platform.OS !== 'web') return;
    if (typeof document === 'undefined') return;
    const nextTitle = tenantTitle || DEFAULT_TENANT_TITLE;
    if (document.title !== nextTitle) {
      document.title = nextTitle;
    }
  }, [pathname, tenantTitle]);

  React.useEffect(() => {
    const checkUpdate = async () => {
      if (__DEV__ || !Updates.isEnabled) return;

      try {
        const update = await Updates.checkForUpdateAsync();
        if (!update.isAvailable) {
          setUpdateModalVisible(false);
          return;
        }

        setUpdateModalVisible(true);
        setUpdateStatus('downloading');
        setUpdateProgress(0);
        setUpdateErrorMessage(undefined);

        let simulatedProgress = 0;
        const progressInterval = setInterval(() => {
          simulatedProgress += Math.random() * 15 + 5;
          if (simulatedProgress >= 90) {
            clearInterval(progressInterval);
            setUpdateProgress(90);
          } else {
            setUpdateProgress(Math.min(simulatedProgress, 90));
          }
        }, 200);

        try {
          await Updates.fetchUpdateAsync();
          clearInterval(progressInterval);
          setUpdateProgress(100);
          setUpdateStatus('ready');

          await new Promise((resolve) => setTimeout(resolve, 500));
          await Updates.reloadAsync();
        } catch (downloadError) {
          clearInterval(progressInterval);
          setUpdateStatus('error');
          setUpdateErrorMessage(
            downloadError instanceof Error ? downloadError.message : '下载更新失败，请检查网络连接'
          );
          await new Promise((resolve) => setTimeout(resolve, 3000));
          setUpdateModalVisible(false);
        }
      } catch (error) {
        console.warn('检查更新失败:', error);
      }
    };

    void checkUpdate();
  }, []);

  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <SafeAreaProvider initialMetrics={initialWindowMetrics}>
        <BalanceProvider>
          <InviteFromUrlCapture />
          <ThemeProvider value={NAV_THEME[colorScheme ?? 'light']}>
            <AuthModalProvider>
              <StatusBar style="light" />
              <AuthRouteGuard />
              <Stack screenOptions={{ headerShown: false }}>
                <Stack.Screen name="index" options={bottomTabScreenOptions} />
                <Stack.Screen name="activity" options={bottomTabScreenOptions} />
                <Stack.Screen name="deposit" options={bottomTabScreenOptions} />
                <Stack.Screen name="earn" options={bottomTabScreenOptions} />
                <Stack.Screen name="mine" options={bottomTabScreenOptions} />
              </Stack>
              <PortalHost />
              <ToastProvider />
              <UpdateModal
                visible={updateModalVisible}
                status={updateStatus}
                progress={updateProgress}
                errorMessage={updateErrorMessage}
              />
            </AuthModalProvider>
          </ThemeProvider>
        </BalanceProvider>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}
