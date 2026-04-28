import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Pg51InnerPageTopBar } from '@/components/pg51-clone/inner-page-top-bar';
import { Pg51InnerPage, Pg51SectionCard } from '@/components/pg51-clone/page-ui';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import {
  deleteMessage,
  getMessages,
  markAllAsRead,
  markAsRead,
  MessageSenderRole,
  MessageStatus,
  type DMessage,
} from '@/lib/api/message';
import { getToken } from '@/lib/api/request';
import { Stack, useRouter } from 'expo-router';
import { Bell, CheckCheck, Headset, Megaphone, MessageCircle, Trash2 } from 'lucide-react-native';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, View } from 'react-native';
import { useFocusEffect } from '@react-navigation/native';

const MESSAGE_POLL_MS = 10_000;

const TAB_FILTERS = [
  { key: 'all', label: '全部' },
  { key: 'system', label: '系统通知', role: MessageSenderRole.System },
  { key: 'agent', label: '客服回复', role: MessageSenderRole.Agent },
  { key: 'my', label: '我的留言', role: MessageSenderRole.Customer },
] as const;

const ROLE_META = {
  [MessageSenderRole.System]: {
    icon: Megaphone,
    color: '#9b5cff',
    bg: '#241d39',
    title: '系统消息',
  },
  [MessageSenderRole.Agent]: {
    icon: Headset,
    color: '#4ade80',
    bg: '#172b26',
    title: '客服消息',
  },
  [MessageSenderRole.Customer]: {
    icon: MessageCircle,
    color: '#ff7e93',
    bg: '#3a1f29',
    title: '我的留言',
  },
};

function formatNotifTime(dateStr: string) {
  const date = new Date(dateStr);
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const target = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  const diffDays = (today.getTime() - target.getTime()) / (1000 * 60 * 60 * 24);
  const h = String(date.getHours()).padStart(2, '0');
  const m = String(date.getMinutes()).padStart(2, '0');

  if (diffDays === 0) return `今天 ${h}:${m}`;
  if (diffDays === 1) return `昨天 ${h}:${m}`;
  if (diffDays < 7) return `${Math.floor(diffDays)}天前`;
  return `${date.getMonth() + 1}月${date.getDate()}日`;
}

export default function MessagesScreen() {
  const router = useRouter();
  const [activeTab, setActiveTab] = useState('all');
  const [messages, setMessages] = useState<DMessage[]>([]);
  const [loading, setLoading] = useState(true);

  const loadData = useCallback(async (options?: { silent?: boolean }) => {
    const silent = options?.silent ?? false;
    const token = await getToken();
    if (!token) return;

    if (!silent) setLoading(true);
    try {
      const result = await getMessages();
      if (result.success && result.data) {
        setMessages(result.data);
      }
    } finally {
      if (!silent) setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  useFocusEffect(
    useCallback(() => {
      const timer = setInterval(() => {
        void loadData({ silent: true });
      }, MESSAGE_POLL_MS);
      return () => clearInterval(timer);
    }, [loadData])
  );

  const filtered = useMemo(() => {
    if (activeTab === 'all') return messages;
    const target = TAB_FILTERS.find((tab) => tab.key === activeTab);
    if (!target || !('role' in target)) return messages;
    return messages.filter((message) => message.senderRole === target.role);
  }, [activeTab, messages]);

  const unreadCount = useMemo(
    () =>
      messages.filter(
        (message) =>
          message.status === MessageStatus.未读 &&
          message.senderRole !== MessageSenderRole.Customer &&
          message.senderRole !== MessageSenderRole.System
      ).length,
    [messages]
  );

  const handleMarkAllRead = async () => {
    const token = await getToken();
    if (!token) return;
    setMessages((prev) => prev.map((item) => ({ ...item, status: MessageStatus.已读 })));
    await markAllAsRead();
  };

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51InnerPage
        title="消息通知"
        subtitle="系统公告、客服回复与留言记录统一汇总。"
        tag={unreadCount > 0 ? `${unreadCount}条未读` : '已读完'}
        tone="blue"
        hideHero>
        <Pg51InnerPageTopBar
          onBack={() => router.back()}
          icon={Bell}
          iconColor="#4ea3ff"
          title="消息通知"
          subtitle="系统公告、客服回复与留言记录统一汇总。"
          tone="blue"
        />

        {unreadCount > 0 ? (
          <View className="flex-row justify-end">
            <Pressable
              onPress={handleMarkAllRead}
              className="flex-row items-center gap-2 rounded-full bg-[#6f1dff] px-4 py-2.5">
              <Icon as={CheckCheck} size={15} className="text-white" />
              <Text className="text-[12px] font-bold text-white">全部已读</Text>
            </Pressable>
          </View>
        ) : null}

        <View className="flex-row gap-3">
          <StatCard label="未读消息" count={unreadCount} color="#ff7e93" bg="#3a1f29" icon={Bell} />
          <StatCard
            label="系统公告"
            count={messages.filter((item) => item.senderRole === MessageSenderRole.System).length}
            color="#9b5cff"
            bg="#241d39"
            icon={Megaphone}
          />
          <StatCard
            label="客服回复"
            count={
              messages.filter(
                (item) =>
                  item.senderRole === MessageSenderRole.Agent && item.status === MessageStatus.未读
              ).length
            }
            color="#4ade80"
            bg="#172b26"
            icon={Headset}
          />
        </View>

        <Pg51SectionCard
          title="消息列表"
          description="可按消息类型筛选查看对应内容。"
          right={
            <Pressable
              onPress={() => router.push('/chat')}
              className="flex-row items-center gap-1.5 rounded-full bg-[#6f1dff] px-3 py-2">
              <Icon as={Headset} size={13} color="#ffffff" />
              <Text className="text-[11px] font-bold text-white">联系客服</Text>
            </Pressable>
          }>
          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            contentContainerStyle={{ gap: 8 }}>
            {TAB_FILTERS.map((tab) => {
              const active = activeTab === tab.key;
              return (
                <Pressable
                  key={tab.key}
                  onPress={() => setActiveTab(tab.key)}
                  className="rounded-full border px-3.5 py-2"
                  style={{
                    borderColor: active ? '#9b5cff' : '#414a61',
                    backgroundColor: active ? '#6f1dff' : '#222a3a',
                  }}>
                  <Text
                    className="text-[12px] font-semibold"
                    style={{ color: active ? '#ffffff' : '#b7c0d6' }}>
                    {tab.label}
                  </Text>
                </Pressable>
              );
            })}
          </ScrollView>

          {loading ? (
            <View className="items-center py-8">
              <ActivityIndicator color="#4ea3ff" />
              <Text className="mt-3 text-[14px] text-[#cdd5e6]">消息加载中...</Text>
            </View>
          ) : filtered.length === 0 ? (
            <Text className="text-center text-[13px] leading-[22px] text-[#9fa8be]">
              当前分类下暂无消息。
            </Text>
          ) : (
            <View className="gap-3">
              {filtered.map((item) => (
                <MessageRow
                  key={item.id}
                  item={item}
                  onRead={(id) => {
                    setMessages((prev) =>
                      prev.map((message) =>
                        message.id === id ? { ...message, status: MessageStatus.已读 } : message
                      )
                    );
                    void markAsRead(id);
                  }}
                  onDelete={async (id) => {
                    const result = await deleteMessage(id);
                    if (result.success) {
                      setMessages((prev) => prev.filter((entry) => entry.id !== id));
                    } else {
                      Toast.show({
                        type: 'error',
                        text1: '提示',
                        text2: result.message || '删除失败',
                      });
                    }
                  }}
                />
              ))}
            </View>
          )}
        </Pg51SectionCard>
      </Pg51InnerPage>
    </>
  );
}

function StatCard({
  label,
  count,
  color,
  bg,
  icon,
}: {
  label: string;
  count: number;
  color: string;
  bg: string;
  icon: any;
}) {
  return (
    <View className="flex-1 items-center rounded-[22px] bg-[#171d2a] px-3 py-4">
      <Pg51LucideIconBadge icon={icon} iconSize={17} />
      <Text className="mt-3 text-[22px] font-black" style={{ color }}>
        {count}
      </Text>
      <Text className="mt-1 text-center text-[11px] text-[#8f9ab2]">{label}</Text>
    </View>
  );
}

function MessageRow({
  item,
  onRead,
  onDelete,
}: {
  item: DMessage;
  onRead?: (id: number) => void;
  onDelete?: (id: number) => void;
}) {
  const meta = ROLE_META[item.senderRole] || ROLE_META[MessageSenderRole.System];
  const isUnread =
    item.status === MessageStatus.未读 &&
    item.senderRole !== MessageSenderRole.Customer &&
    item.senderRole !== MessageSenderRole.System;

  return (
    <Pressable
      className="flex-row gap-3 rounded-[20px] bg-[#212838] px-4 py-3"
      onPress={() => {
        if (isUnread && onRead) onRead(item.id);
      }}>
      <View style={{ position: 'relative' }}>
        <Pg51LucideIconBadge icon={meta.icon} />
        {isUnread ? (
          <View
            style={{
              position: 'absolute',
              top: -2,
              right: -2,
              width: 10,
              height: 10,
              borderRadius: 5,
              backgroundColor: '#ff5fa2',
              borderWidth: 2,
              borderColor: '#212838',
            }}
          />
        ) : null}
      </View>

      <View className="flex-1">
        <View className="flex-row items-center">
          <Text className="flex-1 text-[14px] font-bold text-white">{meta.title}</Text>
          <Text className="ml-2 text-[10px] text-[#8f9ab2]">{formatNotifTime(item.sentAt)}</Text>
          {item.senderRole !== MessageSenderRole.System && onDelete ? (
            <Pressable onPress={() => onDelete(item.id)} className="ml-2 p-1">
              <Icon as={Trash2} size={13} color="#ff7e93" />
            </Pressable>
          ) : null}
        </View>
        <Text className="mt-2 text-[12px] leading-[19px] text-[#b7c0d6]">{item.content}</Text>
      </View>
    </Pressable>
  );
}
