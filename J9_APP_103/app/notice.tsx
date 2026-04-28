import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { getNotices } from '@/lib/api/notice';
import { Stack, useRouter } from 'expo-router';
import { ArrowLeft, Bell, Clock3 } from 'lucide-react-native';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, View } from 'react-native';

type NoticeRow = {
  id?: number | string;
  Id?: number | string;
  title?: string;
  Title?: string;
  content?: string;
  Content?: string;
  createdTime?: string;
  CreatedTime?: string;
};

function stripHtml(content: string) {
  return content.replace(/<[^>]*>?/gm, '').trim();
}

export default function NoticeScreen() {
  const router = useRouter();
  const [notices, setNotices] = useState<NoticeRow[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let mounted = true;

    (async () => {
      try {
        const result = await getNotices();
        if (mounted && result.success && result.data) {
          setNotices(result.data);
        }
      } finally {
        if (mounted) setLoading(false);
      }
    })();

    return () => {
      mounted = false;
    };
  }, []);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <View className="flex-1 bg-[#0f1420]">
        <ScrollView className="flex-1" contentContainerStyle={{ padding: 16, paddingBottom: 110 }}>
          <View className="mb-4 flex-row items-center justify-between">
            <View className="flex-row items-center gap-3">
              <Pressable onPress={() => (router.canGoBack() ? router.back() : router.replace('/'))}>
                <Pg51LucideIconBadge
                  icon={ArrowLeft}
                  size={44}
                  iconSize={18}
                  radius={18}
                  color="#ffffff"
                />
              </Pressable>
              <View>
                <Text className="text-[22px] font-black text-white">平台公告</Text>
                <Text className="mt-1 text-[12px] text-[#97a1b8]">查看最新活动信息与平台通知</Text>
              </View>
            </View>

            <Pg51LucideIconBadge icon={Bell} size={44} iconSize={18} radius={18} />
          </View>

          {loading ? (
            <View className="items-center rounded-[24px] border border-[#3c4560] bg-[#171d2a] py-10">
              <ActivityIndicator color="#9b5cff" />
              <Text className="mt-3 text-[14px] text-[#cdd5e6]">公告加载中...</Text>
            </View>
          ) : notices.length ? (
            <View className="gap-4">
              {notices.map((item, index) => {
                const title = item.title || item.Title || '平台公告';
                const content = stripHtml(item.content || item.Content || '');
                const createdTime = item.createdTime || item.CreatedTime;

                return (
                  <View
                    key={String(item.id ?? item.Id ?? index)}
                    className="rounded-[24px] border border-[#39435a] bg-[#171d2a] p-4">
                    <Text className="text-[17px] font-black text-white">{title}</Text>
                    <View className="mt-2 flex-row items-center gap-1.5">
                      <Icon as={Clock3} size={12} color="#8f9ab2" />
                      <Text className="text-[11px] text-[#8f9ab2]">
                        {createdTime ? new Date(createdTime).toLocaleString('zh-CN') : '最新'}
                      </Text>
                    </View>
                    <Text className="mt-3 text-[13px] leading-[22px] text-[#d7def0]">
                      {content || '暂无公告内容'}
                    </Text>
                  </View>
                );
              })}
            </View>
          ) : (
            <View className="rounded-[24px] border border-[#3c4560] bg-[#171d2a] px-4 py-6">
              <Text className="text-center text-[14px] text-[#cdd5e6]">暂无最新公告</Text>
            </View>
          )}
        </ScrollView>
      </View>
    </>
  );
}
