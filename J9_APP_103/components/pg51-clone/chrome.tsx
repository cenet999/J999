import { useAuthModal } from '@/components/auth/auth-modal-provider';
import {
  Pg51BottomIcon,
  Pg51CategoryIcon,
  Pg51QuickActionIcon,
} from '@/components/pg51-clone/original-icons';
import type { Pg51Category, Pg51CategoryId } from '@/components/pg51-clone/types';
import { Text } from '@/components/ui/text';
import { getMessages, MessageSenderRole, MessageStatus } from '@/lib/api/message';
import { useTenantTitle } from '@/lib/api/tenant';
import { useFocusEffect, usePathname, useRouter } from 'expo-router';
import * as React from 'react';
import type { ReactNode } from 'react';
import {
  Animated,
  Pressable,
  ScrollView,
  type NativeScrollEvent,
  type NativeSyntheticEvent,
  type ScrollViewProps,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

type Pg51BottomTabId = 'home' | 'activity' | 'deposit' | 'earn' | 'mine';

const pg51BottomItems: Array<{
  id: Pg51BottomTabId;
  label: string;
  icon: 'home' | 'activity' | 'deposit' | 'earn' | 'mine';
  href: '/' | '/activity' | '/deposit' | '/earn' | '/mine';
  badge?: string;
}> = [
  { id: 'home', label: '首页', icon: 'home', href: '/' },
  { id: 'activity', label: '活动', icon: 'activity', href: '/activity' },
  { id: 'deposit', label: '存款', icon: 'deposit', href: '/deposit', badge: '送3%' },
  { id: 'earn', label: '福利', icon: 'earn', href: '/earn' },
  { id: 'mine', label: '我的', icon: 'mine', href: '/mine' },
];

const mineRelatedPaths = [
  '/mine',
  '/transactions',
  '/invite-friends',
  '/rebate',
  '/change-password',
  '/bind-info',
  '/messages',
  '/chat',
  '/help-center',
  '/about',
];

const pg51QuickActions: Array<{ id: 'deposit' | 'withdraw' | 'service'; label: string }> = [
  { id: 'deposit', label: '存款' },
  { id: 'withdraw', label: '取款' },
  { id: 'service', label: '客服' },
];

const BOTTOM_NAV_HIDE_DISTANCE = 92;
const HIDE_TRIGGER_DISTANCE = 24;
const SHOW_TRIGGER_DISTANCE = 16;
const TOP_SHOW_THRESHOLD = 12;

type BottomNavScrollContextValue = {
  handleScroll: (event: NativeSyntheticEvent<NativeScrollEvent>) => void;
};

const BottomNavScrollContext = React.createContext<BottomNavScrollContextValue>({
  handleScroll: () => {},
});

function getActiveBottomTab(pathname: string): Pg51BottomTabId {
  if (pathname.startsWith('/activity')) return 'activity';
  if (pathname.startsWith('/deposit')) return 'deposit';
  if (pathname.startsWith('/earn')) return 'earn';
  if (mineRelatedPaths.some((path) => pathname.startsWith(path))) return 'mine';
  return 'home';
}

type Pg51PageShellProps = {
  children: ReactNode;
  withBottomNav?: boolean;
};

export function Pg51PageShell({ children, withBottomNav = true }: Pg51PageShellProps) {
  const pathname = usePathname();
  const [bottomNavVisible, setBottomNavVisible] = React.useState(true);
  const translateY = React.useRef(new Animated.Value(0)).current;
  const lastScrollYRef = React.useRef(0);
  const accumulatedDeltaRef = React.useRef(0);

  const animateBottomNav = React.useCallback(
    (visible: boolean) => {
      setBottomNavVisible((current) => {
        if (current === visible) return current;

        Animated.timing(translateY, {
          toValue: visible ? 0 : BOTTOM_NAV_HIDE_DISTANCE,
          duration: 180,
          useNativeDriver: true,
        }).start();

        return visible;
      });
    },
    [translateY]
  );

  const showBottomNav = React.useCallback(() => {
    accumulatedDeltaRef.current = 0;
    animateBottomNav(true);
  }, [animateBottomNav]);

  const hideBottomNav = React.useCallback(() => {
    accumulatedDeltaRef.current = 0;
    animateBottomNav(false);
  }, [animateBottomNav]);

  const handleScroll = React.useCallback(
    (event: NativeSyntheticEvent<NativeScrollEvent>) => {
      const currentY = event.nativeEvent.contentOffset.y;

      if (currentY <= TOP_SHOW_THRESHOLD) {
        lastScrollYRef.current = currentY;
        showBottomNav();
        return;
      }

      const deltaY = currentY - lastScrollYRef.current;
      lastScrollYRef.current = currentY;

      if (Math.abs(deltaY) < 2) return;

      if (deltaY > 0) {
        accumulatedDeltaRef.current = Math.max(0, accumulatedDeltaRef.current) + deltaY;
      } else {
        accumulatedDeltaRef.current = Math.min(0, accumulatedDeltaRef.current) + deltaY;
      }

      if (accumulatedDeltaRef.current >= HIDE_TRIGGER_DISTANCE) {
        hideBottomNav();
      } else if (accumulatedDeltaRef.current <= -SHOW_TRIGGER_DISTANCE) {
        showBottomNav();
      }
    },
    [hideBottomNav, showBottomNav]
  );

  const noopHandleScroll = React.useCallback(() => {}, []);

  React.useEffect(() => {
    lastScrollYRef.current = 0;
    accumulatedDeltaRef.current = 0;

    if (withBottomNav) {
      translateY.setValue(0);
      setBottomNavVisible(true);
      return;
    }

    translateY.setValue(BOTTOM_NAV_HIDE_DISTANCE);
    setBottomNavVisible(false);
  }, [pathname, translateY, withBottomNav]);

  const bottomNavOpacity = translateY.interpolate({
    inputRange: [0, BOTTOM_NAV_HIDE_DISTANCE],
    outputRange: [1, 0.35],
    extrapolate: 'clamp',
  });

  return (
    <View className="flex-1 bg-[#121724] web:items-center">
      <BottomNavScrollContext.Provider
        value={{ handleScroll: withBottomNav ? handleScroll : noopHandleScroll }}>
        <View className="w-full flex-1 bg-[#1b2130] web:max-w-[480px]">
          {children}
          {withBottomNav ? (
            <Animated.View
              pointerEvents={bottomNavVisible ? 'auto' : 'none'}
              style={{
                position: 'absolute',
                left: 0,
                right: 0,
                bottom: 0,
                transform: [{ translateY }],
                opacity: bottomNavOpacity,
              }}>
              <Pg51BottomNav />
            </Animated.View>
          ) : null}
        </View>
      </BottomNavScrollContext.Provider>
    </View>
  );
}

export const Pg51TrackedScrollView = React.forwardRef<ScrollView, ScrollViewProps>(
  (
    { onScroll, scrollEventThrottle = 16, keyboardShouldPersistTaps = 'handled', ...props },
    ref
  ) => {
    const { handleScroll } = React.useContext(BottomNavScrollContext);

    const handleTrackedScroll = React.useCallback(
      (event: NativeSyntheticEvent<NativeScrollEvent>) => {
        handleScroll(event);
        onScroll?.(event);
      },
      [handleScroll, onScroll]
    );

    return (
      <ScrollView
        ref={ref}
        {...props}
        keyboardShouldPersistTaps={keyboardShouldPersistTaps}
        onScroll={handleTrackedScroll}
        scrollEventThrottle={scrollEventThrottle}
      />
    );
  }
);

Pg51TrackedScrollView.displayName = 'Pg51TrackedScrollView';

type Pg51TopStripProps = {
  onClose?: () => void;
};

export function Pg51TopStrip({ onClose }: Pg51TopStripProps) {
  const insets = useSafeAreaInsets();

  return (
    <View
      className="flex-row items-center gap-2 bg-[#3a4052] px-3 pb-2.5"
      style={{ minHeight: insets.top + 30, paddingTop: insets.top + 4 }}>
      <Pressable
        onPress={onClose}
        hitSlop={8}
        className="w-6 items-center justify-center"
        disabled={!onClose}>
        <Text className="text-center text-[24px] leading-[24px] text-[#9a57ff]">×</Text>
      </Pressable>
      <Text className="flex-1 text-[12px] leading-[18px] font-medium text-white" numberOfLines={1}>
        官方域名<Text className="text-[#9b5cff] leading-[18px]">999137.com</Text>，请安装APP
      </Text>
      <Text className="text-[14px] font-semibold text-[#9b5cff]">立即下载&gt;</Text>
    </View>
  );
}

export function Pg51Header() {
  return <Pg51HeaderInner />;
}

type Pg51HeaderInnerProps = {
  isAuthenticated?: boolean;
  displayName?: string;
  onLoginPress?: () => void;
  onRegisterPress?: () => void;
  includeSafeAreaTop?: boolean;
};

export function Pg51HeaderInner({
  isAuthenticated = false,
  displayName = '',
  onLoginPress,
  onRegisterPress,
  includeSafeAreaTop = true,
}: Pg51HeaderInnerProps) {
  const router = useRouter();
  const { requireAuth } = useAuthModal();
  const insets = useSafeAreaInsets();
  const tenantTitle = useTenantTitle();
  const [hasUnreadServiceMessage, setHasUnreadServiceMessage] = React.useState(false);
  // 保留“前 2 字紫 + 其余白”的视觉风格：如“九游俱乐部”-> “九游” + “俱乐部”
  const titlePrefix = tenantTitle.slice(0, 2);
  const titleSuffix = tenantTitle.slice(2);

  useFocusEffect(
    React.useCallback(() => {
      if (!isAuthenticated) {
        setHasUnreadServiceMessage(false);
        return;
      }

      let cancelled = false;

      (async () => {
        try {
          const result = await getMessages();
          if (cancelled || !result.success || !result.data) return;

          setHasUnreadServiceMessage(
            result.data.some(
              (message) =>
                message.status === MessageStatus.未读 &&
                message.senderRole !== MessageSenderRole.Customer &&
                message.senderRole !== MessageSenderRole.System
            )
          );
        } catch {
          if (!cancelled) {
            setHasUnreadServiceMessage(false);
          }
        }
      })();

      return () => {
        cancelled = true;
      };
    }, [isAuthenticated])
  );

  return (
    <View className="px-3.5" style={{ paddingTop: includeSafeAreaTop ? insets.top : 0 }}>
      <View className="flex-row items-center justify-between">
        <View className="min-h-[80px] flex-1 justify-center pr-2">
          <View className="flex-row items-center py-0.5">
            <Text className="text-[28px] font-black leading-[34px] tracking-[-0.8px] text-[#8b4dff]">
              {titlePrefix}
            </Text>
            {titleSuffix ? (
              <Text className="text-[28px] font-black leading-[34px] tracking-[-0.8px] text-white">
                {titleSuffix}
              </Text>
            ) : null}
          </View>

          {isAuthenticated ? (
            <View className="mt-2.5 self-start rounded-full bg-[#2e3444] px-3 py-1.5">
              <Text className="text-[12px] text-[#d8dcea]">
                欢迎回来，{displayName || 'J9会员'}
              </Text>
            </View>
          ) : (
            <View className="mt-2.5 flex-row gap-1.5">
              <Pressable
                onPress={onLoginPress}
                className="h-[20px] w-[48px] items-center justify-center rounded-full bg-[#b69652]">
                <Text className="text-center text-[12px] text-white">登录</Text>
              </Pressable>
              <Pressable
                onPress={onRegisterPress}
                className="h-[20px] min-w-[48px] items-center justify-center rounded-full bg-[#6f1dff] px-2.5">
                <Text className="text-center text-[12px] text-white">注册</Text>
              </Pressable>
            </View>
          )}
        </View>

        <View className="w-[154px] items-end">
          <View className="flex-row items-center justify-end gap-2">
            {pg51QuickActions.map((item) => (
              <Pressable
                key={item.id}
                onPress={async () => {
                  const authenticated = await requireAuth('login');
                  if (!authenticated) return;

                  if (item.id === 'deposit') {
                    router.push('/deposit');
                    return;
                  }

                  if (item.id === 'withdraw') {
                    router.push('/mine?openWithdraw=1');
                    return;
                  }

                  if (item.id === 'service') {
                    router.push('/chat');
                  }
                }}
                className="relative w-[46px] items-center justify-center">
                <View className="size-[42px] items-center justify-center rounded-full bg-[#414559]">
                  <Pg51QuickActionIcon
                    name={item.id as 'deposit' | 'withdraw' | 'service'}
                    size={27}
                  />
                  {item.id === 'service' && hasUnreadServiceMessage ? (
                    <View
                      className="absolute rounded-full bg-[#FF2F4F]"
                      style={{
                        top: 3,
                        right: 4,
                        width: 9,
                        height: 9,
                        borderWidth: 1.5,
                        borderColor: '#414559',
                      }}
                    />
                  ) : null}
                </View>
                <Text className="mt-1.5 text-[10px] leading-[12px] text-[#d8dcea]">
                  {item.label}
                </Text>
                {item.id === 'deposit' ? (
                  <View className="absolute -top-1 right-0 rounded-full bg-[#ff655b] px-1.5 py-[1px]">
                    <Text className="text-[8px] font-bold leading-[10px] text-white">送3%</Text>
                  </View>
                ) : null}
              </Pressable>
            ))}
          </View>
        </View>
      </View>
    </View>
  );
}

export function Pg51QuickSection() {
  const chips = ['PG', 'PA', 'CQ9', 'DB'];

  return (
    <View className="px-2 pb-3">
      <View className="flex-row items-center gap-2">
        {chips.map((chip, index) => (
          <Pressable
            key={chip}
            className={`h-[34px] flex-1 items-center justify-center rounded-full ${
              index === 0 ? 'bg-[#6f1dff]' : 'bg-[#353a4d]'
            }`}>
            <Text
              className={`text-[17px] font-bold ${index === 0 ? 'text-white' : 'text-[#8d92a4]'}`}>
              {chip}
            </Text>
          </Pressable>
        ))}

        <View className="size-[34px] items-center justify-center rounded-full bg-[#353a4d]">
          <Text className="text-[18px] text-white/90">⌕</Text>
        </View>
      </View>
    </View>
  );
}

type Pg51SideNavProps = {
  categories: Pg51Category[];
  activeCategory: Pg51CategoryId;
  onChange: (value: Pg51CategoryId) => void;
};

export function Pg51SideNav({ categories, activeCategory, onChange }: Pg51SideNavProps) {
  return (
    <View className="w-[52px]">
      {categories.map((item) => {
        const active = item.id === activeCategory;
        return (
          <Pressable
            key={item.id}
            onPress={() => onChange(item.id)}
            className={`mb-2 h-[70px] items-center justify-center rounded-[16px] px-0.5 py-1 ${
              active ? 'bg-[#6f1dff]' : 'bg-[#353a4d]'
            }`}>
            <Pg51CategoryIcon category={item.id} active={active} size={34} />
            <Text className={`mt-0.5 text-[11px] ${active ? 'text-white' : 'text-[#9ea5b8]'}`}>
              {item.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

export function Pg51BottomNav() {
  const pathname = usePathname();
  const router = useRouter();
  const { requireAuth } = useAuthModal();
  const activeTab = getActiveBottomTab(pathname);

  return (
    <View className="bg-transparent px-2 pb-1.5">
      <View className="h-[70px] rounded-[22px] bg-[#383c4f] px-2.5 pb-1.5 pt-1 shadow-2xl shadow-black/40">
        <View className="h-full flex-row items-end justify-between">
          {pg51BottomItems.map((item) => {
            const active = item.id === activeTab;

            return (
              <Pressable
                key={item.id}
                onPress={async () => {
                  if (item.id !== 'home') {
                    const authenticated = await requireAuth('login');
                    if (!authenticated) return;
                  }

                  router.replace(item.href);
                }}
                className="relative flex-1 items-center">
                {active ? (
                  <View className="absolute bottom-0 left-1 right-1 h-[56px] rounded-[18px] bg-[#6f1dff]" />
                ) : null}

                {item.badge ? (
                  <View className="absolute right-[2px] top-[6px] z-10">
                    <View className="rounded-full bg-[#ff655b] px-1.5 py-[1px]">
                      <Text className="text-[9px] font-bold text-white">{item.badge}</Text>
                    </View>
                  </View>
                ) : null}

                <View className="h-[60px] items-center justify-end">
                  <Pg51BottomIcon name={item.icon} active={active} activeTone="white" size={28} />
                  <Text
                    className={`mt-1 text-[11px] ${
                      active ? 'font-semibold text-white' : 'text-[#d4d8e0]'
                    }`}>
                    {item.label}
                  </Text>
                  {active ? (
                    <View className="mt-1 h-[2px] w-5 rounded-full bg-white" />
                  ) : (
                    <View className="mt-1 h-[2px]" />
                  )}
                </View>
              </Pressable>
            );
          })}
        </View>
      </View>
    </View>
  );
}
