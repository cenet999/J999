import { Pg51InnerPage, Pg51SectionCard } from '@/components/pg51-clone/page-ui';
import { Icon } from '@/components/ui/icon';
import { Skeleton, SkeletonCircle } from '@/components/ui/skeleton';
import { Toast } from '@/components/ui/toast';
import { Text } from '@/components/ui/text';
import { playerCheckIn } from '@/lib/api/auth';
import {
  claimActivityChest,
  claimDailyTask,
  getCheckInStatus,
  getDailyTasks,
  type CheckInStatus,
  type DailyTask,
} from '@/lib/api/event';
import { apiOk } from '@/lib/api/request';
import { isInstalledAppRuntime } from '@/lib/platform-mode';
import { Stack, useFocusEffect, useRouter } from 'expo-router';
import { Check } from 'lucide-react-native';
import type { ComponentType, ReactNode } from 'react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import Svg, { Path } from 'react-native-svg';

const TASK_MILESTONES = [
  { target: 20, reward: 2 },
  { target: 30, reward: 5 },
  { target: 45, reward: 12 },
  { target: 68, reward: 28 },
  { target: 102, reward: 62 },
  { target: 153, reward: 135 },
] as const;

const APP_ONLY_CHEST_TARGETS = new Set([45, 68, 102, 153]);
const CHEST_DOWNLOAD_REDIRECT_DELAY_MS = 2500;

type TaskIconProps = {
  size?: number;
  color?: string;
};

type TaskIconComponent = ComponentType<TaskIconProps>;

type FontAwesomeIconSpec = {
  width: number;
  height: number;
  path: string;
};

const FONT_AWESOME_ICONS = {
  rightToBracket: {
    width: 512,
    height: 512,
    path: 'M345 273c9.4-9.4 9.4-24.6 0-33.9L201 95c-6.9-6.9-17.2-8.9-26.2-5.2S160 102.3 160 112l0 80-112 0c-26.5 0-48 21.5-48 48l0 32c0 26.5 21.5 48 48 48l112 0 0 80c0 9.7 5.8 18.5 14.8 22.2s19.3 1.7 26.2-5.2L345 273zm7 143c-17.7 0-32 14.3-32 32s14.3 32 32 32l64 0c53 0 96-43 96-96l0-256c0-53-43-96-96-96l-64 0c-17.7 0-32 14.3-32 32s14.3 32 32 32l64 0c17.7 0 32 14.3 32 32l0 256c0 17.7-14.3 32-32 32l-64 0z',
  },
  calendarCheck: {
    width: 448,
    height: 512,
    path: 'M320 0c17.7 0 32 14.3 32 32l0 32 32 0c35.3 0 64 28.7 64 64l0 288c0 35.3-28.7 64-64 64L64 480c-35.3 0-64-28.7-64-64L0 128C0 92.7 28.7 64 64 64l32 0 0-32c0-17.7 14.3-32 32-32s32 14.3 32 32l0 32 128 0 0-32c0-17.7 14.3-32 32-32zm22 161.7c-10.7-7.8-25.7-5.4-33.5 5.3L189.1 331.2 137 279.1c-9.4-9.4-24.6-9.4-33.9 0s-9.4 24.6 0 33.9l72 72c5 5 11.9 7.5 18.8 7s13.4-4.1 17.5-9.8L347.3 195.2c7.8-10.7 5.4-25.7-5.3-33.5z',
  },
  gamepad: {
    width: 640,
    height: 512,
    path: 'M448 64c106 0 192 86 192 192S554 448 448 448l-256 0C86 448 0 362 0 256S86 64 192 64l256 0zM192 176c-13.3 0-24 10.7-24 24l0 32-32 0c-13.3 0-24 10.7-24 24s10.7 24 24 24l32 0 0 32c0 13.3 10.7 24 24 24s24-10.7 24-24l0-32 32 0c13.3 0 24-10.7 24-24s-10.7-24-24-24l-32 0 0-32c0-13.3-10.7-24-24-24zm240 96a32 32 0 1 0 0 64 32 32 0 1 0 0-64zm64-96a32 32 0 1 0 0 64 32 32 0 1 0 0-64z',
  },
  gift: {
    width: 512,
    height: 512,
    path: 'M321.5 68.8C329.1 55.9 342.9 48 357.8 48l2.2 0c22.1 0 40 17.9 40 40s-17.9 40-40 40l-73.3 0 34.8-59.2zm-131 0l34.8 59.2-73.3 0c-22.1 0-40-17.9-40-40s17.9-40 40-40l2.2 0c14.9 0 28.8 7.9 36.3 20.8zm89.6-24.3l-24.1 41-24.1-41C215.7 16.9 186.1 0 154.2 0L152 0c-48.6 0-88 39.4-88 88 0 14.4 3.5 28 9.6 40L32 128c-17.7 0-32 14.3-32 32l0 32c0 17.7 14.3 32 32 32l448 0c17.7 0 32-14.3 32-32l0-32c0-17.7-14.3-32-32-32l-41.6 0c6.1-12 9.6-25.6 9.6-40 0-48.6-39.4-88-88-88l-2.2 0c-31.9 0-61.5 16.9-77.7 44.4zM480 272l-200 0 0 208 136 0c35.3 0 64-28.7 64-64l0-144zm-248 0l-200 0 0 144c0 35.3 28.7 64 64 64l136 0 0-208z',
  },
  userPlus: {
    width: 640,
    height: 512,
    path: 'M285.7 304c98.5 0 178.3 79.8 178.3 178.3 0 16.4-13.3 29.7-29.7 29.7L77.7 512C61.3 512 48 498.7 48 482.3 48 383.8 127.8 304 226.3 304l59.4 0zM528 80c13.3 0 24 10.7 24 24l0 48 48 0c13.3 0 24 10.7 24 24s-10.7 24-24 24l-48 0 0 48c0 13.3-10.7 24-24 24s-24-10.7-24-24l0-48-48 0c-13.3 0-24-10.7-24-24s10.7-24 24-24l48 0 0-48c0-13.3 10.7-24 24-24zM256 248a120 120 0 1 1 0-240 120 120 0 1 1 0 240z',
  },
  wallet: {
    width: 512,
    height: 512,
    path: 'M64 32C28.7 32 0 60.7 0 96L0 384c0 35.3 28.7 64 64 64l384 0c35.3 0 64-28.7 64-64l0-192c0-35.3-28.7-64-64-64L72 128c-13.3 0-24-10.7-24-24S58.7 80 72 80l384 0c13.3 0 24-10.7 24-24s-10.7-24-24-24L64 32zM416 256a32 32 0 1 1 0 64 32 32 0 1 1 0-64z',
  },
} satisfies Record<string, FontAwesomeIconSpec>;

function createFontAwesomeIcon(spec: FontAwesomeIconSpec): TaskIconComponent {
  return function FontAwesomeTaskIcon({ size = 18, color = '#9EA5B8' }: TaskIconProps) {
    return (
      <Svg width={size} height={size} viewBox={`0 0 ${spec.width} ${spec.height}`}>
        <Path d={spec.path} fill={color} />
      </Svg>
    );
  };
}

const LoginIcon = createFontAwesomeIcon(FONT_AWESOME_ICONS.rightToBracket);
const CalendarCheckIcon = createFontAwesomeIcon(FONT_AWESOME_ICONS.calendarCheck);
const GamepadIcon = createFontAwesomeIcon(FONT_AWESOME_ICONS.gamepad);
const GiftIcon = createFontAwesomeIcon(FONT_AWESOME_ICONS.gift);
const UserPlusIcon = createFontAwesomeIcon(FONT_AWESOME_ICONS.userPlus);
const WalletIcon = createFontAwesomeIcon(FONT_AWESOME_ICONS.wallet);

function formatMoney(amount: number) {
  return `¥${amount.toFixed(2)}`;
}

function getTaskActionPath(task: DailyTask) {
  const path = task.jumpPath || '';

  if (path.includes('recharge')) return '/deposit';
  if (path.includes('invite')) return '/mine';
  if (path.includes('game')) return '/';
  return null;
}

function normalizeDailyTasks(tasks: DailyTask[]) {
  return tasks.map((task) => (task.title.includes('邀请') ? { ...task, rewardAmount: 100 } : task));
}

function getTaskPendingVisual(task: DailyTask): {
  icon: TaskIconComponent;
  bg: string;
  border: string;
  color: string;
} {
  const title = task.title || '';

  if (title.includes('充值')) {
    return {
      icon: WalletIcon,
      bg: '#172535',
      border: '#355375',
      color: '#6db8ff',
    };
  }

  if (title.includes('游戏')) {
    return {
      icon: GamepadIcon,
      bg: '#241d39',
      border: '#514072',
      color: '#b896ff',
    };
  }

  if (title.includes('邀请')) {
    return {
      icon: UserPlusIcon,
      bg: '#3a1f29',
      border: '#6a4050',
      color: '#ff9fbb',
    };
  }

  if (title.includes('登录')) {
    return {
      icon: LoginIcon,
      bg: '#383119',
      border: '#6b5a2e',
      color: '#ffd36f',
    };
  }

  if (title.includes('签到')) {
    return {
      icon: CalendarCheckIcon,
      bg: '#383119',
      border: '#6b5a2e',
      color: '#ffd36f',
    };
  }

  return {
    icon: GiftIcon,
    bg: '#2b3246',
    border: '#4c5673',
    color: '#8f9bb8',
  };
}

function TaskIconBadge({
  children,
  backgroundColor = '#353a4d',
}: {
  children: ReactNode;
  backgroundColor?: string;
}) {
  return (
    <View
      className="h-11 w-11 items-center justify-center rounded-full"
      style={{ backgroundColor }}>
      {children}
    </View>
  );
}

export default function EarnScreen() {
  const [refreshSeed, setRefreshSeed] = useState(0);

  const refreshAll = useCallback(() => {
    setRefreshSeed((value) => value + 1);
  }, []);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51InnerPage
        title="福利中心"
        subtitle="签到任务与会员福利"
        tag="会员福利"
        tone="gold"
        hideHero>
        <CheckInCard refreshSeed={refreshSeed} onRefresh={refreshAll} />
        <DailyTasksCard refreshSeed={refreshSeed} onRefresh={refreshAll} />
      </Pg51InnerPage>
    </>
  );
}

function CheckInCard({ refreshSeed, onRefresh }: { refreshSeed: number; onRefresh: () => void }) {
  const [data, setData] = useState<CheckInStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    const result = await getCheckInStatus();

    if (apiOk(result) && result.data) {
      setData(result.data);
    } else {
      setData(null);
    }

    setLoading(false);
  }, []);

  useFocusEffect(
    useCallback(() => {
      loadData();
    }, [loadData, refreshSeed])
  );

  const handleCheckIn = useCallback(async () => {
    if (!data || data.isTodayChecked || submitting) return;

    setSubmitting(true);
    const result = await playerCheckIn();

    if (apiOk(result) && result.data) {
      Toast.show({
        type: 'success',
        text1: '签到成功',
        text2: `获得 ${result.data.bonusPoints || 0} 积分`,
      });
      onRefresh();
    } else {
      Toast.show({ type: 'error', text1: '签到失败', text2: result.message || '请稍后再试' });
    }

    setSubmitting(false);
  }, [data, onRefresh, submitting]);

  return (
    <View className="overflow-hidden rounded-[30px] border border-[#5d43a0] bg-[#23183b] px-4 pb-5 pt-4">
      <View className="flex-row items-start justify-between gap-3">
        <View className="flex-1">
          <View className="flex-row items-center gap-2.5">
            <Text className="text-[22px] font-black text-white">每日签到</Text>
            <View className="flex-row items-center gap-1 rounded-full bg-[#4a2b18] px-2.5 py-1">
              <Text className="text-[11px] font-bold text-[#ffb35c]">
                {data?.continuousDays || 0}天连签
              </Text>
            </View>
          </View>
          <Text className="mt-2 text-[12px] leading-[20px] text-[#b9b0d1]">
            每日完成签到后可领取积分，连续签到天数将自动累计。
          </Text>
        </View>

        <View className="items-end">
          <Text className="text-[11px] font-semibold text-[#a69abb]">累计积分</Text>
          <Text className="mt-1 text-[28px] font-black text-[#ffd36a]">
            {data?.activityPoint || 0}
          </Text>
        </View>
      </View>

      <View className="mt-5 min-h-[76px] justify-center">
        {loading ? (
          <CheckInSkeleton />
        ) : data?.checkInDays?.length ? (
          <View className="flex-row justify-between">
            {data.checkInDays.map((item) => {
              const isDone = item.checked;
              const isToday = item.isToday;
              const canCheckIn = isToday && !data.isTodayChecked;

              return (
                <Pressable
                  key={`${item.day}-${item.date}`}
                  className="items-center gap-1"
                  disabled={!canCheckIn || submitting}
                  onPress={handleCheckIn}>
                  <Text
                    className="text-[11px] font-bold"
                    style={{ color: isToday ? '#ff9a3c' : isDone ? '#ffffff' : '#b1a7ca' }}>
                    {item.day}
                  </Text>
                  <Text
                    className="text-[9px]"
                    style={{ color: isToday ? '#ff9a3c' : isDone ? '#9f93bc' : '#74698e' }}>
                    {item.date}
                  </Text>

                  <View
                    className="mt-1 h-10 w-10 items-center justify-center rounded-full"
                    style={
                      isDone
                        ? { backgroundColor: '#7c5dff' }
                        : isToday
                          ? {
                              borderWidth: 2,
                              borderColor: '#ff9a3c',
                              backgroundColor: '#ff9a3c12',
                            }
                          : { borderWidth: 1.5, borderColor: '#5c5175', borderStyle: 'dashed' }
                    }>
                    {isDone ? (
                      <Icon as={Check} size={18} className="text-white" />
                    ) : isToday ? (
                      <Text className="text-[13px] font-black text-[#ff9a3c]">
                        {submitting ? '...' : '签'}
                      </Text>
                    ) : (
                      <Text className="text-[11px] font-bold text-[#8d82a9]">{item.reward}</Text>
                    )}
                  </View>
                </Pressable>
              );
            })}
          </View>
        ) : (
          <Text className="text-center text-[12px] text-[#b9b0d1]">签到数据加载失败</Text>
        )}
      </View>
    </View>
  );
}

function DailyTasksCard({
  refreshSeed,
  onRefresh,
}: {
  refreshSeed: number;
  onRefresh: () => void;
}) {
  const router = useRouter();
  const [tasks, setTasks] = useState<DailyTask[]>([]);
  const [totalActivityPoint, setTotalActivityPoint] = useState(0);
  const [claimedChests, setClaimedChests] = useState<number[]>([]);
  const [loading, setLoading] = useState(true);
  const [actingId, setActingId] = useState('');
  const [actingChest, setActingChest] = useState<number | null>(null);
  const downloadRedirectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    const result = await getDailyTasks();

    if (apiOk(result) && result.data) {
      setTasks(normalizeDailyTasks(result.data.tasks || []));
      setTotalActivityPoint(result.data.totalActivityPoint || 0);
      setClaimedChests(result.data.claimedChests || []);
    } else {
      setTasks([]);
      setTotalActivityPoint(0);
      setClaimedChests([]);
    }

    setLoading(false);
  }, []);

  useFocusEffect(
    useCallback(() => {
      loadData();
    }, [loadData, refreshSeed])
  );

  useEffect(() => {
    return () => {
      if (downloadRedirectTimerRef.current) {
        clearTimeout(downloadRedirectTimerRef.current);
      }
    };
  }, []);

  const handleTaskPress = useCallback(
    async (task: DailyTask) => {
      if (task.status === 2 || actingId) return;

      if (task.status === 1) {
        setActingId(task.id);
        const result = await claimDailyTask(task.id);

        if (apiOk(result) && result.data) {
          Toast.show({
            type: 'success',
            text1: '领取成功',
            text2: `余额更新为 ${formatMoney(result.data.newBalance || 0)}`,
          });
          onRefresh();
        } else {
          Toast.show({ type: 'error', text1: '领取失败', text2: result.message || '请稍后再试' });
        }

        setActingId('');
        return;
      }

      if (task.title.includes('签到')) {
        setActingId(task.id);
        const result = await playerCheckIn();

        if (apiOk(result) && result.data) {
          Toast.show({
            type: 'success',
            text1: '签到成功',
            text2: `获得 ${result.data.bonusPoints || 0} 积分`,
          });
          onRefresh();
        } else {
          Toast.show({ type: 'error', text1: '操作失败', text2: result.message || '请稍后再试' });
        }

        setActingId('');
        return;
      }

      const path = getTaskActionPath(task);
      if (path) {
        router.push(path as '/' | '/deposit' | '/mine');
        return;
      }

      Toast.show({
        type: 'info',
        text1: '暂不可跳转',
        text2: '当前任务进度将自动刷新，请稍后查看。',
      });
    },
    [actingId, onRefresh, router]
  );

  const handleClaimChest = useCallback(
    async (target: number) => {
      if (APP_ONLY_CHEST_TARGETS.has(target) && !isInstalledAppRuntime()) {
        Toast.show({
          type: 'info',
          text1: '请下载 App 领取',
          text2: '高积分宝箱仅支持在 App 内领取，即将前往下载页面。',
          duration: CHEST_DOWNLOAD_REDIRECT_DELAY_MS,
        });

        if (downloadRedirectTimerRef.current) {
          clearTimeout(downloadRedirectTimerRef.current);
        }

        downloadRedirectTimerRef.current = setTimeout(() => {
          router.push('/download');
          downloadRedirectTimerRef.current = null;
        }, CHEST_DOWNLOAD_REDIRECT_DELAY_MS);
        return;
      }

      if (actingChest !== null) return;

      setActingChest(target);
      const result = await claimActivityChest(target);

      if (apiOk(result) && result.data) {
        Toast.show({
          type: 'success',
          text1: '领取成功',
          text2: `余额更新为 ${formatMoney(result.data.newBalance || 0)}`,
        });
        onRefresh();
      } else {
        Toast.show({ type: 'error', text1: '领取失败', text2: result.message || '请稍后再试' });
      }

      setActingChest(null);
    },
    [actingChest, onRefresh, router]
  );

  const finishedTaskCount = tasks.filter((item) => item.status >= 1).length;

  return (
    <Pg51SectionCard
      title="每日任务"
      description="完成任务领取奖励与积分"
      right={
        <View className="rounded-full border border-[#5a47a0] bg-[#241d36] px-3 py-1.5">
          <Text className="text-[11px] font-bold text-[#bca8ff]">
            进度 {finishedTaskCount}/{tasks.length || 0}
          </Text>
        </View>
      }>
      {loading ? (
        <DailyTasksSkeleton />
      ) : tasks.length ? (
        <>
          {tasks.map((task) => (
            <TaskRow
              key={task.id}
              task={task}
              loading={actingId === task.id}
              onPress={() => handleTaskPress(task)}
            />
          ))}

          <MilestoneCard
            totalActivityPoint={totalActivityPoint}
            claimedChests={claimedChests}
            actingChest={actingChest}
            onClaimChest={handleClaimChest}
          />
        </>
      ) : (
        <View className="rounded-[22px] border border-[#313b52] bg-[#212838] px-4 py-6">
          <Text className="text-center text-[13px] text-[#98a3ba]">暂无任务数据</Text>
        </View>
      )}
    </Pg51SectionCard>
  );
}

function TaskRow({
  task,
  loading,
  onPress,
}: {
  task: DailyTask;
  loading: boolean;
  onPress: () => void;
}) {
  const isDone = task.status === 2;
  const isClaim = task.status === 1;
  const progressText =
    task.targetValue > 1 ? `${task.currentValue}/${task.targetValue}` : task.description;
  const pendingVisual = getTaskPendingVisual(task);
  const TaskIcon = pendingVisual.icon;

  return (
    <Pressable
      onPress={onPress}
      disabled={isDone || loading}
      className="flex-row items-center gap-3 rounded-[22px] border px-4 py-4 active:opacity-90"
      style={{
        backgroundColor: isDone ? '#1b3026' : '#212838',
        borderColor: isDone ? '#315642' : '#313b52',
      }}>
      <TaskIconBadge backgroundColor={isDone ? '#243b31' : '#353a4d'}>
        {isDone ? (
          <Icon as={Check} size={18} className="text-[#53de90]" />
        ) : (
          <TaskIcon size={18} color={isClaim ? '#ff9a3c' : pendingVisual.color} />
        )}
      </TaskIconBadge>

      <View className="min-w-0 flex-1">
        <View className="flex-row flex-wrap items-center gap-1.5">
          <Text className="text-[15px] font-bold text-white">{task.title}</Text>
          <View className="rounded-full bg-[#2b2342] px-2 py-0.5">
            <Text className="text-[10px] font-bold text-[#bca8ff]">积分+{task.activityPoint}</Text>
          </View>
        </View>
        <Text className="mt-1 text-[12px] leading-[19px] text-[#98a3ba]">{progressText}</Text>
        <Text className="mt-1 text-[12px] font-semibold text-[#ffb35c]">
          奖励金额 {formatMoney(task.rewardAmount || 0)}
        </Text>
      </View>

      <View
        className="rounded-full px-3.5 py-2"
        style={{
          backgroundColor: isDone ? '#23382d' : isClaim ? '#ff8a34' : '#2a3145',
          borderWidth: isDone || isClaim ? 0 : 1,
          borderColor: '#4d5876',
        }}>
        <Text
          className="text-[11px] font-bold"
          style={{ color: isDone ? '#77e2a4' : isClaim ? '#ffffff' : '#b8c2d9' }}>
          {loading ? '处理中' : isDone ? '已完成' : isClaim ? '领取' : '去完成'}
        </Text>
      </View>
    </Pressable>
  );
}

function MilestoneCard({
  totalActivityPoint,
  claimedChests,
  actingChest,
  onClaimChest,
}: {
  totalActivityPoint: number;
  claimedChests: number[];
  actingChest: number | null;
  onClaimChest: (target: number) => void;
}) {
  const milestoneMax = TASK_MILESTONES[TASK_MILESTONES.length - 1].target;

  return (
    <View className="rounded-[24px] border border-[#5a4630] bg-[#1c1a1d] px-4 py-4">
      <View className="flex-row items-center justify-between gap-3">
        <View className="flex-row items-center gap-2">
          <GiftIcon size={18} color="#ffb35c" />
          <Text className="text-[14px] font-bold text-white">积分宝箱</Text>
        </View>
        <Text className="text-[12px] font-bold text-[#ffb35c]">今日积分：{totalActivityPoint}</Text>
      </View>

      <Text className="mt-2 text-[11px] leading-[18px] text-[#b9ad9a]">
        达到对应积分档位后，可领取额外奖励，当日仅可领取一次。
      </Text>
      <Text className="mt-1 text-[10px] leading-[16px] text-[#8d7f6a]">
        45分及以上档位仅支持在 App 内领取。
      </Text>

      <View className="mt-4 h-[8px] overflow-hidden rounded-full bg-[#332d38]">
        <View
          className="h-full rounded-full bg-[#ff7d58]"
          style={{ width: `${Math.min((totalActivityPoint / milestoneMax) * 100, 100)}%` }}
        />
      </View>

      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        className="mt-4"
        contentContainerStyle={{ paddingRight: 4 }}>
        <View className="flex-row items-start gap-2">
          {TASK_MILESTONES.map((item) => {
            const isAppOnly = APP_ONLY_CHEST_TARGETS.has(item.target);
            const reached = totalActivityPoint >= item.target;
            const claimed = claimedChests.includes(item.target);
            const canClaim = reached && !claimed;

            return (
              <Pressable
                key={item.target}
                className="w-[52px] shrink-0 items-center"
                disabled={!canClaim || actingChest !== null}
                onPress={() => onClaimChest(item.target)}>
                <View
                  className="size-10 items-center justify-center rounded-full"
                  style={{
                    backgroundColor: claimed ? '#2fd17c' : canClaim ? '#ff8a34' : '#2d2940',
                    borderWidth: 1,
                    borderColor: claimed
                      ? '#62e2a0'
                      : canClaim
                        ? '#ffc07b'
                        : isAppOnly
                          ? '#8f6a35'
                          : '#574f73',
                  }}>
                  {claimed ? (
                    <Icon as={Check} size={16} className="text-white" />
                  ) : canClaim ? (
                    <Text className="text-[11px] font-bold text-white">
                      {actingChest === item.target ? '...' : '领'}
                    </Text>
                  ) : (
                    <Text className="text-[11px] font-bold text-[#b3a9d1]">{item.target}</Text>
                  )}
                </View>
                <Text className="mt-2 text-[10px] font-semibold text-[#d1c6e9]">
                  ¥{item.reward}
                </Text>
              </Pressable>
            );
          })}
        </View>
      </ScrollView>
    </View>
  );
}

function CheckInSkeleton() {
  return (
    <View className="flex-row justify-between">
      {Array.from({ length: 7 }).map((_, index) => (
        <View key={index} className="items-center gap-1">
          <Skeleton width={18} height={11} radius={4} />
          <Skeleton width={24} height={9} radius={4} />
          <View className="mt-1">
            <SkeletonCircle size={40} />
          </View>
        </View>
      ))}
    </View>
  );
}

function DailyTasksSkeleton() {
  return (
    <View className="gap-3">
      {Array.from({ length: 3 }).map((_, index) => (
        <View
          key={index}
          className="flex-row items-center gap-3 rounded-[22px] border border-[#313b52] bg-[#212838] px-4 py-4">
          <SkeletonCircle size={44} />
          <View className="min-w-0 flex-1 gap-2">
            <Skeleton width={140} height={15} radius={6} />
            <Skeleton width={180} height={12} radius={6} />
            <Skeleton width={100} height={12} radius={6} />
          </View>
          <Skeleton width={56} height={26} radius={999} />
        </View>
      ))}

      <View className="rounded-[24px] border border-[#5a4630] bg-[#1c1a1d] px-4 py-4">
        <View className="flex-row items-center justify-between gap-3">
          <Skeleton width={110} height={16} radius={6} />
          <Skeleton width={90} height={12} radius={6} />
        </View>
        <View className="mt-3 gap-2">
          <Skeleton width="100%" height={11} radius={6} />
          <Skeleton width="60%" height={11} radius={6} />
        </View>
        <View className="mt-4 h-[8px] overflow-hidden rounded-full">
          <Skeleton width="100%" height={8} radius={999} />
        </View>
        <View className="mt-4 flex-row justify-between">
          {Array.from({ length: 6 }).map((_, index) => (
            <View key={index} className="w-[52px] items-center gap-2">
              <SkeletonCircle size={40} />
              <Skeleton width={28} height={10} radius={4} />
            </View>
          ))}
        </View>
      </View>
    </View>
  );
}
