import { Pg51ChromeIconBadge } from '@/components/pg51-clone/original-icons';
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
  getTimeLimitedEvents,
  type CheckInStatus,
  type DailyTask,
  type TimeLimitedEvent,
} from '@/lib/api/event';
import { getEventDisplayNumbers } from '@/lib/event-progress';
import { apiOk, toAbsoluteUrl } from '@/lib/api/request';
import { Stack, useFocusEffect, useRouter } from 'expo-router';
import {
  CalendarDays,
  Check,
  ChevronRight,
  Circle,
  Clock,
  Flame,
  Gamepad2,
  Gift,
  UserPlus,
  Wallet,
} from 'lucide-react-native';
import type { LucideIcon } from 'lucide-react-native';
import { useCallback, useState } from 'react';
import { Image, Platform, Pressable, View } from 'react-native';

const TASK_MILESTONES = [
  { target: 20, reward: 2 },
  { target: 30, reward: 5 },
  { target: 45, reward: 12 },
  { target: 68, reward: 28 },
  { target: 102, reward: 62 },
  { target: 153, reward: 135 },
] as const;

const APP_ONLY_CHEST_TARGETS = new Set([45, 68, 102, 153]);

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

function isNativeAppPlatform() {
  return Platform.OS === 'ios' || Platform.OS === 'android';
}

function getTaskPendingVisual(task: DailyTask): {
  icon: LucideIcon;
  bg: string;
  border: string;
  color: string;
} {
  const title = task.title || '';

  if (title.includes('充值')) {
    return {
      icon: Wallet,
      bg: '#172535',
      border: '#355375',
      color: '#6db8ff',
    };
  }

  if (title.includes('游戏')) {
    return {
      icon: Gamepad2,
      bg: '#241d39',
      border: '#514072',
      color: '#b896ff',
    };
  }

  if (title.includes('邀请')) {
    return {
      icon: UserPlus,
      bg: '#3a1f29',
      border: '#6a4050',
      color: '#ff9fbb',
    };
  }

  if (title.includes('登录') || title.includes('签到')) {
    return {
      icon: CalendarDays,
      bg: '#383119',
      border: '#6b5a2e',
      color: '#ffd36f',
    };
  }

  return {
    icon: Circle,
    bg: '#2b3246',
    border: '#4c5673',
    color: '#8f9bb8',
  };
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
        subtitle="签到、任务与限时活动统一汇总，会员权益一目了然。"
        tag="会员福利"
        tone="gold"
        hideHero>
        <CheckInCard refreshSeed={refreshSeed} onRefresh={refreshAll} />
        <DailyTasksCard refreshSeed={refreshSeed} onRefresh={refreshAll} />
        <TimeLimitedEventsCard refreshSeed={refreshSeed} />
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
      if (APP_ONLY_CHEST_TARGETS.has(target) && !isNativeAppPlatform()) {
        Toast.show({
          type: 'info',
          text1: '请在 App 内领取',
          text2: '45分及以上的积分宝箱仅支持在 App 内领取。',
        });
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
    [actingChest, onRefresh]
  );

  const finishedTaskCount = tasks.filter((item) => item.status >= 1).length;

  return (
    <Pg51SectionCard
      title="每日任务"
      description="完成指定任务后，可领取对应奖励与积分。"
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

  return (
    <Pressable
      onPress={onPress}
      disabled={isDone || loading}
      className="flex-row items-center gap-3 rounded-[22px] border px-4 py-4 active:opacity-90"
      style={{
        backgroundColor: isDone ? '#1b3026' : '#212838',
        borderColor: isDone ? '#315642' : '#313b52',
      }}>
      <Pg51ChromeIconBadge size={44} radius={22} active={isClaim}>
        {isDone ? (
          <Icon as={Check} size={18} className="text-[#53de90]" />
        ) : isClaim ? (
          <Icon as={Clock} size={18} className="text-[#ff9a3c]" />
        ) : (
          <Icon as={pendingVisual.icon} size={18} color={pendingVisual.color} />
        )}
      </Pg51ChromeIconBadge>

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
          <Icon as={Gift} size={18} className="text-[#ffb35c]" />
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

      <View className="mt-4 flex-row justify-between">
        {TASK_MILESTONES.map((item) => {
          const isAppOnly = APP_ONLY_CHEST_TARGETS.has(item.target);
          const reached = totalActivityPoint >= item.target;
          const claimed = claimedChests.includes(item.target);
          const canClaim = reached && !claimed;

          return (
            <Pressable
              key={item.target}
              className="w-[52px] items-center"
              disabled={!canClaim || actingChest !== null}
              onPress={() => onClaimChest(item.target)}>
              <View
                className="size-10 items-center justify-center rounded-full"
                style={{
                  backgroundColor: claimed ? '#2fd17c' : canClaim ? '#ff8a34' : '#2d2940',
                  borderWidth: 1,
                  borderColor: claimed ? '#62e2a0' : canClaim ? '#ffc07b' : isAppOnly ? '#8f6a35' : '#574f73',
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
              <Text className="mt-2 text-[10px] font-semibold text-[#d1c6e9]">¥{item.reward}</Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

function TimeLimitedEventsCard({ refreshSeed }: { refreshSeed: number }) {
  const [events, setEvents] = useState<TimeLimitedEvent[]>([]);
  const [loading, setLoading] = useState(true);

  const loadData = useCallback(async () => {
    setLoading(true);
    const result = await getTimeLimitedEvents();

    if (apiOk(result) && Array.isArray(result.data)) {
      setEvents(result.data);
    } else {
      setEvents([]);
    }

    setLoading(false);
  }, []);

  useFocusEffect(
    useCallback(() => {
      loadData();
    }, [loadData, refreshSeed])
  );

  return (
    <Pg51SectionCard
      title="限时活动"
      description="展示当前开放中的限时活动与参与进度。"
      right={
        <View className="flex-row items-center gap-1 rounded-full bg-[#212838] px-3 py-1.5">
          <Text className="text-[11px] font-bold text-[#9b5cff]">实时活动</Text>
          <Icon as={ChevronRight} size={14} color="#9b5cff" />
        </View>
      }>
      {loading ? (
        <TimeLimitedEventsSkeleton />
      ) : events.length ? (
        events.map((item, index) => <LimitedEventCard key={`${item.name}-${index}`} item={item} />)
      ) : (
        <View className="rounded-[22px] border border-[#313b52] bg-[#212838] px-4 py-6">
          <Text className="text-center text-[13px] text-[#98a3ba]">暂无进行中的活动</Text>
        </View>
      )}
    </Pg51SectionCard>
  );
}

function LimitedEventCard({ item }: { item: TimeLimitedEvent }) {
  const displayProgress = getEventDisplayNumbers(item);
  const imageUrl = toAbsoluteUrl(item.image);

  return (
    <View className="rounded-[22px] border border-[#313b52] bg-[#212838] p-3.5">
      <View className="flex-row items-start gap-3">
        <View className="overflow-hidden rounded-[16px] bg-[#2b3246]">
          {imageUrl ? (
            <Image
              source={{ uri: imageUrl }}
              style={{ width: 62, height: 62 }}
              resizeMode="cover"
            />
          ) : (
            <View className="h-[62px] w-[62px] items-center justify-center bg-[#2b3246]">
              <Icon as={Flame} size={18} className="text-[#ff9a3c]" />
            </View>
          )}
        </View>

        <View className="flex-1">
          <View className="flex-row items-start justify-between gap-2">
            <View className="flex-1">
              <Text className="text-[15px] font-bold text-white">{item.name}</Text>
              <Text className="mt-1 text-[12px] leading-[19px] text-[#9aa5bd]">{item.desc}</Text>
            </View>

            <View className="flex-row items-center gap-1 rounded-full bg-[#3b2a24] px-2.5 py-1">
              <Icon as={Flame} size={12} className="text-[#ff9a3c]" />
              <Text className="text-[10px] font-bold text-[#ffb35c]">{item.timeLeft}</Text>
            </View>
          </View>

          <View className="mt-3 flex-row items-center justify-between">
            <View className="flex-row items-center gap-1.5">
              <Icon as={CalendarDays} size={13} className="text-[#7c5dff]" />
              <Text className="text-[11px] text-[#98a3ba]">{displayProgress.heatText}</Text>
            </View>
            <Text className="text-[12px] font-bold text-[#d9e1f1]">
              {displayProgress.text}
            </Text>
          </View>
        </View>
      </View>
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

function TimeLimitedEventsSkeleton() {
  return (
    <View className="gap-3">
      {Array.from({ length: 2 }).map((_, index) => (
        <View
          key={index}
          className="rounded-[22px] border border-[#313b52] bg-[#212838] p-3.5">
          <View className="flex-row items-start gap-3">
            <Skeleton width={62} height={62} radius={16} />
            <View className="flex-1 gap-2">
              <View className="flex-row items-start justify-between gap-2">
                <View className="flex-1 gap-2">
                  <Skeleton width={140} height={15} radius={6} />
                  <Skeleton width={200} height={12} radius={6} />
                </View>
                <Skeleton width={56} height={22} radius={999} />
              </View>
              <View className="mt-2 flex-row items-center justify-between">
                <Skeleton width={90} height={11} radius={6} />
                <Skeleton width={60} height={12} radius={6} />
              </View>
            </View>
          </View>
        </View>
      ))}
    </View>
  );
}
