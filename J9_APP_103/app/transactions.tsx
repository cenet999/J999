import AsyncStorage from '@react-native-async-storage/async-storage';
import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Pg51InnerPageTopBar } from '@/components/pg51-clone/inner-page-top-bar';
import { Pg51InnerPage, Pg51SectionCard } from '@/components/pg51-clone/page-ui';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import { getMemberInfo } from '@/lib/api/auth';
import { formatRecycleRecentGamesAmountLine, recycleRecentGames } from '@/lib/api/game';
import { apiOk } from '@/lib/api/request';
import {
  getTransactionList,
  getTransactionMonthSummary,
  syncBetHistoryToDatabase,
  type TransactionMonthSummary,
  type TransactionRecord,
  type TransactionSyncResult,
} from '@/lib/api/transaction';
import * as Clipboard from 'expo-clipboard';
import { Stack, useRouter } from 'expo-router';
import {
  ArrowDownLeft,
  ArrowUpRight,
  Ban,
  CheckCircle2,
  Clock,
  Gamepad2,
  Gift,
  HelpCircle,
  Percent,
  Recycle,
  Receipt,
  RefreshCw,
  Repeat2,
  TrendingDown,
  TrendingUp,
  Undo2,
  Wallet,
  XCircle,
  type LucideIcon,
} from 'lucide-react-native';
import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, View } from 'react-native';
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

const TRANSACTION_PAGE_SIZE = 10;
const TRANSACTION_SYNC_COOLDOWN_KEY = '@j9_transaction_sync_cooldown';
const TRANSACTION_SYNC_COOLDOWN_MS = 10 * 60 * 1000;

/** 业务展示统一为北京时间（与服务器/运营口径一致，不随手机系统时区变化） */
const TIMEZONE_BEIJING = 'Asia/Shanghai';

function hasExplicitTimeZone(s: string): boolean {
  return /Z$/i.test(s) || /[+-]\d{2}:\d{2}$/.test(s) || /[+-]\d{4}$/.test(s);
}

/**
 * 解析为 UTC 时刻毫秒：Unix 秒/毫秒；带 Z 或 ±offset 的 ISO 串按标准解析；
 * 无时区信息的日期时间串按「北京时间墙钟」理解（补 +08:00）。
 */
function parseTransactionInstantMs(value: unknown): number | null {
  if (value == null) return null;
  if (typeof value === 'number' && Number.isFinite(value)) {
    const ms = value < 1e12 ? value * 1000 : value;
    return Number.isFinite(ms) ? ms : null;
  }
  if (typeof value === 'string') {
    const t = value.trim();
    if (!t) return null;
    if (/^\d+$/.test(t)) {
      const n = Number(t);
      if (!Number.isFinite(n)) return null;
      return parseTransactionInstantMs(n);
    }
    const normalized = t.includes('T') ? t : t.replace(' ', 'T');
    if (hasExplicitTimeZone(t)) {
      const n = Date.parse(normalized);
      return Number.isNaN(n) ? null : n;
    }
    const n = Date.parse(`${normalized}+08:00`);
    return Number.isNaN(n) ? null : n;
  }
  return null;
}

function beijingDateTimeParts(ms: number): {
  yyyy: number;
  mm: number;
  dd: number;
  HH: string;
  MI: string;
  SS: string;
} {
  const dtf = new Intl.DateTimeFormat('en-CA', {
    timeZone: TIMEZONE_BEIJING,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  });
  const parts = dtf.formatToParts(new Date(ms));
  const map: Record<string, string> = {};
  for (const p of parts) {
    if (p.type !== 'literal') map[p.type] = p.value;
  }
  return {
    yyyy: Number(map.year),
    mm: Number(map.month),
    dd: Number(map.day),
    HH: map.hour ?? '00',
    MI: map.minute ?? '00',
    SS: map.second ?? '00',
  };
}

function beijingYmd(ms: number): string {
  const p = beijingDateTimeParts(ms);
  return `${p.yyyy}-${String(p.mm).padStart(2, '0')}-${String(p.dd).padStart(2, '0')}`;
}

function formatFullDateBeijing(ms: number): string {
  const p = beijingDateTimeParts(ms);
  return `${p.yyyy}年${p.mm}月${p.dd}日 ${p.HH}:${p.MI}:${p.SS}`;
}

const TYPE_FILTER_TABS = [
  { key: 'all', label: '全部', type: undefined as number | undefined },
  { key: 'recharge', label: '充值', type: 4 },
  { key: 'withdraw', label: '提现', type: 3 },
  { key: 'transfer', label: '转账', type: 1 },
  { key: 'game', label: '游戏', type: 2 },
];

const STATUS_FILTER_TABS = [
  { key: 'all', label: '全部', status: undefined as number | undefined },
  { key: 'success', label: '成功', status: 0 },
  { key: 'failed', label: '失败', status: 1 },
  { key: 'processing', label: '处理中', status: 2 },
  { key: 'pending', label: '待处理', status: 3 },
  { key: 'cancelled', label: '已取消', status: 4 },
  { key: 'refunded', label: '已退款', status: 5 },
];

const TX_TYPE_NAME_TO_VALUE: Record<string, number> = {
  TransferIn: 0,
  TransferOut: 1,
  Bet: 2,
  Withdraw: 3,
  Recharge: 4,
  Refund: 5,
  Commission: 6,
  Activity: 7,
  Other: 8,
  AgentTransferIn: 9,
  AgentTransferOut: 10,
  Rebate: 11,
  AgentRecharge: 12,
  Login: 13,
  CheckIn: 14,
  Register: 15,
};

const TX_STATUS_NAME_TO_VALUE: Record<string, number> = {
  Success: 0,
  Failed: 1,
  Processing: 2,
  Pending: 3,
  Cancelled: 4,
  Refunded: 5,
};

const TX_TYPE_MAP: Record<number, { label: string; icon: LucideIcon; color: string; bg: string }> =
  {
    0: { label: '上分', icon: ArrowUpRight, color: '#FF8A34', bg: '#35261d' },
    1: { label: '下分', icon: ArrowDownLeft, color: '#35D07F', bg: '#1b3128' },
    2: { label: '游戏投注', icon: Gamepad2, color: '#7B5CFF', bg: '#241d39' },
    3: { label: '提现', icon: ArrowUpRight, color: '#FF8A34', bg: '#35261d' },
    4: { label: '充值到账', icon: ArrowDownLeft, color: '#35D07F', bg: '#1b3128' },
    5: { label: '退款', icon: Undo2, color: '#B794F4', bg: '#2b2240' },
    6: { label: '返佣', icon: TrendingUp, color: '#35D07F', bg: '#1b3128' },
    7: { label: '活动', icon: Gift, color: '#FF5FA2', bg: '#3a1f29' },
    8: { label: '其他', icon: Repeat2, color: '#9da7bd', bg: '#2b3345' },
    9: { label: '代理上分', icon: ArrowUpRight, color: '#FF8A34', bg: '#35261d' },
    10: { label: '代理下分', icon: ArrowDownLeft, color: '#35D07F', bg: '#1b3128' },
    11: { label: '反水', icon: Percent, color: '#7B5CFF', bg: '#241d39' },
    12: { label: '代理充值', icon: ArrowDownLeft, color: '#35D07F', bg: '#1b3128' },
    13: { label: '登录奖励', icon: Gift, color: '#FF5FA2', bg: '#3a1f29' },
    14: { label: '签到奖励', icon: Gift, color: '#FFD84D', bg: '#383119' },
    15: { label: '注册奖励', icon: Gift, color: '#35D07F', bg: '#1b3128' },
  };

const DEFAULT_TX_META = { label: '其他', icon: Repeat2, color: '#9da7bd', bg: '#2b3345' };

const TX_STATUS_MAP: Record<
  number,
  { label: string; color: string; bg: string; icon: LucideIcon }
> = {
  0: { label: '成功', color: '#35D07F', bg: '#1b3128', icon: CheckCircle2 },
  1: { label: '失败', color: '#FF7E93', bg: '#3a1f29', icon: XCircle },
  2: { label: '处理中', color: '#FFD84D', bg: '#383119', icon: RefreshCw },
  3: { label: '待处理', color: '#4ea3ff', bg: '#172535', icon: Clock },
  4: { label: '已取消', color: '#9da7bd', bg: '#2b3345', icon: Ban },
  5: { label: '已退款', color: '#B794F4', bg: '#2b2240', icon: Undo2 },
};

const DEFAULT_TX_STATUS = {
  label: '未知',
  color: '#9da7bd',
  bg: '#2b3345',
  icon: HelpCircle,
};

const TRANSFER_OUT_TYPES = [1, 4, 10, 12];
const TRANSFER_IN_TYPES = [0, 3, 9];

function pickAmount(value: unknown): number {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string') {
    const num = Number(value);
    if (Number.isFinite(num)) return num;
  }
  return 0;
}

function pickText(value: unknown): string {
  if (value == null) return '';
  return String(value).trim();
}

/** 交易时间：支持 Unix 秒/毫秒与日期串；输出为北京时间下的 `yyyy-MM-dd HH:mm:ss`（用于分组键等） */
function pickTransactionTimeText(value: unknown): string {
  const ms = parseTransactionInstantMs(value);
  if (ms == null) return pickText(value);
  const p = beijingDateTimeParts(ms);
  return `${p.yyyy}-${String(p.mm).padStart(2, '0')}-${String(p.dd).padStart(2, '0')} ${p.HH}:${p.MI}:${p.SS}`;
}

function pickBool(value: unknown): boolean {
  return value === true || value === 1 || value === '1' || value === 'true';
}

async function copyTransactionMetaRow(label: string, value: string) {
  const text = value.trim();
  if (!text || text === '—') {
    Toast.show({
      type: 'info',
      text1: '暂无可复制',
      text2: `${label}为空`,
    });
    return;
  }
  try {
    await Clipboard.setStringAsync(text);
    Toast.show({
      type: 'success',
      text1: '复制成功',
      text2: `${label}已写入剪贴板`,
    });
  } catch {
    Toast.show({ type: 'error', text1: '复制失败', text2: '请稍后重试' });
  }
}

function getTransactionTypeValue(record: TransactionRecord): number {
  const obj = record as unknown as Record<string, unknown>;
  const value = obj.transactionType ?? obj.TransactionType;

  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string') {
    if (/^\d+$/.test(value)) return Number(value);
    return TX_TYPE_NAME_TO_VALUE[value] ?? 8;
  }

  return 8;
}

function getTransactionStatusValue(record: TransactionRecord): number {
  const obj = record as unknown as Record<string, unknown>;
  const value = obj.status ?? obj.Status;

  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string') {
    if (/^\d+$/.test(value)) return Number(value);
    return TX_STATUS_NAME_TO_VALUE[value] ?? 0;
  }

  return 0;
}

function getTxMeta(type: number) {
  return TX_TYPE_MAP[type] ?? DEFAULT_TX_META;
}

function getTxStatusMeta(status: number) {
  return TX_STATUS_MAP[status] ?? DEFAULT_TX_STATUS;
}

function formatMoney(amount: number, currency = 'CNY') {
  const text = Math.abs(amount).toFixed(2);
  return currency.toUpperCase() === 'USDT' ? `${text} USDT` : `¥${text}`;
}

function formatSignedMoney(amount: number, currency = 'CNY') {
  const sign = amount > 0 ? '+' : amount < 0 ? '-' : '';
  return `${sign}${formatMoney(amount, currency)}`;
}

function formatFullDate(dateStr: string) {
  if (!dateStr) return '—';
  const ms = parseTransactionInstantMs(dateStr);
  if (ms == null) return dateStr;
  return formatFullDateBeijing(ms);
}

function getDateKey(dateStr: string) {
  const ms = parseTransactionInstantMs(dateStr);
  if (ms == null) return dateStr || 'unknown';
  return beijingYmd(ms);
}

function formatDateGroup(dateStr: string) {
  const ms = parseTransactionInstantMs(dateStr);
  if (ms == null) return dateStr || '未知日期';

  const todayKey = beijingYmd(Date.now());
  const targetKey = beijingYmd(ms);
  const p = beijingDateTimeParts(ms);
  const month = p.mm;
  const day = p.dd;
  const year = p.yyyy;

  if (targetKey === todayKey) return `今天  ${month}月${day}日`;

  const parseYmd = (s: string) => {
    const [y, m, d] = s.split('-').map(Number);
    return { y, m, d };
  };
  const a = parseYmd(todayKey);
  const b = parseYmd(targetKey);
  const diff = Math.round(
    (Date.UTC(a.y, a.m - 1, a.d) - Date.UTC(b.y, b.m - 1, b.d)) / (1000 * 60 * 60 * 24)
  );
  if (diff === 1) return `昨天  ${month}月${day}日`;

  const thisYear = beijingDateTimeParts(Date.now()).yyyy;
  if (year !== thisYear) return `${year}年${month}月${day}日`;
  return `${month}月${day}日`;
}

function getAmountMeta(record: TransactionRecord, typeValue: number, statusValue: number) {
  const obj = record as unknown as Record<string, unknown>;
  const currency = pickText(obj.currencyCode ?? obj.CurrencyCode) || 'CNY';
  const actualAmount = pickAmount(obj.actualAmount ?? obj.ActualAmount);

  if (TRANSFER_IN_TYPES.includes(typeValue)) {
    return {
      text: formatSignedMoney(-Math.abs(actualAmount), currency),
      color: statusValue === 1 || statusValue === 4 ? '#9da7bd' : '#FF8A34',
    };
  }

  if (TRANSFER_OUT_TYPES.includes(typeValue)) {
    return {
      text: formatSignedMoney(Math.abs(actualAmount), currency),
      color: '#35D07F',
    };
  }

  return {
    text: formatSignedMoney(actualAmount, currency),
    color: actualAmount >= 0 ? '#35D07F' : '#FF8A34',
  };
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
      <Icon as={icon} size={13} color="#DBEAFE" />
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
        backgroundColor: '#172535',
        borderWidth: 1,
        borderColor: '#2F4E73',
        opacity: onPress ? (loading || launching ? 0.9 : 1) : 0.8,
      }}>
      <QuickActionIcon icon={icon} spinning={loading} toggled={toggled} launching={launching} />
      <Text className="text-[11px] font-bold text-[#DBEAFE]">{label}</Text>
    </Pressable>
  );
}

function getSyncSummary(result?: TransactionSyncResult) {
  if (!result) return '投注记录已同步完成。';
  return `拉取 ${result.remoteFetched ?? 0} 条，新增 ${result.inserted ?? 0}，更新 ${result.updated ?? 0}，无单号跳过 ${result.skippedNoSerial ?? 0}。`;
}

function formatCooldownRemaining(ms: number) {
  const totalSeconds = Math.max(1, Math.ceil(ms / 1000));
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;

  if (minutes <= 0) return `${seconds}秒`;
  if (seconds === 0) return `${minutes}分钟`;
  return `${minutes}分${seconds}秒`;
}

async function getSyncCooldownRemainingMs() {
  try {
    const raw = await AsyncStorage.getItem(TRANSACTION_SYNC_COOLDOWN_KEY);
    if (!raw) return 0;

    const lastSyncedAt = Number(raw);
    if (!Number.isFinite(lastSyncedAt) || lastSyncedAt <= 0) {
      await AsyncStorage.removeItem(TRANSACTION_SYNC_COOLDOWN_KEY);
      return 0;
    }

    return Math.max(0, TRANSACTION_SYNC_COOLDOWN_MS - (Date.now() - lastSyncedAt));
  } catch {
    return 0;
  }
}

async function markSyncCooldownNow() {
  try {
    await AsyncStorage.setItem(TRANSACTION_SYNC_COOLDOWN_KEY, String(Date.now()));
  } catch {
    // ignore cache write failures
  }
}

export default function TransactionsScreen() {
  const router = useRouter();
  const [activeTypeFilter, setActiveTypeFilter] = useState('all');
  const [activeStatusFilter, setActiveStatusFilter] = useState('all');
  const [records, setRecords] = useState<TransactionRecord[]>([]);
  const [monthSummary, setMonthSummary] = useState<TransactionMonthSummary>({
    income: 0,
    expense: 0,
  });
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [nextPage, setNextPage] = useState(1);
  const [hasMore, setHasMore] = useState(true);
  const [recycleLoading, setRecycleLoading] = useState(false);
  const [syncLoading, setSyncLoading] = useState(false);

  const selectedType = TYPE_FILTER_TABS.find((item) => item.key === activeTypeFilter);
  const selectedStatus = STATUS_FILTER_TABS.find((item) => item.key === activeStatusFilter);

  async function loadFirstPage() {
    setLoading(true);
    setLoadingMore(false);

    try {
      const [listResult, summaryResult] = await Promise.all([
        getTransactionList(1, TRANSACTION_PAGE_SIZE, selectedType?.type, selectedStatus?.status),
        getTransactionMonthSummary(),
      ]);

      const list = listResult.success && listResult.data ? listResult.data : [];
      setRecords(list);
      setNextPage(2);
      setHasMore(list.length >= TRANSACTION_PAGE_SIZE);

      if (summaryResult.success && summaryResult.data) {
        setMonthSummary(summaryResult.data);
      } else {
        setMonthSummary({ income: 0, expense: 0 });
      }
    } catch {
      setRecords([]);
      setMonthSummary({ income: 0, expense: 0 });
      setNextPage(1);
      setHasMore(false);
    } finally {
      setLoading(false);
    }
  }

  async function loadMore() {
    if (loading || loadingMore || !hasMore) return;

    setLoadingMore(true);
    try {
      const result = await getTransactionList(
        nextPage,
        TRANSACTION_PAGE_SIZE,
        selectedType?.type,
        selectedStatus?.status
      );
      const list = result.success && result.data ? result.data : [];
      setRecords((current) => [...current, ...list]);
      setNextPage((current) => current + 1);
      setHasMore(list.length >= TRANSACTION_PAGE_SIZE);
    } catch {
      setHasMore(false);
    } finally {
      setLoadingMore(false);
    }
  }

  async function handleRecycle() {
    if (recycleLoading) return;

    setRecycleLoading(true);
    try {
      const memberResult = await getMemberInfo();
      if (!apiOk(memberResult) || !memberResult.data) {
        Toast.show({
          type: 'error',
          text1: '回收失败',
          text2: memberResult.message || '请先登录后再试。',
        });
        return;
      }

      const memberInfo = memberResult.data as Record<string, unknown>;
      const memberId = pickText(memberInfo.Id ?? memberInfo.id);
      if (!memberId) {
        Toast.show({ type: 'error', text1: '回收失败', text2: '未获取到会员信息，请稍后再试。' });
        return;
      }

      const result = await recycleRecentGames(memberId);
      if (!result.ok) {
        Toast.show({ type: 'error', text1: '回收失败', text2: result.message || '请稍后再试。' });
        return;
      }

      await loadFirstPage();
      Toast.show({
        type: 'success',
        text1: result.partial ? '部分回收完成' : '回收成功',
        text2: formatRecycleRecentGamesAmountLine(result),
      });
    } catch (error) {
      console.error('回收失败:', error);
      Toast.show({ type: 'error', text1: '网络异常', text2: '回收失败，请稍后重试。' });
    } finally {
      setRecycleLoading(false);
    }
  }

  async function handleSyncRecords() {
    if (syncLoading) return;

    const remainingMs = await getSyncCooldownRemainingMs();
    if (remainingMs > 0) {
      Toast.show({
        type: 'info',
        text1: '请稍后再试',
        text2: `${formatCooldownRemaining(remainingMs)}后才能再次同步记录。`,
      });
      return;
    }

    setSyncLoading(true);
    try {
      const result = await syncBetHistoryToDatabase();
      if (!apiOk(result)) {
        Toast.show({ type: 'error', text1: '同步失败', text2: result.message || '请稍后再试。' });
        return;
      }

      await markSyncCooldownNow();
      await loadFirstPage();
      Toast.show({
        type: 'success',
        text1: '同步完成',
        text2: result.message || getSyncSummary(result.data),
      });
    } catch (error) {
      console.error('同步投注记录失败:', error);
      Toast.show({ type: 'error', text1: '网络异常', text2: '同步失败，请稍后重试。' });
    } finally {
      setSyncLoading(false);
    }
  }

  useEffect(() => {
    void loadFirstPage();
  }, [activeTypeFilter, activeStatusFilter]);

  const groupedRecords = useMemo(() => {
    const groups: Record<
      string,
      { dateKey: string; label: string; items: TransactionRecord[] }
    > = {};

    for (const record of records) {
      const obj = record as unknown as Record<string, unknown>;
      const time = pickTransactionTimeText(
        obj.transactionTime ?? obj.TransactionTime ?? obj.createdTime ?? obj.CreatedTime
      );
      const key = getDateKey(time);

      if (!groups[key]) {
        groups[key] = {
          dateKey: key,
          label: formatDateGroup(time),
          items: [],
        };
      }

      groups[key].items.push(record);
    }

    return Object.entries(groups)
      .sort(([a], [b]) => b.localeCompare(a))
      .map(([, value]) => value);
  }, [records]);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51InnerPage
        title="交易明细"
        subtitle="查看充值、提现及账户资金流水。"
        tag="资金流水"
        tone="blue"
        hideHero>
        <Pg51InnerPageTopBar
          onBack={() => router.back()}
          icon={Receipt}
          iconColor="#4ea3ff"
          title="交易明细"
          subtitle="查看充值、提现和资金流水"
          tone="blue"
        />

        <Pg51SectionCard
          title="筛选与概览"
          description="按交易类型与状态筛选，便于快速查看相关记录。">
          <View className="gap-3">
            <FilterBlock
              title="交易类型"
              tabs={TYPE_FILTER_TABS}
              activeKey={activeTypeFilter}
              onChange={setActiveTypeFilter}
            />
            <FilterBlock
              title="交易状态"
              tabs={STATUS_FILTER_TABS}
              activeKey={activeStatusFilter}
              onChange={setActiveStatusFilter}
            />
          </View>

          <View className="flex-row gap-3">
            <SummaryCard
              title="本月充值"
              value={`+${formatMoney(monthSummary.income)}`}
              hint="只算成功充值"
              icon={TrendingUp}
              color="#35D07F"
              bg="#1b3128"
            />
            <SummaryCard
              title="本月提现"
              value={`-${formatMoney(monthSummary.expense)}`}
              hint="只算成功提现"
              icon={TrendingDown}
              color="#FF8A34"
              bg="#35261d"
            />
          </View>
        </Pg51SectionCard>

        <Pg51SectionCard
          title={`交易记录（${records.length}笔）`}
          right={
            <View className="flex-row flex-wrap justify-end gap-2">
              <QuickActionButton
                icon={Recycle}
                label={recycleLoading ? '回收中' : '回收'}
                onPress={() => {
                  void handleRecycle();
                }}
                loading={recycleLoading}
              />
              <QuickActionButton
                icon={RefreshCw}
                label={syncLoading ? '同步中' : '同步记录'}
                onPress={() => {
                  void handleSyncRecords();
                }}
                loading={syncLoading}
              />
            </View>
          }>
          {loading ? (
            <View className="items-center py-8">
              <ActivityIndicator size="large" color="#4ea3ff" />
              <Text className="mt-2 text-[12px] text-[#97a1b8]">加载中...</Text>
            </View>
          ) : records.length === 0 ? (
            <View className="items-center rounded-[22px] bg-[#212838] px-4 py-8">
              <Icon as={Wallet} size={22} color="#97a1b8" />
              <Text className="mt-3 text-[14px] font-semibold text-white">暂无交易记录</Text>
              <Text className="mt-1 text-[12px] text-[#97a1b8]">当前筛选条件下暂无相关记录。</Text>
            </View>
          ) : (
            <View className="gap-3">
              {groupedRecords.map((group) => (
                <View key={group.dateKey} className="gap-2.5">
                  <Text className="px-1 pt-2 text-[12px] font-bold text-[#97a1b8]">
                    {group.label}
                  </Text>
                  {group.items.map((record, index) => {
                    const obj = record as unknown as Record<string, unknown>;
                    const key = pickText(obj.id ?? obj.Id) || `${group.dateKey}-${index}`;

                    return <TransactionCard key={key} record={record} />;
                  })}
                </View>
              ))}

              {hasMore ? (
                <Pressable
                  onPress={() => {
                    void loadMore();
                  }}
                  disabled={loadingMore}
                  className="items-center justify-center rounded-[20px] bg-[#212838] px-4 py-3.5"
                  style={{ opacity: loadingMore ? 0.7 : 1 }}>
                  {loadingMore ? (
                    <ActivityIndicator color="#4ea3ff" />
                  ) : (
                    <Text className="text-[13px] font-bold text-[#dbe3f4]">加载更多</Text>
                  )}
                </Pressable>
              ) : (
                <Text className="py-2 text-center text-[11px] text-[#97a1b8]">已经到底了</Text>
              )}
            </View>
          )}
        </Pg51SectionCard>
      </Pg51InnerPage>
    </>
  );
}

function FilterBlock({
  title,
  tabs,
  activeKey,
  onChange,
}: {
  title: string;
  tabs: Array<{ key: string; label: string }>;
  activeKey: string;
  onChange: (key: string) => void;
}) {
  return (
    <View className="gap-2">
      <Text className="text-[12px] font-bold text-[#9fa8be]">{title}</Text>
      <View className="flex-row flex-wrap gap-2">
        {tabs.map((tab) => {
          const active = activeKey === tab.key;

          return (
            <Pressable
              key={tab.key}
              onPress={() => onChange(tab.key)}
              className="rounded-full px-3 py-1.5"
              style={{ backgroundColor: active ? '#2563eb' : '#212838' }}>
              <Text
                className="text-[11px] font-bold"
                style={{ color: active ? '#ffffff' : '#9fa8be' }}>
                {tab.label}
              </Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

function SummaryCard({
  title,
  value,
  hint,
  icon,
  color,
  bg,
}: {
  title: string;
  value: string;
  hint: string;
  icon: LucideIcon;
  color: string;
  bg: string;
}) {
  return (
    <View className="flex-1 rounded-[22px] px-4 py-4" style={{ backgroundColor: bg }}>
      <View className="flex-row items-center gap-2">
        <Icon as={icon} size={15} color={color} />
        <Text className="text-[12px] font-semibold text-[#dbe3f4]">{title}</Text>
      </View>
      <Text className="mt-3 text-[20px] font-black" style={{ color }}>
        {value}
      </Text>
      <Text className="mt-1 text-[11px] text-[#97a1b8]">{hint}</Text>
    </View>
  );
}

function DetailTile({ label, value }: { label: string; value: string }) {
  return (
    <View className="min-w-0 flex-1 items-center gap-1 px-1 py-2.5">
      <Text className="text-center text-[10px] text-[#97a1b8]" numberOfLines={2}>
        {label}
      </Text>
      <Text
        className="text-center text-[11px] font-bold text-white"
        numberOfLines={1}
        adjustsFontSizeToFit
        minimumFontScale={0.75}>
        {value}
      </Text>
    </View>
  );
}

function MetaRow({
  label,
  value,
  valueLines = 1,
}: {
  label: string;
  value: string;
  valueLines?: number;
}) {
  return (
    <Pressable
      accessibilityLabel={`复制${label}`}
      accessibilityRole="button"
      onPress={() => {
        void copyTransactionMetaRow(label, value);
      }}
      className="w-full flex-row items-start gap-3 py-1"
      style={({ pressed }) => ({ opacity: pressed ? 0.72 : 1 })}>
      <Text className="w-[68px] shrink-0 text-[10px] font-bold text-[#97a1b8]">{label}</Text>
      <View className="min-w-0 flex-1">
        <Text
          className="text-right text-[10px] font-semibold leading-[16px] text-[#dbe3f4]"
          numberOfLines={valueLines}
          ellipsizeMode="tail">
          {value}
        </Text>
      </View>
    </Pressable>
  );
}

function TransactionCard({ record }: { record: TransactionRecord }) {
  const obj = record as unknown as Record<string, unknown>;
  const typeValue = getTransactionTypeValue(record);
  const statusValue = getTransactionStatusValue(record);
  const typeMeta = getTxMeta(typeValue);
  const statusMeta = getTxStatusMeta(statusValue);
  const amountMeta = getAmountMeta(record, typeValue, statusValue);

  const beforeAmount = pickAmount(obj.beforeAmount ?? obj.BeforeAmount);
  const afterAmount = pickAmount(obj.afterAmount ?? obj.AfterAmount);
  const betAmount = pickAmount(obj.betAmount ?? obj.BetAmount);
  const actualAmount = pickAmount(obj.actualAmount ?? obj.ActualAmount);
  const currency = pickText(obj.currencyCode ?? obj.CurrencyCode) || 'CNY';
  const serialNumber = pickText(obj.serialNumber ?? obj.SerialNumber);
  const gameRound = pickText(obj.gameRound ?? obj.GameRound);
  const transactionTime = pickTransactionTimeText(
    obj.transactionTime ?? obj.TransactionTime ?? obj.createdTime ?? obj.CreatedTime
  );
  const createdTime = pickText(obj.createdTime ?? obj.CreatedTime);
  const modifiedTime = pickText(obj.modifiedTime ?? obj.ModifiedTime);
  const transactionId = pickText(obj.id ?? obj.Id) || '—';
  const isRebate = pickBool(obj.isRebate ?? obj.IsRebate);

  return (
    <View className="gap-3 rounded-[22px] border border-[#33405A] bg-[#212838] p-4">
      <View className="flex-row items-start gap-3">
        <Pg51LucideIconBadge icon={typeMeta.icon} size={44} iconSize={20} radius={16} />

        <View className="flex-1 gap-1">
          <View className="flex-row flex-wrap items-center gap-2">
            <Text className="text-[15px] font-black text-white">{typeMeta.label}</Text>
            <View
              className="flex-row items-center gap-1 rounded-full px-2 py-0.5"
              style={{ backgroundColor: statusMeta.bg }}>
              <Icon as={statusMeta.icon} size={11} color={statusMeta.color} />
              <Text className="text-[10px] font-bold" style={{ color: statusMeta.color }}>
                {statusMeta.label}
              </Text>
            </View>
            {isRebate ? (
              <View className="rounded-full bg-[#241d39] px-2 py-0.5">
                <Text className="text-[10px] font-bold text-[#b794f4]">反水</Text>
              </View>
            ) : null}
          </View>
          <Text className="text-[12px] text-[#97a1b8]">{formatFullDate(transactionTime)}</Text>
        </View>

        <Text className="text-[16px] font-black" style={{ color: amountMeta.color }}>
          {amountMeta.text}
        </Text>
      </View>

      <View className="flex-row rounded-[18px] bg-[#171d2a]">
        <DetailTile label="交易前余额" value={formatMoney(beforeAmount, currency)} />
        <DetailTile label="交易后余额" value={formatMoney(afterAmount, currency)} />
        <DetailTile label="投注金额" value={formatMoney(betAmount, currency)} />
        <DetailTile label="实动金额" value={formatSignedMoney(actualAmount, currency)} />
      </View>

      <View className="gap-0.5">
        <MetaRow label="流水 ID" value={transactionId} />
        <MetaRow label="业务单号" value={serialNumber || '—'} />
        {gameRound ? <MetaRow label="游戏局号" value={gameRound} /> : null}
        {createdTime && createdTime !== transactionTime ? (
          <MetaRow label="创建时间" value={formatFullDate(createdTime)} />
        ) : null}
        {modifiedTime ? <MetaRow label="修改时间" value={formatFullDate(modifiedTime)} /> : null}
      </View>
    </View>
  );
}
