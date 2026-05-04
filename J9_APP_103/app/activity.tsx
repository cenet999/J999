import { Pg51PageShell, Pg51TrackedScrollView } from '@/components/pg51-clone/chrome';
import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Skeleton } from '@/components/ui/skeleton';
import { Text } from '@/components/ui/text';
import { getEventDisplayNumbers } from '@/lib/event-progress';
import { apiOk, toAbsoluteUrl } from '@/lib/api/request';
import { getTimeLimitedEvents, type TimeLimitedEvent } from '@/lib/api/event';
import { Stack } from 'expo-router';
import { CalendarDays } from 'lucide-react-native';
import { useEffect, useState } from 'react';
import { Image, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

export default function ActivityScreen() {
  const insets = useSafeAreaInsets();
  const [events, setEvents] = useState<TimeLimitedEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let mounted = true;

    async function loadEvents() {
      setLoading(true);
      setError('');

      const res = await getTimeLimitedEvents();

      if (!mounted) return;

      if (apiOk(res) && Array.isArray(res.data)) {
        setEvents(res.data);
      } else {
        setEvents([]);
        setError(res.message || '活动加载失败');
      }

      setLoading(false);
    }

    loadEvents();

    return () => {
      mounted = false;
    };
  }, []);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51PageShell>
        <Pg51TrackedScrollView
          className="flex-1"
          showsVerticalScrollIndicator={false}
          contentContainerStyle={{
            paddingHorizontal: 8,
            paddingTop: insets.top + 20,
            paddingBottom: 110,
          }}>
          <View className="mb-4 rounded-[28px] border border-[#4f3a80] bg-[#171d2a] px-4 py-4">
            <View className="flex-row items-center justify-between gap-3">
              <View className="flex-1">
                <Text className="text-[20px] font-black text-white">活动中心</Text>
                <Text className="mt-1 text-[12px] leading-[19px] text-[#97a1b8]">
                  最新活动与会员福利
                </Text>
              </View>

              <Pg51LucideIconBadge icon={CalendarDays} size={44} iconSize={18} radius={18} />
            </View>
          </View>

          {loading ? (
            <ActivitySkeleton />
          ) : error ? (
            <View className="rounded-[24px] border border-[#5a2f3d] bg-[#2a1b23] px-4 py-4">
              <Text className="text-[14px] leading-[22px] text-[#ffb8c8]">{error}</Text>
            </View>
          ) : events.length > 0 ? (
            events.map((item, index) => {
              const imageUrl = toAbsoluteUrl(item.image);
              const displayProgress = getEventDisplayNumbers(item);

              return (
                <View
                  key={`${item.name}-${index}`}
                  className="mb-4 overflow-hidden rounded-[26px] border border-[#39435a] bg-[#171d2a] last:mb-0">
                  <View className="relative">
                    {imageUrl ? (
                      <Image
                        source={{ uri: imageUrl }}
                        style={{ width: '100%', height: 108 }}
                        resizeMode="cover"
                      />
                    ) : (
                      <View className="h-[108px] items-center justify-center bg-[#2d3447]">
                        <Text className="text-[14px] text-[#a4aec4]">暂无活动配图</Text>
                      </View>
                    )}

                    <View className="absolute inset-y-0 left-0 right-0 justify-center px-4">
                      <View className="max-w-[78%] self-start rounded-[18px] bg-[#111827cc] px-4 py-2.5">
                        <Text className="text-[15px] font-black text-white" numberOfLines={2}>
                          {item.name}
                        </Text>
                      </View>
                    </View>

                    <View className="absolute bottom-4 right-4 rounded-full bg-[#6f1dff] px-3 py-1.5">
                      <Text className="text-[12px] font-semibold text-white">
                        剩余 {item.timeLeft}
                      </Text>
                    </View>
                  </View>

                  <View className="px-4 pb-4 pt-4">
                    <View className="flex-row items-start gap-3">
                      <Text className="flex-1 text-[13px] leading-[21px] text-[#b5bed3]">
                        {item.desc}
                      </Text>

                      <View className="rounded-full bg-[#222a3a] px-2.5 py-1">
                        <Text className="text-[11px] font-semibold text-[#eef2ff]">
                          {displayProgress.text}
                        </Text>
                      </View>
                    </View>

                    <View className="mt-2 flex-row items-center justify-between">
                      <Text className="text-[11px] text-[#98a3ba]">{displayProgress.heatText}</Text>
                      <Text className="text-[11px] font-semibold text-[#d9e1f1]">活动进行中</Text>
                    </View>
                  </View>
                </View>
              );
            })
          ) : (
            <View className="rounded-[24px] border border-[#3c4560] bg-[#171d2a] px-4 py-6">
              <Text className="text-[14px] text-[#cdd5e6]">暂无进行中的活动</Text>
            </View>
          )}
        </Pg51TrackedScrollView>
      </Pg51PageShell>
    </>
  );
}

function ActivitySkeleton() {
  return (
    <View>
      {Array.from({ length: 3 }).map((_, index) => (
        <View
          key={index}
          className="mb-4 overflow-hidden rounded-[26px] border border-[#39435a] bg-[#171d2a]">
          <View className="relative">
            <Skeleton width="100%" height={108} radius={0} />

            <View className="absolute inset-y-0 left-0 right-0 justify-center px-4">
              <Skeleton width="70%" height={38} radius={18} />
            </View>

            <View className="absolute bottom-4 right-4">
              <Skeleton width={86} height={28} radius={999} />
            </View>
          </View>

          <View className="px-4 pb-4 pt-4">
            <View className="flex-row items-start gap-3">
              <View className="flex-1 gap-2">
                <Skeleton width="100%" height={13} radius={6} />
                <Skeleton width="80%" height={13} radius={6} />
              </View>
              <Skeleton width={56} height={22} radius={999} />
            </View>

            <View className="mt-3 flex-row items-center justify-between">
              <Skeleton width={100} height={11} radius={6} />
              <Skeleton width={64} height={11} radius={6} />
            </View>
          </View>
        </View>
      ))}
    </View>
  );
}
