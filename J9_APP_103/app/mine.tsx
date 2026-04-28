import { useAuthModal } from '@/components/auth/auth-modal-provider';
import {
  Pg51ChromeIconBadge,
  Pg51LucideIconBadge,
  Pg51MineMenuIcon,
  type Pg51MineMenuIconName,
} from '@/components/pg51-clone/original-icons';
import { Pg51InnerPage, Pg51SectionCard } from '@/components/pg51-clone/page-ui';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import { type MemberInfo, getMemberInfo } from '@/lib/api/auth';
import { formatRecycleRecentGamesAmountLine, recycleRecentGames } from '@/lib/api/game';
import { getMessages, MessageSenderRole, MessageStatus } from '@/lib/api/message';
import { apiOk, clearToken, toAbsoluteUrl } from '@/lib/api/request';
import { playerWithdraw } from '@/lib/api/transaction';
import * as Clipboard from 'expo-clipboard';
import { Stack, useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import {
  ArrowDownToLine,
  ArrowUpFromLine,
  Bell,
  ChevronRight,
  Eye,
  EyeOff,
  LogOut,
  Recycle,
  Star,
  Wallet,
  type LucideIcon,
} from 'lucide-react-native';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Image, Pressable, TextInput, View } from 'react-native';
import Animated, {
  Easing,
  cancelAnimation,
  useAnimatedStyle,
  useSharedValue,
  withRepeat,
  withSequence,
  withSpring,
  withTiming,
} from 'react-native-reanimated';

type MenuItemData = {
  title: string;
  hint: string;
  path: string;
  iconName: Pg51MineMenuIconName;
  badge?: string;
};

const SECURITY_MENU: MenuItemData[] = [
  {
    title: '交易明细',
    iconName: 'transactions',
    hint: '充值 / 提现 / 游戏流水',
    path: '/transactions',
  },
  {
    title: '邀请好友',
    iconName: 'invite',
    hint: '邀请码、排行和邀请记录',
    path: '/invite-friends',
  },
  {
    title: '返水中心',
    iconName: 'rebate',
    hint: '查看可领返水和到账记录',
    path: '/rebate',
  },
  {
    title: '修改密码',
    iconName: 'password',
    hint: '修改登录密码，更安全',
    path: '/change-password',
  },
  {
    title: '系统设置',
    iconName: 'settings',
    hint: '手机号、TG、地址、提现密码',
    path: '/bind-info',
  },
];

const OTHER_MENU: MenuItemData[] = [
  {
    title: '消息通知',
    iconName: 'messages',
    hint: '系统公告、客服回复',
    path: '/messages',
  },
  {
    title: '帮助中心',
    iconName: 'help',
    hint: '常见问题和使用说明',
    path: '/help-center',
  },
  {
    title: '关于我们',
    iconName: 'about',
    hint: '平台介绍和服务信息',
    path: '/about',
  },
];

type MineMemberInfo = MemberInfo & {
  Id?: number | string;
  id?: number | string;
  Avatar?: string;
  avatar?: string;
  CreditAmount?: number | string;
  creditAmount?: number | string;
  ActivityPoint?: number | string;
  activityPoint?: number | string;
  VipLevel?: number | string;
  vipLevel?: number | string;
  RebateAmount?: number | string;
  rebateAmount?: number | string;
  USDTAddress?: string;
  usdtAddress?: string;
  WithdrawPassword?: string;
  withdrawPassword?: string;
};

const WITHDRAW_AMOUNTS = [100, 300, 500, 1000, 3000, -1];

function pickText(...values: Array<string | number | null | undefined>) {
  for (const value of values) {
    if (value === null || value === undefined) continue;
    const text = String(value).trim();
    if (text) return text;
  }

  return '';
}

function pickNumber(...values: Array<string | number | null | undefined>) {
  for (const value of values) {
    if (value === null || value === undefined || value === '') continue;
    const num = typeof value === 'number' ? value : Number(value);
    if (!Number.isNaN(num)) return num;
  }

  return 0;
}

function formatCny(amount: number) {
  return `¥${amount.toLocaleString('zh-CN', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

function getDisplayName(memberInfo: MineMemberInfo | null) {
  return pickText(
    memberInfo?.Nickname,
    memberInfo?.nickname,
    memberInfo?.Username,
    memberInfo?.username
  );
}

function getMemberId(memberInfo: MineMemberInfo | null) {
  return pickText(memberInfo?.Id, memberInfo?.id);
}

function getMemberAvatar(memberInfo: MineMemberInfo | null) {
  return toAbsoluteUrl(pickText(memberInfo?.Avatar, memberInfo?.avatar));
}

function QuickActionIcon({
  icon,
  spinning = false,
  toggled = false,
  launching = false,
}: {
  icon: LucideIcon;
  spinning?: boolean;
  toggled?: boolean;
  launching?: boolean;
}) {
  const rotation = useSharedValue(0);
  const translateY = useSharedValue(0);
  const scale = useSharedValue(1);

  useEffect(() => {
    if (spinning) {
      rotation.value = 0;
      rotation.value = withRepeat(
        withTiming(360, {
          duration: 850,
          easing: Easing.linear,
        }),
        -1,
        false
      );
      return;
    }

    if (launching) {
      cancelAnimation(rotation);
      cancelAnimation(translateY);
      cancelAnimation(scale);
      rotation.value = 0;
      translateY.value = 0;
      scale.value = 1;
      rotation.value = withTiming(-180, {
        duration: 260,
        easing: Easing.out(Easing.cubic),
      });
      translateY.value = withSequence(
        withTiming(-4, {
          duration: 180,
          easing: Easing.out(Easing.cubic),
        }),
        withTiming(0, {
          duration: 80,
          easing: Easing.in(Easing.cubic),
        })
      );
      scale.value = withSequence(
        withTiming(1.08, {
          duration: 180,
          easing: Easing.out(Easing.cubic),
        }),
        withTiming(1, {
          duration: 80,
          easing: Easing.in(Easing.cubic),
        })
      );
      return;
    }

    cancelAnimation(rotation);
    cancelAnimation(translateY);
    cancelAnimation(scale);
    rotation.value = withTiming(toggled ? 180 : 0, {
      duration: 220,
      easing: Easing.out(Easing.cubic),
    });
    translateY.value = withSpring(0, { damping: 16, stiffness: 160 });
    scale.value = withSpring(1, { damping: 16, stiffness: 160 });
  }, [launching, rotation, scale, spinning, toggled, translateY]);

  const animatedStyle = useAnimatedStyle(() => ({
    transform: [
      { rotate: `${rotation.value}deg` },
      { translateY: translateY.value },
      { scale: scale.value },
    ],
  }));

  return (
    <Animated.View style={animatedStyle}>
      <Icon as={icon} size={13} color="#F7EFFF" />
    </Animated.View>
  );
}

function QuickActionButton({
  icon,
  label,
  onPress,
  loading = false,
  toggled = false,
  launching = false,
}: {
  icon: LucideIcon;
  label: string;
  onPress?: () => void;
  loading?: boolean;
  toggled?: boolean;
  launching?: boolean;
}) {
  return (
    <Pressable
      onPress={onPress}
      disabled={!onPress || loading || launching}
      className="flex-row items-center gap-1 rounded-full px-3 py-2"
      style={{
        backgroundColor: '#FFFFFF14',
        borderWidth: 1,
        borderColor: '#FFFFFF24',
        opacity: onPress ? (loading || launching ? 0.9 : 1) : 0.8,
      }}>
      <QuickActionIcon icon={icon} spinning={loading} toggled={toggled} launching={launching} />
      <Text className="text-[11px] font-bold text-[#F7EFFF]">{label}</Text>
    </Pressable>
  );
}

function ProfileOverviewCard({
  isAuthenticated,
  memberInfo,
  balanceVisible,
  loading,
  withdrawExpanded,
  withdrawAmount,
  withdrawCustomAmount,
  withdrawPassword,
  withdrawSubmitting,
  rechargeLaunching,
  onToggleBalance,
  onLogin,
  onRecharge,
  onRecycle,
  recycleLoading,
  onWithdrawToggle,
  onWithdrawAmountChange,
  onWithdrawCustomAmountChange,
  onWithdrawPasswordChange,
  onWithdrawSubmit,
}: {
  isAuthenticated: boolean;
  memberInfo: MineMemberInfo | null;
  balanceVisible: boolean;
  loading: boolean;
  withdrawExpanded: boolean;
  withdrawAmount: number | null;
  withdrawCustomAmount: string;
  withdrawPassword: string;
  withdrawSubmitting: boolean;
  rechargeLaunching: boolean;
  onToggleBalance: () => void;
  onLogin: () => void;
  onRecharge: () => void;
  onRecycle: () => void;
  recycleLoading: boolean;
  onWithdrawToggle: () => void;
  onWithdrawAmountChange: (value: number) => void;
  onWithdrawCustomAmountChange: (value: string) => void;
  onWithdrawPasswordChange: (value: string) => void;
  onWithdrawSubmit: () => void;
}) {
  const displayName = getDisplayName(memberInfo) || (isAuthenticated ? 'J9会员' : '游客');
  const memberId = getMemberId(memberInfo);
  const avatarUri = getMemberAvatar(memberInfo);
  const vipLevel = pickNumber(memberInfo?.VipLevel, memberInfo?.vipLevel);
  const points = pickNumber(memberInfo?.ActivityPoint, memberInfo?.activityPoint);
  const balance = pickNumber(memberInfo?.CreditAmount, memberInfo?.creditAmount);
  const showRealData = isAuthenticated && !!memberInfo;
  const balanceText = showRealData ? formatCny(balance) : loading ? '加载中...' : '登录后查看';
  const handleCopyUid = async () => {
    if (!memberId) return;
    await Clipboard.setStringAsync(memberId);
    Toast.show({ type: 'success', text1: 'UID 已复制' });
  };
  const customAmount = Number(withdrawCustomAmount);
  const selectedAmount =
    withdrawAmount === -1
      ? Number.isFinite(customAmount) && customAmount > 0
        ? customAmount
        : 0
      : (withdrawAmount ?? 0);

  return (
    <View
      className="overflow-hidden rounded-[30px]"
      style={{
        backgroundColor: '#43246B',
        borderWidth: 1,
        borderColor: '#5A3891',
      }}>
      <View className="px-4 pb-3 pt-4">
        <View className="flex-row items-center justify-between gap-3">
          <View className="flex-row items-center gap-3">
            {avatarUri ? (
              <Image
                source={{ uri: avatarUri }}
                resizeMode="cover"
                style={{ width: 52, height: 52, borderRadius: 26, backgroundColor: '#FFFFFF22' }}
              />
            ) : (
              <View
                className="items-center justify-center rounded-full"
                style={{ width: 52, height: 52, backgroundColor: '#FFFFFF22' }}>
                <Text className="text-[18px] font-black text-white">
                  {displayName.slice(0, 1) || 'J'}
                </Text>
              </View>
            )}

            <View className="flex-1">
              <View className="flex-row flex-wrap items-center gap-1.5">
                <Text className="text-[16px] font-black text-white">{displayName}</Text>
                {showRealData ? (
                  <View
                    className="flex-row items-center gap-1 rounded-full px-2 py-1"
                    style={{
                      backgroundColor: '#FFD84D22',
                      borderWidth: 1,
                      borderColor: '#FFD84D44',
                    }}>
                    <Icon as={Star} size={11} color="#FFD84D" />
                    <Text className="text-[10px] font-bold text-[#FFD84D]">VIP {vipLevel}</Text>
                  </View>
                ) : null}
                {showRealData ? (
                  <View
                    className="flex-row items-center gap-1 rounded-full px-2 py-1"
                    style={{ backgroundColor: '#FFFFFF14' }}>
                    <Icon as={Star} size={10} color="#FFD84D" />
                    <Text className="text-[10px] font-bold text-[#F2DDB7]">积分 {points}</Text>
                  </View>
                ) : null}
              </View>

              {memberId ? (
                <Pressable
                  onPress={() => {
                    void handleCopyUid();
                  }}
                  hitSlop={8}
                  className="mt-1 self-start rounded-full px-2 py-1"
                  style={{ backgroundColor: '#FFFFFF14' }}>
                  <Text className="text-[11px] text-[#E8DDF8B8]">{`UID: ${memberId}`}</Text>
                </Pressable>
              ) : (
                <Text className="mt-1 text-[11px] text-[#E8DDF8B8]">
                  {isAuthenticated ? '资料加载中...' : '登录后显示 UID'}
                </Text>
              )}
            </View>
          </View>
        </View>
      </View>

      <View style={{ height: 1, backgroundColor: '#FFFFFF14' }} />

      <View className="gap-3 px-4 pb-4 pt-3">
        <View className="flex-row items-end justify-between gap-3">
          <View className="flex-1">
            <View className="flex-row items-center gap-1.5">
              <Icon as={Wallet} size={14} color="#E8DDF8B8" />
              <Text className="text-[11px] font-semibold text-[#E8DDF8B8]">总资产</Text>
              {showRealData ? (
                <Pressable onPress={onToggleBalance} hitSlop={8}>
                  <Icon as={balanceVisible ? Eye : EyeOff} size={14} color="#E8DDF8B8" />
                </Pressable>
              ) : null}
            </View>

            <Text className="mt-2 text-[20px] font-black text-white">
              {showRealData && !balanceVisible ? '******' : balanceText}
            </Text>
          </View>

          {showRealData ? (
            <View className="flex-row flex-wrap justify-end gap-2">
              <QuickActionButton
                icon={ArrowDownToLine}
                label="充值"
                onPress={onRecharge}
                launching={rechargeLaunching}
              />
              <QuickActionButton
                icon={ArrowUpFromLine}
                label="提现"
                onPress={onWithdrawToggle}
                toggled={withdrawExpanded}
              />
              <QuickActionButton
                icon={Recycle}
                label={recycleLoading ? '回收中' : '回收'}
                onPress={onRecycle}
                loading={recycleLoading}
              />
            </View>
          ) : (
            <Pressable
              onPress={onLogin}
              className="rounded-full px-4 py-2.5"
              style={{ backgroundColor: '#FFFFFF14', borderWidth: 1, borderColor: '#FFFFFF24' }}>
              <Text className="text-[12px] font-bold text-white">登录账户</Text>
            </Pressable>
          )}
        </View>
      </View>

      {showRealData && withdrawExpanded ? (
        <>
          <View style={{ height: 1, backgroundColor: '#FFFFFF14' }} />

          <View className="gap-4 px-4 pb-4 pt-4">
            <View className="flex-row items-end justify-between gap-3">
              <View className="gap-1">
                <Text className="text-[18px] font-black text-white">快速提现</Text>
                <Text className="text-[12px] text-[#E8DDF8B8]">输入金额，安全到账</Text>
              </View>

              <View
                className="rounded-full px-3 py-1.5"
                style={{ backgroundColor: '#FF5FA220', borderWidth: 1, borderColor: '#FF5FA244' }}>
                <Text className="text-[11px] font-bold text-[#FFB5CF]">USDT 提现</Text>
              </View>
            </View>

            <View
              className="flex-row items-center justify-between rounded-[18px] px-4 py-3"
              style={{ backgroundColor: '#2A1A46', borderWidth: 1, borderColor: '#5A3891' }}>
              <Text className="text-[12px] font-semibold text-[#CDBEE8]">可用余额</Text>
              <Text className="text-[18px] font-black text-white">{formatCny(balance)}</Text>
            </View>

            <View className="gap-2.5">
              <View className="flex-row gap-2.5">
                {WITHDRAW_AMOUNTS.slice(0, 3).map((amount) => (
                  <WithdrawChip
                    key={amount}
                    amount={amount}
                    selected={withdrawAmount === amount}
                    onPress={() => onWithdrawAmountChange(amount)}
                  />
                ))}
              </View>
              <View className="flex-row gap-2.5">
                {WITHDRAW_AMOUNTS.slice(3).map((amount) => (
                  <WithdrawChip
                    key={amount}
                    amount={amount}
                    selected={withdrawAmount === amount}
                    onPress={() => onWithdrawAmountChange(amount)}
                  />
                ))}
              </View>
            </View>

            {withdrawAmount === -1 ? (
              <View className="gap-2">
                <Text className="text-[13px] font-bold text-white">自定义金额</Text>
                <TextInput
                  value={withdrawCustomAmount}
                  onChangeText={onWithdrawCustomAmountChange}
                  placeholder="请输入提现金额"
                  placeholderTextColor="#8E7BAF"
                  keyboardType="numeric"
                  className="rounded-[18px] px-4 py-3 text-[14px] font-semibold text-white"
                  style={{
                    backgroundColor: '#2A1A46',
                    borderWidth: 1,
                    borderColor: '#5A3891',
                  }}
                />
              </View>
            ) : null}

            <View className="gap-2">
              <Text className="text-[13px] font-bold text-white">提现密码</Text>
              <TextInput
                value={withdrawPassword}
                onChangeText={onWithdrawPasswordChange}
                secureTextEntry
                placeholder="请输入提现密码"
                placeholderTextColor="#8E7BAF"
                className="rounded-[18px] px-4 py-3 text-[14px] font-semibold text-white"
                style={{
                  backgroundColor: '#2A1A46',
                  borderWidth: 1,
                  borderColor: '#5A3891',
                }}
              />
            </View>

            <Pressable
              onPress={onWithdrawSubmit}
              disabled={withdrawSubmitting}
              className="items-center justify-center rounded-[20px] px-4 py-4"
              style={{
                backgroundColor: '#E45A84',
                borderWidth: 1,
                borderColor: '#FF8FB5',
                opacity: withdrawSubmitting ? 0.75 : 1,
              }}>
              {withdrawSubmitting ? (
                <ActivityIndicator color="#ffffff" />
              ) : (
                <Text className="text-[15px] font-black text-white">
                  {selectedAmount > 0 ? `确认提现 ${formatCny(selectedAmount)}` : '确认提现'}
                </Text>
              )}
            </Pressable>
          </View>
        </>
      ) : null}
    </View>
  );
}

function WithdrawChip({
  amount,
  selected,
  onPress,
}: {
  amount: number;
  selected: boolean;
  onPress: () => void;
}) {
  const isCustom = amount === -1;

  return (
    <Pressable
      onPress={onPress}
      className="flex-1 items-center justify-center rounded-[16px] py-3.5"
      style={{
        backgroundColor: selected ? '#5A2140' : '#2A1A46',
        borderWidth: 1,
        borderColor: selected ? '#FF8FB5' : '#5A3891',
      }}>
      <Text className="text-[16px] font-black" style={{ color: selected ? '#FF8FB5' : '#FFFFFF' }}>
        {isCustom ? '自定义' : `¥${amount}`}
      </Text>
    </Pressable>
  );
}

function MenuIconBadge({ item }: { item: MenuItemData }) {
  return (
    <Pg51ChromeIconBadge size={52} radius={16}>
      <View style={{ paddingTop: 1 }}>
        <Pg51MineMenuIcon name={item.iconName} size={33} color="#AEB6C8" />
      </View>
    </Pg51ChromeIconBadge>
  );
}

function MenuSection({
  title,
  items,
  onPress,
}: {
  title: string;
  items: MenuItemData[];
  onPress: (item: MenuItemData) => void;
}) {
  return (
    <Pg51SectionCard title={title} description="">
      {items.map((item) => (
        <Pressable
          key={item.title}
          onPress={() => onPress(item)}
          className="flex-row items-center gap-3 rounded-[20px] px-4 py-3.5"
          style={{
            backgroundColor: '#212838',
            borderWidth: 1,
            borderColor: '#2B3448',
          }}>
          <MenuIconBadge item={item} />
          <View className="flex-1">
            <View className="flex-row items-center gap-2">
              <Text className="text-[14px] font-semibold text-white">{item.title}</Text>
              {item.badge ? (
                <View className="rounded-full bg-[#FF5FA2] px-2 py-0.5">
                  <Text className="text-[10px] font-bold text-white">{item.badge}</Text>
                </View>
              ) : null}
            </View>
            <Text className="mt-1 text-[11px] text-[#9da7bd]">{item.hint}</Text>
          </View>
          <Icon as={ChevronRight} size={18} color="#98a3ba" />
        </Pressable>
      ))}
    </Pg51SectionCard>
  );
}

export default function MineScreen() {
  const { isAuthenticated, refreshAuthState, openAuthModal } = useAuthModal();
  const router = useRouter();
  const params = useLocalSearchParams<{ openWithdraw?: string }>();
  const [memberInfo, setMemberInfo] = useState<MineMemberInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const [balanceVisible, setBalanceVisible] = useState(true);
  const [withdrawExpanded, setWithdrawExpanded] = useState(false);
  const [withdrawAmount, setWithdrawAmount] = useState<number | null>(500);
  const [withdrawCustomAmount, setWithdrawCustomAmount] = useState('');
  const [withdrawPassword, setWithdrawPassword] = useState('');
  const [withdrawSubmitting, setWithdrawSubmitting] = useState(false);
  const [rechargeLaunching, setRechargeLaunching] = useState(false);
  const [recycleLoading, setRecycleLoading] = useState(false);
  const hasHandledOpenWithdrawRef = useRef(false);
  const autoRecycleAttemptedRef = useRef(false);

  useEffect(() => {
    let cancelled = false;

    async function loadMember() {
      if (!isAuthenticated) {
        setMemberInfo(null);
        setLoading(false);
        return;
      }

      setLoading(true);
      try {
        const result = await getMemberInfo();
        if (!cancelled && apiOk(result)) {
          setMemberInfo((result.data as MineMemberInfo | undefined) ?? null);
        }
        if (!cancelled && !apiOk(result)) {
          setMemberInfo(null);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    loadMember();

    return () => {
      cancelled = true;
    };
  }, [isAuthenticated]);

  useFocusEffect(
    useCallback(() => {
      if (!isAuthenticated) {
        setUnreadCount(0);
        return;
      }

      let cancelled = false;

      (async () => {
        try {
          const result = await getMessages();
          if (!result.success || !result.data || cancelled) return;

          const count = result.data.filter(
            (message) =>
              message.status === MessageStatus.未读 &&
              message.senderRole !== MessageSenderRole.Customer &&
              message.senderRole !== MessageSenderRole.System
          ).length;

          setUnreadCount(count);
        } catch {
          if (!cancelled) {
            setUnreadCount(0);
          }
        }
      })();

      return () => {
        cancelled = true;
      };
    }, [isAuthenticated])
  );

  useEffect(() => {
    if (!isAuthenticated) {
      hasHandledOpenWithdrawRef.current = false;
      return;
    }

    if (params.openWithdraw !== '1') {
      hasHandledOpenWithdrawRef.current = false;
      return;
    }

    if (hasHandledOpenWithdrawRef.current) return;

    hasHandledOpenWithdrawRef.current = true;
    setWithdrawExpanded(true);
    router.setParams({ openWithdraw: undefined });
  }, [isAuthenticated, params.openWithdraw, router]);

  const handleLogout = async () => {
    await clearToken();
    setMemberInfo(null);
    await refreshAuthState();
  };

  const handleRecycle = useCallback(async () => {
    const memberId = getMemberId(memberInfo);

    if (!memberId) {
      Toast.show({ type: 'error', text1: '请先登录', text2: '登录后才能使用回收功能。' });
      return;
    }

    if (recycleLoading) return;

    setRecycleLoading(true);
    try {
      const result = await recycleRecentGames(memberId);
      if (!result.ok) {
        Toast.show({ type: 'error', text1: '回收失败', text2: result.message || '请稍后再试。' });
        return;
      }

      Toast.show({
        type: 'success',
        text1: result.partial ? '部分回收完成' : '回收成功',
        text2: formatRecycleRecentGamesAmountLine(result),
      });

      const refreshed = await getMemberInfo();
      if (apiOk(refreshed)) {
        setMemberInfo((refreshed.data as MineMemberInfo | undefined) ?? null);
      }
    } catch (error) {
      console.error('回收失败:', error);
      Toast.show({ type: 'error', text1: '网络异常', text2: '回收失败，请稍后重试。' });
    } finally {
      setRecycleLoading(false);
    }
  }, [memberInfo, recycleLoading]);

  useEffect(() => {
    if (!isAuthenticated) {
      autoRecycleAttemptedRef.current = false;
      return;
    }

    const memberId = getMemberId(memberInfo);
    const balance = pickNumber(memberInfo?.CreditAmount, memberInfo?.creditAmount);

    if (!memberId) {
      autoRecycleAttemptedRef.current = false;
      return;
    }

    if (balance >= 1) {
      autoRecycleAttemptedRef.current = false;
      return;
    }

    if (recycleLoading || autoRecycleAttemptedRef.current) return;

    autoRecycleAttemptedRef.current = true;
    void handleRecycle();
  }, [handleRecycle, isAuthenticated, memberInfo, recycleLoading]);

  const handleRecharge = useCallback(() => {
    if (rechargeLaunching) return;

    setRechargeLaunching(true);

    setTimeout(() => {
      setTimeout(() => {
        router.push('/deposit');
        setTimeout(() => {
          setRechargeLaunching(false);
        }, 80);
      }, 500);
    }, 260);
  }, [rechargeLaunching, router]);

  const handleSubmitWithdraw = async () => {
    if (!memberInfo) {
      Toast.show({ type: 'error', text1: '账户信息未加载', text2: '请稍后再试。' });
      return;
    }

    const username = pickText(memberInfo.Username, memberInfo.username);
    const usdtAddress = pickText(memberInfo.USDTAddress, memberInfo.usdtAddress);
    const amount =
      withdrawAmount === -1 ? Number(withdrawCustomAmount || 0) : (withdrawAmount ?? 0);

    if (!amount || amount <= 0) {
      Toast.show({ type: 'error', text1: '金额无效', text2: '请输入正确的提现金额。' });
      return;
    }

    if (!withdrawPassword.trim()) {
      Toast.show({ type: 'error', text1: '请输入提现密码' });
      return;
    }

    if (!usdtAddress) {
      Toast.show({
        type: 'error',
        text1: '未绑定地址',
        text2: '请前往"系统设置"维护 USDT 提现地址。',
      });
      return;
    }

    if (!username) {
      Toast.show({
        type: 'error',
        text1: '账号信息异常',
        text2: '当前账号信息获取失败，请重新登录后再试。',
      });
      return;
    }

    setWithdrawSubmitting(true);

    try {
      const result = await playerWithdraw(username, amount, withdrawPassword.trim(), 'USDT提现');
      if (apiOk(result)) {
        Toast.show({
          type: 'success',
          text1: '提交成功',
          text2: result.message || '提现申请已经提交，请等待处理。',
        });
        setWithdrawPassword('');
        setWithdrawCustomAmount('');
        setWithdrawAmount(500);
        setWithdrawExpanded(false);

        const refreshed = await getMemberInfo();
        if (apiOk(refreshed)) {
          setMemberInfo((refreshed.data as MineMemberInfo | undefined) ?? null);
        }
      } else {
        Toast.show({ type: 'error', text1: result.message || '提现提交失败，请稍后再试' });
      }
    } catch {
      Toast.show({ type: 'error', text1: '网络异常，提现提交失败，请稍后重试' });
    } finally {
      setWithdrawSubmitting(false);
    }
  };

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51InnerPage
        title="我的"
        subtitle="账户资产、资金操作与常用服务统一汇总。"
        tag={isAuthenticated ? '已登录' : '未登录'}
        tone="blue"
        hideHero>
        <View className="rounded-[28px] border border-[#4f3a80] bg-[#171d2a] px-4 py-4">
          <View className="flex-row items-center justify-between gap-3">
            <View className="flex-1">
              <Text className="text-[20px] font-black text-white">账户概览</Text>
              <Text className="mt-1 text-[12px] leading-[19px] text-[#97a1b8]">
                核心账户信息与常用操作已集中展示。
              </Text>
            </View>

            <Pressable onPress={() => router.push('/messages')}>
              <Pg51LucideIconBadge icon={Bell} size={44} iconSize={18} radius={18} />
              {unreadCount > 0 ? (
                <View
                  className="absolute right-0 top-0 min-w-[18px] items-center justify-center rounded-full bg-[#FF5FA2] px-1"
                  style={{ height: 18 }}>
                  <Text className="text-[10px] font-bold text-white">
                    {unreadCount > 99 ? '99+' : unreadCount}
                  </Text>
                </View>
              ) : null}
            </Pressable>
          </View>
        </View>

        <ProfileOverviewCard
          isAuthenticated={isAuthenticated}
          memberInfo={memberInfo}
          balanceVisible={balanceVisible}
          loading={loading}
          withdrawExpanded={withdrawExpanded}
          withdrawAmount={withdrawAmount}
          withdrawCustomAmount={withdrawCustomAmount}
          withdrawPassword={withdrawPassword}
          withdrawSubmitting={withdrawSubmitting}
          rechargeLaunching={rechargeLaunching}
          onToggleBalance={() => setBalanceVisible((value) => !value)}
          onLogin={() => openAuthModal('login')}
          onRecharge={handleRecharge}
          onRecycle={() => {
            void handleRecycle();
          }}
          recycleLoading={recycleLoading}
          onWithdrawToggle={() => setWithdrawExpanded((value) => !value)}
          onWithdrawAmountChange={setWithdrawAmount}
          onWithdrawCustomAmountChange={(value) => {
            setWithdrawAmount(-1);
            setWithdrawCustomAmount(value.replace(/[^\d.]/g, ''));
          }}
          onWithdrawPasswordChange={setWithdrawPassword}
          onWithdrawSubmit={handleSubmitWithdraw}
        />

        <MenuSection
          title="系统菜单"
          items={SECURITY_MENU}
          onPress={(item) => router.push(item.path as any)}
        />

        <MenuSection
          title="其他"
          items={OTHER_MENU.map((item) =>
            item.title === '消息通知' && unreadCount > 0
              ? { ...item, badge: unreadCount > 99 ? '99+' : String(unreadCount) }
              : item
          )}
          onPress={(item) => router.push(item.path as any)}
        />

        {isAuthenticated ? (
          <Pg51SectionCard title="账户操作" description="">
            <Pressable
              onPress={handleLogout}
              className="flex-row items-center justify-center gap-2 rounded-[20px] bg-[#3a1f29] px-4 py-3.5">
              <Icon as={LogOut} size={18} color="#ff9ab3" />
              <Text className="text-[14px] font-bold text-[#ff9ab3]">退出登录</Text>
            </Pressable>
          </Pg51SectionCard>
        ) : null}
      </Pg51InnerPage>
    </>
  );
}
