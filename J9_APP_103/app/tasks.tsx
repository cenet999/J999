import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { useAuthModal } from '@/components/auth/auth-modal-provider';
import { Icon } from '@/components/ui/icon';
import { Toast } from '@/components/ui/toast';
import { Text } from '@/components/ui/text';
import {
  claimActivityChest,
  claimDailyTask,
  getDailyTasks,
  type DailyTask,
  type DailyTaskResponse,
} from '@/lib/api/event';
import { apiOk } from '@/lib/api/request';
import { formatCny } from '@/lib/format-money';
import { Stack, useFocusEffect, useRouter } from 'expo-router';
import { ArrowLeft, Check, Coins, Flame, Gift, Star } from 'lucide-react-native';
import { useCallback, useEffect, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, View } from 'react-native';
import Animated, {
  useAnimatedStyle,
  useSharedValue,
  withRepeat,
  withSequence,
  withTiming,
} from 'react-native-reanimated';

const CHEST_MILESTONES = [20, 30, 45, 68, 102, 153];

function resolveTaskPath(task: DailyTask) {
  const path = task.jumpPath || '';
  if (path.includes('recharge')) return '/deposit';
  if (path.includes('invite')) return '/mine';
  if (path.includes('game')) return '/';
  return task.jumpPath || '';
}

export default function TasksScreen() {
  const router = useRouter();
  const { requireAuth } = useAuthModal();
  const [refreshing, setRefreshing] = useState(false);
  const [data, setData] = useState<DailyTaskResponse>({
    tasks: [],
    totalActivityPoint: 0,
    claimedChests: [],
  });

  const fetchData = useCallback(async () => {
    const authenticated = await requireAuth('login');
    if (!authenticated) return;

    const result = await getDailyTasks();
    if (apiOk(result) && result.data) {
      setData(result.data);
    } else {
      Toast.show({ type: 'error', text1: '获取任务失败', text2: result.message });
    }
  }, [requireAuth]);

  useFocusEffect(
    useCallback(() => {
      void fetchData();
    }, [fetchData])
  );

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    await fetchData();
    setRefreshing(false);
  }, [fetchData]);

  const handleClaimTask = async (taskId: string) => {
    const result = await claimDailyTask(taskId);
    if (apiOk(result)) {
      Toast.show({
        type: 'success',
        text1: '领取成功',
        text2: `余额更新：${formatCny(result.data?.newBalance ?? 0)}`,
      });
      await fetchData();
    } else {
      Toast.show({ type: 'error', text1: '领奖失败', text2: result.message });
    }
  };

  const handleClaimChest = async (targetPoint: number) => {
    const result = await claimActivityChest(targetPoint);
    if (apiOk(result)) {
      Toast.show({ type: 'success', text1: '开启宝箱成功', text2: result.message });
      await fetchData();
    } else {
      Toast.show({ type: 'error', text1: '开启宝箱失败', text2: result.message });
    }
  };

  const maxPoint = CHEST_MILESTONES[CHEST_MILESTONES.length - 1];
  const progressPercent = Math.min((data.totalActivityPoint / maxPoint) * 100, 100);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <View className="flex-1 bg-[#0f1420]">
        <ScrollView
          className="flex-1"
          contentContainerStyle={{ paddingHorizontal: 8, paddingTop: 16, paddingBottom: 110 }}
          refreshControl={
            <RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor="#9b5cff" />
          }>
          <View className="mb-4 flex-row items-center gap-3">
            <Pressable onPress={() => router.back()}>
              <Pg51LucideIconBadge
                icon={ArrowLeft}
                size={44}
                iconSize={18}
                radius={18}
                color="#ffffff"
              />
            </Pressable>
            <View>
              <Text className="text-[22px] font-black text-white">每日任务</Text>
              <Text className="mt-1 text-[12px] text-[#97a1b8]">任务积分与宝箱奖励</Text>
            </View>
          </View>

          <View className="mb-4 rounded-[24px] border border-[#39435a] bg-[#171d2a] p-4">
            <View className="mb-4 flex-row items-center justify-between">
              <View className="flex-row items-center gap-3">
                <Pg51LucideIconBadge icon={Flame} size={44} iconSize={20} radius={18} />
                <View>
                  <Text className="text-[12px] text-[#9fa8be]">今日活跃度</Text>
                  <Text className="text-[28px] font-black text-white">
                    {data.totalActivityPoint}
                  </Text>
                </View>
              </View>
              <Text className="text-[12px] text-[#8f9ab2]">/{maxPoint}</Text>
            </View>

            <View className="relative h-16">
              <View className="absolute left-4 right-4 top-6 h-2 rounded-full bg-[#232b3d]" />
              <View
                className="absolute left-4 top-6 h-2 rounded-full bg-[#9b5cff]"
                style={{ width: `${progressPercent}%` }}
              />

              <View className="absolute left-4 right-4 top-0 flex-row items-center justify-between">
                {CHEST_MILESTONES.map((point) => {
                  const isClaimed = data.claimedChests.includes(point);
                  const canClaim = data.totalActivityPoint >= point && !isClaimed;

                  return (
                    <ChestItem
                      key={point}
                      point={point}
                      isClaimed={isClaimed}
                      canClaim={canClaim}
                      onClaim={() => void handleClaimChest(point)}
                    />
                  );
                })}
              </View>
            </View>
          </View>

          <View className="gap-3">
            {data.tasks.length === 0 ? (
              <View className="rounded-[24px] border border-[#39435a] bg-[#171d2a] px-4 py-8">
                <Text className="text-center text-[14px] text-[#cdd5e6]">暂无任务数据</Text>
              </View>
            ) : (
              data.tasks.map((task) => (
                <TaskCard
                  key={task.id}
                  task={task}
                  onClaim={() => void handleClaimTask(task.id)}
                  onGo={() => {
                    const path = resolveTaskPath(task);
                    if (path) router.push(path as any);
                  }}
                />
              ))
            )}
          </View>
        </ScrollView>
      </View>
    </>
  );
}

function ChestItem({
  point,
  isClaimed,
  canClaim,
  onClaim,
}: {
  point: number;
  isClaimed: boolean;
  canClaim: boolean;
  onClaim: () => void;
}) {
  const scale = useSharedValue(1);

  useEffect(() => {
    if (canClaim) {
      scale.value = withRepeat(
        withSequence(withTiming(1.12, { duration: 500 }), withTiming(1, { duration: 500 })),
        -1,
        true
      );
    } else {
      scale.value = 1;
    }
  }, [canClaim, scale]);

  const animatedStyle = useAnimatedStyle(() => ({
    transform: [{ scale: scale.value }],
  }));

  return (
    <View className="items-center">
      <Pressable onPress={canClaim ? onClaim : undefined}>
        <Animated.View
          style={animatedStyle}
          className="size-10 items-center justify-center rounded-full border-2 border-white bg-white/90">
          {isClaimed ? (
            <Icon as={Check} size={16} color="#7B5CFF" />
          ) : (
            <Icon as={Gift} size={18} color={canClaim ? '#FF8A34' : '#9ca3af'} />
          )}
        </Animated.View>
      </Pressable>
      <View className="mt-2 rounded-full bg-[#232b3d] px-2 py-0.5">
        <Text className="text-[10px] font-bold text-white">{point}</Text>
      </View>
    </View>
  );
}

function TaskCard({
  task,
  onClaim,
  onGo,
}: {
  task: DailyTask;
  onClaim: () => void;
  onGo: () => void;
}) {
  const progressPercent = Math.min((task.currentValue / task.targetValue) * 100, 100);
  const canClaim = task.status === 1;
  const claimed = task.status === 2;

  const TaskIcon = task.icon === 'star' ? Star : task.icon === 'coins' ? Coins : Flame;

  return (
    <View className="rounded-[24px] border border-[#39435a] bg-[#171d2a] p-4">
      <View className="flex-row items-center gap-3">
        <Pg51LucideIconBadge icon={TaskIcon} size={48} iconSize={20} radius={18} />

        <View className="flex-1">
          <View className="flex-row flex-wrap items-center">
            <Text className="text-[15px] font-black text-white">{task.title}</Text>
            <View className="ml-2 rounded-md bg-[#2d2618] px-1.5 py-0.5">
              <Text className="text-[10px] font-bold text-[#f6c453]">
                +{task.activityPoint} 活跃度
              </Text>
            </View>
          </View>
          <Text className="mt-1 text-[12px] leading-[20px] text-[#9fa8be]">
            {task.description?.trim() || '完成任务即可领取奖励'}
          </Text>
          <Text className="mt-1 text-[12px] font-semibold text-[#ffb547]">
            奖励金额 {formatCny(task.rewardAmount)}
          </Text>
        </View>
      </View>

      <View className="mt-4 flex-row items-center gap-2">
        <View className="h-1.5 flex-1 overflow-hidden rounded-full bg-[#232b3d]">
          <View
            className="h-full rounded-full bg-[#9b5cff]"
            style={{ width: `${progressPercent}%` }}
          />
        </View>
        <Text className="text-[11px] font-semibold text-[#8f9ab2]">
          {task.currentValue}/{task.targetValue}
        </Text>
      </View>

      <View className="mt-4">
        {claimed ? (
          <View className="rounded-[18px] bg-[#172b26] px-4 py-3">
            <Text className="text-center text-[14px] font-bold text-[#4ade80]">已领取</Text>
          </View>
        ) : canClaim ? (
          <Pressable onPress={onClaim} className="rounded-[18px] bg-[#6f1dff] px-4 py-3">
            <Text className="text-center text-[14px] font-bold text-white">领取奖励</Text>
          </Pressable>
        ) : (
          <Pressable onPress={onGo} className="rounded-[18px] bg-[#232b3d] px-4 py-3">
            <Text className="text-center text-[14px] font-bold text-[#d7def0]">前往完成</Text>
          </Pressable>
        )}
      </View>
    </View>
  );
}
