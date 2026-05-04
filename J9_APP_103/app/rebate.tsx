import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Pg51InnerPageTopBar } from '@/components/pg51-clone/inner-page-top-bar';
import { Pg51InnerPage, Pg51InfoRow, Pg51SectionCard } from '@/components/pg51-clone/page-ui';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import { getMemberInfo } from '@/lib/api/auth';
import { formatCny } from '@/lib/format-money';
import { getTransactionList, playerRebate, type TransactionRecord } from '@/lib/api/transaction';
import { Stack, useFocusEffect, useRouter } from 'expo-router';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, View } from 'react-native';
import { useCallback, useState } from 'react';
import { Percent, RefreshCw } from 'lucide-react-native';

const REBATE_TX_TYPE = 11;

function pickNumber(value: unknown) {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string') {
    const result = Number.parseFloat(value);
    return Number.isFinite(result) ? result : 0;
  }
  return 0;
}

function pickText(value: unknown) {
  return typeof value === 'string' ? value : '';
}

function formatTime(value: unknown) {
  if (typeof value === 'number' && Number.isFinite(value)) {
    const ms = value < 1e12 ? value * 1000 : value;
    const date = new Date(ms);
    if (!Number.isNaN(date.getTime())) {
      const h = String(date.getHours()).padStart(2, '0');
      const m = String(date.getMinutes()).padStart(2, '0');
      return `${date.getFullYear()}-${date.getMonth() + 1}-${date.getDate()} ${h}:${m}`;
    }
  }
  const text = pickText(value);
  if (!text) return '—';
  const date = new Date(text.includes('T') ? text : text.replace(' ', 'T'));
  if (Number.isNaN(date.getTime())) return text;
  const h = String(date.getHours()).padStart(2, '0');
  const m = String(date.getMinutes()).padStart(2, '0');
  return `${date.getFullYear()}-${date.getMonth() + 1}-${date.getDate()} ${h}:${m}`;
}

export default function RebateScreen() {
  const router = useRouter();
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [username, setUsername] = useState('');
  const [rebateBase, setRebateBase] = useState(0);
  const [rebateEstimate, setRebateEstimate] = useState(0);
  const [records, setRecords] = useState<TransactionRecord[]>([]);

  const loadAll = useCallback(async () => {
    const infoResult = await getMemberInfo();
    if (infoResult.success && infoResult.data) {
      setUsername(String(infoResult.data.Username ?? infoResult.data.username ?? ''));
      setRebateBase(
        pickNumber(infoResult.data.RebateTotalAmount ?? infoResult.data.rebateTotalAmount)
      );
      setRebateEstimate(pickNumber(infoResult.data.RebateAmount ?? infoResult.data.rebateAmount));
    } else {
      setUsername('');
      setRebateBase(0);
      setRebateEstimate(0);
    }

    const recordResult = await getTransactionList(1, 50, REBATE_TX_TYPE);
    setRecords(recordResult.success && recordResult.data ? recordResult.data : []);
  }, []);

  useFocusEffect(
    useCallback(() => {
      let cancelled = false;
      (async () => {
        setLoading(true);
        try {
          await loadAll();
        } finally {
          if (!cancelled) setLoading(false);
        }
      })();
      return () => {
        cancelled = true;
      };
    }, [loadAll])
  );

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await loadAll();
    } finally {
      setRefreshing(false);
    }
  }, [loadAll]);

  const onApplyRebate = async () => {
    if (!username) {
      Toast.show({ type: 'error', text1: '请先登录' });
      return;
    }
    if (rebateEstimate <= 0) {
      Toast.show({ type: 'info', text1: '当前暂无可结算返水' });
      return;
    }

    setSubmitting(true);
    try {
      const result = await playerRebate(username);
      if (result.success) {
        Toast.show({ type: 'success', text1: '返水已结算到账' });
        await loadAll();
      } else {
        Toast.show({ type: 'error', text1: result.message || '申请失败，请稍后再试' });
      }
    } catch {
      Toast.show({ type: 'error', text1: '网络异常，请稍后再试' });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51InnerPage
        title="返水中心"
        subtitle="返水查询与结算"
        tag="实时结算"
        tone="blue"
        hideHero>
        <Pg51InnerPageTopBar
          onBack={() => router.back()}
          icon={Percent}
          iconColor="#4ea3ff"
          title="返水中心"
          subtitle="返水查询与结算"
          tone="blue"
        />

        {loading ? (
          <LoadingCard />
        ) : (
          <>
            <Pg51SectionCard
              title="当前可领返水"
              description="按规则计算可领金额">
              <View className="rounded-[22px] border border-[#2f5479] bg-[#172535] px-4 py-4">
                <View className="flex-row items-center gap-3">
                  <Pg51LucideIconBadge icon={Percent} size={44} iconSize={20} />
                  <View className="flex-1">
                    <Text className="text-[14px] font-bold text-white">返水结算</Text>
                    <Text className="mt-1 text-[11px] leading-[18px] text-[#9fa8be]">
                      成功后记录将同步更新。
                    </Text>
                  </View>
                </View>

                <View className="mt-4 gap-3">
                  <Pg51InfoRow label="待结算流水" value={formatCny(rebateBase)} />
                  <Pg51InfoRow
                    label="预计返水"
                    value={formatCny(rebateEstimate)}
                    valueTone="success"
                  />
                </View>

                <Pressable
                  onPress={onApplyRebate}
                  disabled={submitting || rebateEstimate <= 0}
                  className="mt-4 flex-row items-center justify-center gap-2 rounded-[22px] px-4 py-4"
                  style={{
                    backgroundColor: submitting || rebateEstimate <= 0 ? '#3a4256' : '#2563eb',
                  }}>
                  {submitting ? (
                    <ActivityIndicator color="#ffffff" />
                  ) : (
                    <Icon as={RefreshCw} size={18} className="text-white" />
                  )}
                  <Text className="text-[14px] font-black text-white">
                    {rebateEstimate <= 0 ? '暂无可结算返水' : '提交返水申请'}
                  </Text>
                </Pressable>
              </View>
            </Pg51SectionCard>

            <Pg51SectionCard title="最近返水记录" description="近期结算记录">
              {records.length === 0 ? (
                <Text className="text-center text-[13px] text-[#9fa8be]">
                  近 7 天暂无返水记录。
                </Text>
              ) : (
                records.map((record, index) => {
                  const id = String((record as any).id ?? (record as any).Id ?? index);
                  const description =
                    pickText((record as any).description ?? (record as any).Description) ||
                    '返水入账';
                  const actualAmount = pickNumber(
                    (record as any).actualAmount ?? (record as any).ActualAmount
                  );
                  const time = formatTime(
                    (record as any).transactionTime ??
                      (record as any).TransactionTime ??
                      (record as any).createdTime ??
                      (record as any).CreatedTime
                  );

                  return (
                    <View
                      key={id}
                      className="flex-row items-center justify-between rounded-[20px] bg-[#212838] px-4 py-3">
                      <View className="flex-1 pr-3">
                        <Text className="text-[14px] font-bold text-white">{description}</Text>
                        <Text className="mt-1 text-[11px] text-[#8f9ab2]">{time}</Text>
                      </View>
                      <Text className="text-[16px] font-black text-[#4ade80]">
                        +{formatCny(actualAmount)}
                      </Text>
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

function LoadingCard() {
  return (
    <View className="items-center rounded-[24px] border border-[#3c4560] bg-[#171d2a] py-10">
      <ActivityIndicator color="#4ea3ff" />
      <Text className="mt-3 text-[14px] text-[#cdd5e6]">返水数据加载中...</Text>
    </View>
  );
}
