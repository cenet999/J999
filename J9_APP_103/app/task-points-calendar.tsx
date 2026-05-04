import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { useAuthModal } from '@/components/auth/auth-modal-provider';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { getMonthlyTaskActivity, type MonthlyTaskActivityResponse } from '@/lib/api/event';
import { Stack, useFocusEffect, useRouter } from 'expo-router';
import { ArrowLeft, ChevronLeft, ChevronRight, Flame } from 'lucide-react-native';
import { useCallback, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, View } from 'react-native';

const WEEK_LABELS = ['一', '二', '三', '四', '五', '六', '日'];

function pad2(value: number) {
  return value.toString().padStart(2, '0');
}

function buildMonthCells(year: number, month: number): Array<number | null> {
  const firstDay = new Date(year, month - 1, 1);
  const lastDay = new Date(year, month, 0).getDate();
  const offset = (firstDay.getDay() + 6) % 7;
  const cells: Array<number | null> = [];

  for (let i = 0; i < offset; i += 1) cells.push(null);
  for (let day = 1; day <= lastDay; day += 1) cells.push(day);

  return cells;
}

function isTodayDate(year: number, month: number, day: number) {
  const today = new Date();
  return today.getFullYear() === year && today.getMonth() + 1 === month && today.getDate() === day;
}

export default function TaskPointsCalendarScreen() {
  const router = useRouter();
  const { requireAuth } = useAuthModal();
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [data, setData] = useState<MonthlyTaskActivityResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const loadData = useCallback(async () => {
    const authenticated = await requireAuth('login');
    if (!authenticated) {
      setData(null);
      setLoading(false);
      return;
    }

    setLoading(true);
    try {
      const result = await getMonthlyTaskActivity(year, month);
      if (result.success && result.data) {
        setData(result.data);
      } else {
        setData(null);
      }
    } finally {
      setLoading(false);
    }
  }, [month, requireAuth, year]);

  useFocusEffect(
    useCallback(() => {
      void loadData();
    }, [loadData])
  );

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    await loadData();
    setRefreshing(false);
  }, [loadData]);

  const pointsByDate = useMemo(() => {
    const pointMap: Record<string, number> = {};
    for (const day of data?.days ?? []) {
      pointMap[day.date] = day.taskActivityPoint;
    }
    return pointMap;
  }, [data]);

  const cells = useMemo(() => buildMonthCells(year, month), [month, year]);
  const canGoNext =
    year < now.getFullYear() || (year === now.getFullYear() && month < now.getMonth() + 1);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <View className="flex-1 bg-[#0f1420]">
        <ScrollView
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
              <Text className="text-[22px] font-black text-white">任务积分日历</Text>
              <Text className="mt-1 text-[12px] text-[#97a1b8]">按月查看每日任务积分</Text>
            </View>
          </View>

          <View className="mb-4 rounded-[24px] border border-[#39435a] bg-[#171d2a] p-4">
            <View className="flex-row items-center justify-between">
              <View className="flex-row items-center gap-3">
                <Pg51LucideIconBadge icon={Flame} size={48} iconSize={22} radius={18} />
                <View>
                  <Text className="text-[12px] text-[#9fa8be]">本月累计任务积分</Text>
                  <Text className="text-[26px] font-black text-white">{data?.monthTotal ?? 0}</Text>
                </View>
              </View>
              <View className="items-end">
                <Text className="text-[12px] text-[#9fa8be]">有积分天数</Text>
                <Text className="text-[22px] font-black text-[#9b5cff]">
                  {data?.activeDays ?? 0}
                </Text>
              </View>
            </View>
          </View>

          <View className="rounded-[24px] border border-[#39435a] bg-[#171d2a] p-4">
            <View className="mb-4 flex-row items-center justify-between">
              <Pressable
                onPress={() => {
                  if (month === 1) {
                    setYear((value) => value - 1);
                    setMonth(12);
                  } else {
                    setMonth((value) => value - 1);
                  }
                }}>
                <Pg51LucideIconBadge
                  icon={ChevronLeft}
                  size={40}
                  iconSize={18}
                  radius={14}
                  color="#ffffff"
                />
              </Pressable>

              <Text className="text-[18px] font-black text-white">
                {year} 年 {month} 月
              </Text>

              <Pressable
                disabled={!canGoNext}
                onPress={() => {
                  if (!canGoNext) return;
                  if (month === 12) {
                    setYear((value) => value + 1);
                    setMonth(1);
                  } else {
                    setMonth((value) => value + 1);
                  }
                }}
                style={{ opacity: canGoNext ? 1 : 0.35 }}>
                <Pg51LucideIconBadge
                  icon={ChevronRight}
                  size={40}
                  iconSize={18}
                  radius={14}
                  color="#ffffff"
                />
              </Pressable>
            </View>

            {loading ? (
              <View className="items-center py-12">
                <ActivityIndicator color="#9b5cff" />
              </View>
            ) : (
              <>
                <View className="mb-2 flex-row">
                  {WEEK_LABELS.map((label) => (
                    <View key={label} className="flex-1 items-center py-1">
                      <Text className="text-[11px] font-bold text-[#8f9ab2]">{label}</Text>
                    </View>
                  ))}
                </View>

                <View className="flex-row flex-wrap">
                  {cells.map((day, index) => {
                    if (day == null) {
                      return (
                        <View
                          key={`empty-${index}`}
                          className="mb-2 items-center justify-center"
                          style={{ width: `${100 / 7}%` }}
                        />
                      );
                    }

                    const dateKey = `${year}-${pad2(month)}-${pad2(day)}`;
                    const points = pointsByDate[dateKey] ?? 0;
                    const today = isTodayDate(year, month, day);

                    return (
                      <View
                        key={dateKey}
                        className="mb-2 items-center justify-center"
                        style={{ width: `${100 / 7}%` }}>
                        <View
                          className="h-[54px] w-[46px] items-center justify-center rounded-[16px]"
                          style={{
                            backgroundColor: today ? '#25193e' : points > 0 ? '#2d2618' : '#212838',
                            borderWidth: today ? 1.5 : 0,
                            borderColor: today ? '#9b5cff' : 'transparent',
                          }}>
                          <Text className="text-[14px] font-black text-white">{day}</Text>
                          <Text
                            className="mt-0.5 text-[10px] font-semibold"
                            style={{ color: points > 0 ? '#f6c453' : '#7f879b' }}>
                            {points > 0 ? `+${points}` : '—'}
                          </Text>
                        </View>
                      </View>
                    );
                  })}
                </View>
              </>
            )}
          </View>
        </ScrollView>
      </View>
    </>
  );
}
