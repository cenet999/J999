import * as Clipboard from 'expo-clipboard';
import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Pg51InnerPageTopBar } from '@/components/pg51-clone/inner-page-top-bar';
import { Pg51InnerPage, Pg51SectionCard, Pg51StatCard } from '@/components/pg51-clone/page-ui';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import { getInviteCenter, type InviteCenterData } from '@/lib/api/invite';
import { useTenantTitle } from '@/lib/api/tenant';
import { formatCny } from '@/lib/format-money';
import { buildHomeInviteLink } from '@/lib/register-link';
import { Stack, useFocusEffect, useRouter } from 'expo-router';
import { Calendar, Copy, Gift, Share2, Trophy, Users } from 'lucide-react-native';
import { useCallback, useState } from 'react';
import type { ReactNode } from 'react';
import {
  ActivityIndicator,
  Platform,
  Pressable,
  RefreshControl,
  ScrollView,
  Share,
  View,
} from 'react-native';

function pickText(value: unknown) {
  return typeof value === 'string' ? value : '';
}

function pickNumber(value: unknown) {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string') {
    const result = Number.parseFloat(value);
    return Number.isFinite(result) ? result : 0;
  }
  return 0;
}

function normalizeCenter(raw: InviteCenterData | null | undefined) {
  return {
    agentId: pickNumber(raw?.agentId ?? raw?.AgentId),
    agentName: pickText(raw?.agentName ?? raw?.AgentName),
    inviteCode: pickText(raw?.inviteCode ?? raw?.InviteCode),
    totalInvites: pickNumber(raw?.totalInvites ?? raw?.TotalInvites),
    todayInvites: pickNumber(raw?.todayInvites ?? raw?.TodayInvites),
    totalInviteTaskReward: pickNumber(raw?.totalInviteTaskReward ?? raw?.TotalInviteTaskReward),
    myRank: pickNumber(raw?.myRank ?? raw?.MyRank),
    myInviteCount: pickNumber(raw?.myInviteCount ?? raw?.MyInviteCount),
    records: Array.isArray(raw?.records)
      ? raw?.records
      : Array.isArray(raw?.Records)
        ? raw?.Records
        : [],
    leaderboard: Array.isArray(raw?.leaderboard)
      ? raw?.leaderboard
      : Array.isArray(raw?.Leaderboard)
        ? raw?.Leaderboard
        : [],
  };
}

function formatDateTimeFull(text: string) {
  if (!text) return '—';
  const date = new Date(text.includes('T') ? text : text.replace(' ', 'T'));
  if (Number.isNaN(date.getTime())) return text;
  const h = String(date.getHours()).padStart(2, '0');
  const m = String(date.getMinutes()).padStart(2, '0');
  return `${date.getFullYear()}-${date.getMonth() + 1}-${date.getDate()} ${h}:${m}`;
}

export default function InviteFriendsScreen() {
  const router = useRouter();
  const tenantTitle = useTenantTitle();
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [data, setData] = useState<ReturnType<typeof normalizeCenter> | null>(null);

  const load = useCallback(async () => {
    const result = await getInviteCenter();
    if (!result.success || !result.data) {
      setData(null);
      if (result.message) Toast.show({ type: 'error', text1: result.message });
      return;
    }
    setData(normalizeCenter(result.data));
  }, []);

  useFocusEffect(
    useCallback(() => {
      let cancelled = false;
      (async () => {
        setLoading(true);
        try {
          await load();
        } finally {
          if (!cancelled) setLoading(false);
        }
      })();
      return () => {
        cancelled = true;
      };
    }, [load])
  );

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  }, [load]);

  const inviteCode = data?.inviteCode ?? '';
  const inviteLink = buildHomeInviteLink(
    inviteCode,
    data?.agentId || undefined,
    data?.agentName || undefined
  );

  const copyInviteCode = async () => {
    if (!inviteCode) {
      Toast.show({ type: 'info', text1: '当前没有邀请码' });
      return;
    }
    await Clipboard.setStringAsync(inviteCode);
    Toast.show({ type: 'success', text1: '邀请码已复制' });
  };

  const copyInviteLink = async () => {
    if (!inviteLink) {
      Toast.show({ type: 'info', text1: '当前没有邀请链接' });
      return;
    }
    await Clipboard.setStringAsync(inviteLink);
    Toast.show({ type: 'success', text1: '邀请链接已复制' });
  };

  const shareInvite = async () => {
    if (!inviteLink) {
      Toast.show({ type: 'info', text1: '当前没有邀请链接' });
      return;
    }

    try {
      await Share.share({
        message: `加入${tenantTitle}，使用我的邀请链接注册：${inviteLink}`,
        url: Platform.OS === 'ios' ? inviteLink : undefined,
      });
    } catch {}
  };

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51InnerPage
        title="邀请好友"
        subtitle="邀请码、奖励统计、排行榜与邀请记录统一展示。"
        tag="邀请返利"
        tone="purple"
        hideHero>
        <Pg51InnerPageTopBar
          onBack={() => router.back()}
          icon={Users}
          iconColor="#9b5cff"
          title="邀请好友"
          subtitle="邀请码、奖励统计、排行榜与邀请记录统一展示。"
          tone="purple"
        />

        {loading ? (
          <LoadingCard text="邀请数据加载中..." />
        ) : !data ? (
          <ErrorCard text="邀请数据加载失败，请下拉刷新后重试。" onRefresh={onRefresh} />
        ) : (
          <>
            <Pg51SectionCard
              title="我的邀请码"
              description="好友通过邀请码或邀请链接注册后，将计入您的邀请记录。">
              <View className="flex-row items-center justify-between rounded-[22px] border border-[#4f3a80] bg-[#221b35] px-4 py-4">
                <View>
                  <Text className="text-[11px] font-semibold text-[#9fa8be]">邀请码</Text>
                  <Text className="mt-1 text-[26px] font-black tracking-[3px] text-white">
                    {inviteCode || '—'}
                  </Text>
                </View>

                <Pressable
                  onPress={copyInviteCode}
                  className="flex-row items-center gap-2 rounded-full bg-[#6f1dff] px-3 py-2">
                  <Icon as={Copy} size={15} className="text-white" />
                  <Text className="text-[12px] font-bold text-white">复制</Text>
                </Pressable>
              </View>

              <View className="flex-row gap-3">
                <ActionButton icon={Copy} label="复制链接" onPress={copyInviteLink} />
                <ActionButton icon={Share2} label="分享邀请" onPress={shareInvite} tone="pink" />
              </View>
            </Pg51SectionCard>

            <Pg51SectionCard
              title="奖励统计"
              description="累计任务奖励，就是邀请活动已经发给你的总金额。">
              <View className="flex-row gap-3">
                <Pg51StatCard
                  icon={Users}
                  label="累计邀请"
                  value={String(data.totalInvites)}
                  hint="总人数"
                  tone="purple"
                />
                <Pg51StatCard
                  icon={Gift}
                  label="今日新增"
                  value={String(data.todayInvites)}
                  hint="今天新增"
                  tone="blue"
                />
              </View>
              <View className="rounded-[22px] bg-[#172b26] px-4 py-4">
                <Text className="text-[12px] font-semibold text-[#9fd7bb]">累计任务奖励</Text>
                <Text className="mt-2 text-[26px] font-black text-[#4ade80]">
                  {formatCny(data.totalInviteTaskReward)}
                </Text>
              </View>
            </Pg51SectionCard>

            <Pg51SectionCard
              title="邀请排行榜"
              description="按直属邀请人数进行排名展示。">
              <View className="rounded-[20px] bg-[#2d2618] px-4 py-3">
                <Text className="text-[12px] font-semibold text-[#d3c299]">
                  我的排名：
                  {data.myInviteCount <= 0
                    ? ' 暂无邀请'
                    : data.myRank > 0
                      ? ` 第 ${data.myRank} 名`
                      : ' —'}
                </Text>
                <Text className="mt-1 text-[16px] font-black text-[#f6c453]">
                  已邀请 {data.myInviteCount} 人
                </Text>
              </View>

              {data.leaderboard.length === 0 ? (
                <EmptyText text="暂无排行榜数据。" />
              ) : (
                data.leaderboard.map((row: any, index) => {
                  const rank = pickNumber(row.rank ?? row.Rank) || index + 1;
                  const name = pickText(row.displayName ?? row.DisplayName) || '—';
                  const count = pickNumber(row.inviteCount ?? row.InviteCount);
                  const isMe = Boolean(row.isCurrentUser ?? row.IsCurrentUser);
                  const medal =
                    rank === 1 ? '🥇' : rank === 2 ? '🥈' : rank === 3 ? '🥉' : String(rank);

                  return (
                    <View
                      key={`${rank}-${name}`}
                      className="flex-row items-center justify-between rounded-[20px] bg-[#212838] px-4 py-3">
                      <View className="flex-row items-center gap-3">
                        <View className="w-8 items-center">
                          <Text className="text-[15px] font-black text-white">{medal}</Text>
                        </View>
                        <View>
                          <Text
                            className="text-[14px] font-bold"
                            style={{ color: isMe ? '#9b5cff' : '#ffffff' }}>
                            {name}
                            {isMe ? '（我）' : ''}
                          </Text>
                          <Text className="mt-1 text-[11px] text-[#8f9ab2]">邀请人数</Text>
                        </View>
                      </View>

                      <Text className="text-[18px] font-black text-[#f6c453]">{count}</Text>
                    </View>
                  );
                })
              )}
            </Pg51SectionCard>

            <Pg51SectionCard
              title="邀请记录"
              description="展示最近的好友注册记录，账号做了简单隐藏。">
              {data.records.length === 0 ? (
                <EmptyText text="还没有朋友通过你的邀请注册。" />
              ) : (
                data.records.map((record: any, index) => {
                  const displayName = pickText(record.displayName ?? record.DisplayName) || '—';
                  const time = pickText(record.registeredAt ?? record.RegisteredAt);
                  return (
                    <View
                      key={`${time}-${index}`}
                      className="flex-row items-center justify-between rounded-[20px] bg-[#212838] px-4 py-3">
                      <View className="flex-1 pr-2">
                        <Text className="text-[14px] font-bold text-white">{displayName}</Text>
                        <Text className="mt-1 text-[11px] leading-[18px] text-[#8f9ab2]">
                          注册时间 {formatDateTimeFull(time)}
                        </Text>
                      </View>
                      <Pg51LucideIconBadge icon={Calendar} />
                    </View>
                  );
                })
              )}
            </Pg51SectionCard>
          </>
        )}

        <ScrollView
          className="hidden"
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
        />
      </Pg51InnerPage>
    </>
  );
}

function ActionButton({
  icon,
  label,
  onPress,
  tone = 'purple',
}: {
  icon: any;
  label: string;
  onPress: () => void;
  tone?: 'purple' | 'pink';
}) {
  const bg = tone === 'pink' ? '#d14d72' : '#6f1dff';

  return (
    <Pressable
      onPress={onPress}
      className="flex-1 flex-row items-center justify-center gap-2 rounded-[20px] px-4 py-3"
      style={{ backgroundColor: bg }}>
      <Icon as={icon} size={16} className="text-white" />
      <Text className="text-[13px] font-bold text-white">{label}</Text>
    </Pressable>
  );
}

function LoadingCard({ text }: { text: string }) {
  return (
    <View className="items-center rounded-[24px] border border-[#3c4560] bg-[#171d2a] py-10">
      <ActivityIndicator color="#9b5cff" />
      <Text className="mt-3 text-[14px] text-[#cdd5e6]">{text}</Text>
    </View>
  );
}

function ErrorCard({ text, onRefresh }: { text: string; onRefresh: () => void }) {
  return (
    <View className="rounded-[24px] border border-[#5a2f3d] bg-[#2a1b23] px-4 py-5">
      <Text className="text-[14px] leading-[22px] text-[#ffb8c8]">{text}</Text>
      <Pressable
        onPress={onRefresh}
        className="mt-4 self-start rounded-full bg-[#6f1dff] px-4 py-2">
        <Text className="text-[12px] font-bold text-white">重新加载</Text>
      </Pressable>
    </View>
  );
}

function EmptyText({ text }: { text: string }) {
  return <Text className="text-center text-[13px] leading-[22px] text-[#9fa8be]">{text}</Text>;
}
