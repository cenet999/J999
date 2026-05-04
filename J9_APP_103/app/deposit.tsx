import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Pg51InnerPage, Pg51SectionCard } from '@/components/pg51-clone/page-ui';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import { createRechargeOrderAndOpenPayUrl } from '@/lib/api/transaction';
import { Stack } from 'expo-router';
import { Check, Coins, ShieldCheck } from 'lucide-react-native';
import { useState } from 'react';
import { ActivityIndicator, Linking, Pressable, TextInput, View } from 'react-native';

const manualRechargeUrl = 'https://item.taobao.com/item.htm?id=902865679763&skuId=6222234890286';

const CUSTOM_AMOUNT = 'custom';
const amountOptions = ['500', '1000', '2000', '5000', '10000', CUSTOM_AMOUNT];
const channelCards = [
  {
    key: 'usdt',
    title: 'USDT-TRC20',
    description: '生成支付页，按指引转账',
    badge: '推荐',
    tone: 'gold' as const,
    icon: Coins,
  },
  {
    key: 'manual',
    title: '专属通道',
    description: '快速跳转支付页面',
    badge: '便捷',
    tone: 'purple' as const,
    icon: ShieldCheck,
  },
];
export default function DepositScreen() {
  const [selectedAmount, setSelectedAmount] = useState<string>(amountOptions[0]);
  const [customAmount, setCustomAmount] = useState('');
  const [selectedChannel, setSelectedChannel] = useState(channelCards[0].key);
  const [submitting, setSubmitting] = useState(false);

  const isCustomSelected = selectedAmount === CUSTOM_AMOUNT;
  const effectiveAmount = isCustomSelected ? customAmount.trim() : selectedAmount;

  const handleCustomAmountChange = (text: string) => {
    setCustomAmount(text.replace(/[^0-9]/g, ''));
  };

  const handleRecharge = async () => {
    const amount = Number(effectiveAmount);
    if (!Number.isFinite(amount) || amount <= 0) {
      Toast.show({ type: 'error', text1: '金额无效', text2: '请选择正确的充值金额。' });
      return;
    }

    if (amount < 2) {
      Toast.show({ type: 'error', text1: '金额过低', text2: '充值金额不能低于 2 元。' });
      return;
    }

    if (selectedChannel === 'manual') {
      try {
        await Linking.openURL(manualRechargeUrl);
      } catch (error) {
        console.error('打开链接失败:', error);
        Toast.show({
          type: 'error',
          text1: '打开失败',
          text2: '支付页面暂时无法打开，请稍后重试。',
        });
      }
      return;
    }

    setSubmitting(true);

    try {
      const { ok, message } = await createRechargeOrderAndOpenPayUrl(amount);
      if (!ok) {
        Toast.show({
          type: 'error',
          text1: message || '充值申请提交失败，请稍后重试。',
        });
        return;
      }

      Toast.show({
        type: 'success',
        text1: '已打开支付页面',
        text2: '请根据页面指引完成充值流程。',
      });
    } catch (error) {
      console.error('充值请求失败:', error);
      Toast.show({
        type: 'error',
        text1: '网络异常',
        text2: '充值请求提交失败，请稍后重试。',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51InnerPage
        title="存款中心"
        subtitle="选金额与通道即可支付"
        tag="安全到账"
        tone="gold"
        hideHero>
        <View className="mb-4 rounded-[28px] border border-[#4f3a80] bg-[#171d2a] px-4 py-4">
          <View className="flex-row items-center justify-between gap-3">
            <View className="flex-1">
              <Text className="text-[20px] font-black text-white">存款中心</Text>
              <Text className="mt-1 text-[12px] leading-[19px] text-[#97a1b8]">
                选金额与通道即可支付
              </Text>
            </View>

            <Pg51LucideIconBadge icon={Coins} size={44} iconSize={18} radius={18} />
          </View>
        </View>

        <Pg51SectionCard>
          <View className="gap-4">
            <View className="gap-2">
              <Text className="text-[13px] font-semibold text-[#9fa8be]">选择金额</Text>
              <View className="flex-row flex-wrap gap-3">
                {amountOptions.map((item) => (
                  <AmountOption
                    key={item}
                    amount={item}
                    active={selectedAmount === item}
                    onPress={() => setSelectedAmount(item)}
                  />
                ))}
              </View>

              {isCustomSelected ? (
                <View className="mt-1 gap-2">
                  <Text className="text-[13px] font-semibold text-white">自定义金额</Text>
                  <TextInput
                    value={customAmount}
                    onChangeText={handleCustomAmountChange}
                    placeholder="请输入充值金额"
                    placeholderTextColor="#8f9ab2"
                    keyboardType="number-pad"
                    maxLength={7}
                    className="rounded-[18px] border px-4 py-3 text-[14px] font-semibold text-white"
                    style={{
                      backgroundColor: '#212838',
                      borderColor: '#b79249',
                    }}
                  />
                </View>
              ) : null}
            </View>

            <View className="gap-2">
              <Text className="text-[13px] font-semibold text-[#9fa8be]">充值方式</Text>
              <View className="gap-3">
                {channelCards.map((item) => (
                  <ChannelOption
                    key={item.key}
                    title={item.title}
                    description={item.description}
                    badge={item.badge}
                    icon={item.icon}
                    active={selectedChannel === item.key}
                    onPress={() => setSelectedChannel(item.key)}
                  />
                ))}
              </View>
            </View>

            <Pressable
              onPress={handleRecharge}
              disabled={submitting}
              className="rounded-[22px] bg-[#6f1dff] px-4 py-4 active:opacity-90"
              style={{ opacity: submitting ? 0.75 : 1 }}>
              {submitting ? (
                <ActivityIndicator color="#ffffff" />
              ) : (
                <Text className="text-center text-[15px] font-black text-white">
                  确认充值 ¥{effectiveAmount || '0'}
                </Text>
              )}
              <Text className="mt-1 text-center text-[11px] text-[#d9cbff]">
                {submitting
                  ? '正在跳转支付页面，请稍等...'
                  : `当前方式：${channelCards.find((item) => item.key === selectedChannel)?.title}`}
              </Text>
            </Pressable>
          </View>
        </Pg51SectionCard>
      </Pg51InnerPage>
    </>
  );
}

function AmountOption({
  amount,
  active,
  onPress,
}: {
  amount: string;
  active: boolean;
  onPress: () => void;
}) {
  const isCustom = amount === CUSTOM_AMOUNT;

  return (
    <Pressable
      onPress={onPress}
      className="min-w-[31%] flex-1 rounded-[20px] border px-3 py-4 active:opacity-90"
      style={{
        borderColor: active ? '#b79249' : '#39435a',
        backgroundColor: active ? '#2d2618' : '#212838',
      }}>
      <Text className="text-center text-[18px] font-black text-white">
        {isCustom ? '自定义' : `¥${amount}`}
      </Text>
      <Text
        className="mt-1 text-center text-[11px] font-medium"
        style={{ color: active ? '#f6c453' : '#8f9ab2' }}>
        {active ? '已选择' : isCustom ? '自定义金额' : '可选金额'}
      </Text>
    </Pressable>
  );
}

function ChannelOption({
  title,
  description,
  badge,
  icon,
  active,
  onPress,
}: {
  title: string;
  description: string;
  badge: string;
  icon: typeof Coins;
  active: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      onPress={onPress}
      className="rounded-[22px] border px-4 py-4 active:opacity-90"
      style={{
        borderColor: active ? '#6f1dff' : '#39435a',
        backgroundColor: active ? '#241c38' : '#212838',
      }}>
      <View className="flex-row items-center gap-3">
        <Pg51LucideIconBadge icon={icon} active={active} size={44} iconSize={20} />

        <View className="flex-1">
          <View className="flex-row items-center gap-2">
            <Text className="text-[15px] font-bold text-white">{title}</Text>
            <View className="rounded-full bg-[#2d2618] px-2 py-1">
              <Text className="text-[10px] font-bold text-[#f6c453]">{badge}</Text>
            </View>
          </View>
          <Text className="mt-1 text-[12px] leading-[19px] text-[#9fa8be]">{description}</Text>
        </View>

        <View
          className="size-6 items-center justify-center rounded-full border"
          style={{
            borderColor: active ? '#9b5cff' : '#586179',
            backgroundColor: active ? '#6f1dff' : 'transparent',
          }}>
          {active ? <Icon as={Check} size={14} className="text-white" /> : null}
        </View>
      </View>
    </Pressable>
  );
}
