import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Pg51InnerPage, Pg51SectionCard } from '@/components/pg51-clone/page-ui';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import {
  createRechargeOrderAndOpenPayUrl,
  getPayApiList,
  type PayApiChannel,
} from '@/lib/api/transaction';
import { Stack } from 'expo-router';
import { Check, Coins, RefreshCcw } from 'lucide-react-native';
import type { ComponentType } from 'react';
import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, TextInput, View } from 'react-native';
import Svg, { Path } from 'react-native-svg';

const CUSTOM_AMOUNT = 'custom';

type DepositChannel = {
  key: string;
  payApiId: string;
  ip: string;
  title: string;
  description: string;
  badge: string;
  icon: ChannelIconComponent;
  iconColor: string;
  iconBg: string;
  minAmount: number;
  maxAmount: number;
  isUserInput: boolean;
  sort: number;
  amountOptions: string[];
};

type ChannelIconProps = {
  size?: number;
  color?: string;
};

type ChannelIconComponent = ComponentType<ChannelIconProps>;

type BrandIconSpec = {
  viewBox: string;
  path: string;
};

const BRAND_ICONS = {
  alipay: {
    viewBox: '0 0 24 24',
    path: 'M19.695 15.07c3.426 1.158 4.203 1.22 4.203 1.22V3.846c0-2.124-1.705-3.845-3.81-3.845H3.914C1.808.001.102 1.722.102 3.846v16.31c0 2.123 1.706 3.845 3.813 3.845h16.173c2.105 0 3.81-1.722 3.81-3.845v-.157s-6.19-2.602-9.315-4.119c-2.096 2.602-4.8 4.181-7.607 4.181-4.75 0-6.361-4.19-4.112-6.949.49-.602 1.324-1.175 2.617-1.497 2.025-.502 5.247.313 8.266 1.317a16.796 16.796 0 0 0 1.341-3.302H5.781v-.952h4.799V6.975H4.77v-.953h5.81V3.591s0-.409.411-.409h2.347v2.84h5.744v.951h-5.744v1.704h4.69a19.453 19.453 0 0 1-1.986 5.06c1.424.52 2.702 1.011 3.654 1.333m-13.81-2.032c-.596.06-1.71.325-2.321.869-1.83 1.608-.735 4.55 2.968 4.55 2.151 0 4.301-1.388 5.99-3.61-2.403-1.182-4.438-2.028-6.637-1.809',
  },
  tether: {
    viewBox: '0 0 24 24',
    path: 'M18.7538 10.5176c0 .6251-2.2379 1.1483-5.2381 1.2812l.0028.0007c-.0848.0064-.5233.0325-1.5012.0325-.7778 0-1.33-.0233-1.5237-.0325-3.0059-.1322-5.2495-.6555-5.2495-1.2819s2.2436-1.149 5.2495-1.2834v2.0442c.1965.0142.7594.0474 1.5372.0474.9334 0 1.4008-.0389 1.4849-.0466V9.2356c2.9994.1337 5.2381.657 5.2381 1.282zm5.19.5466L12.1248 22.389a.1803.1803 0 0 1-.2496 0L.0562 11.0635a.1781.1781 0 0 1-.0382-.2079l4.3762-9.1921a.1767.1767 0 0 1 .1626-.1026h14.8878a.1768.1768 0 0 1 .1612.1032l4.3762 9.1922a.1782.1782 0 0 1-.0382.2079zm-4.478-.4038c0-.8068-2.5515-1.4799-5.9473-1.6369V7.195h4.186V4.4055H6.3076V7.195h4.1852v1.8286c-3.4018.1562-5.9601.83-5.9601 1.6376 0 .8075 2.5583 1.4806 5.9601 1.6376v5.8618h3.025v-5.8639c3.394-.1563 5.948-.8295 5.948-1.6363z',
  },
} satisfies Record<string, BrandIconSpec>;

function createBrandIcon(spec: BrandIconSpec): ChannelIconComponent {
  return function BrandIcon({ size = 20, color = '#9EA5B8' }: ChannelIconProps) {
    return (
      <Svg width={size} height={size} viewBox={spec.viewBox}>
        <Path d={spec.path} fill={color} />
      </Svg>
    );
  };
}

const AlipayIcon = createBrandIcon(BRAND_ICONS.alipay);
const UsdtIcon = createBrandIcon(BRAND_ICONS.tether);

function toRecord(value: unknown): Record<string, unknown> {
  return value != null && typeof value === 'object' ? (value as Record<string, unknown>) : {};
}

function pickString(obj: Record<string, unknown>, camel: string, pascal: string) {
  const value = obj[camel] ?? obj[pascal];
  return typeof value === 'string' ? value.trim() : value == null ? '' : String(value).trim();
}

function pickNumber(obj: Record<string, unknown>, camel: string, pascal: string, fallback: number) {
  const value = Number(obj[camel] ?? obj[pascal] ?? fallback);
  return Number.isFinite(value) ? value : fallback;
}

function parseAmountOptions(
  defaultValue: string,
  minAmount: number,
  maxAmount: number,
  isUserInput: boolean
) {
  const values = defaultValue
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
    .map((item) => Number(item))
    .filter((item) => Number.isFinite(item) && item >= minAmount && item <= maxAmount)
    .map((item) => String(item));

  const unique = Array.from(new Set(values.length > 0 ? values : [String(minAmount)]));
  return isUserInput ? [...unique, CUSTOM_AMOUNT] : unique;
}

function formatSuccessBadge(successRate: number, fallback: string) {
  if (successRate >= 100) return '稳定';
  if (successRate >= 80) return '推荐';
  if (successRate >= 60) return '良好';
  if (successRate >= 40) return '普通';
  if (successRate > 0) return '繁忙';
  return fallback;
}

function buildDepositChannels(items: PayApiChannel[] | undefined): DepositChannel[] {
  if (!Array.isArray(items)) return [];

  return items
    .map((item, index) => {
      const obj = toRecord(item);
      const ip = pickString(obj, 'ip', 'IP').toUpperCase();
      const isEnabled = Boolean(obj.isEnabled ?? obj.IsEnabled ?? true);
      const isUserInput = Boolean(obj.isUserInput ?? obj.IsUserInput ?? true);
      const minAmount = pickNumber(obj, 'minAmount', 'MinAmount', 2);
      const maxAmount = pickNumber(obj, 'maxAmount', 'MaxAmount', 999999);
      const sort = pickNumber(obj, 'sort', 'Sort', index);
      const successRate = pickNumber(obj, 'successRate', 'SuccessRate', 0);
      const payMethodName = pickString(obj, 'payMethodName', 'PayMethodName');
      const payMethod = pickString(obj, 'payMethod', 'PayMethod');
      const defaultValue = pickString(obj, 'defaultValue', 'DefaultValue');
      const id = pickString(obj, 'id', 'Id') || `${ip}-${index}`;

      if (!isEnabled || (ip !== 'USDT' && ip !== 'POPO')) return null;

      return {
        key: id,
        payApiId: id,
        ip,
        title: payMethodName || (ip === 'POPO' ? 'POPO' : 'USDT'),
        description:
          ip === 'POPO'
            ? `${payMethod || '在线支付'} · ¥${minAmount}-${maxAmount}`
            : `TRC20 转账 · ¥${minAmount}-${maxAmount}`,
        badge: formatSuccessBadge(successRate, ip),
        icon: ip === 'POPO' ? AlipayIcon : UsdtIcon,
        iconColor: ip === 'POPO' ? '#39a8ff' : '#35d6a3',
        iconBg: ip === 'POPO' ? '#173154' : '#123d36',
        minAmount,
        maxAmount,
        isUserInput,
        sort,
        amountOptions: parseAmountOptions(defaultValue, minAmount, maxAmount, isUserInput),
      } satisfies DepositChannel;
    })
    .filter((item): item is DepositChannel => item != null)
    .sort((a, b) => a.sort - b.sort || a.title.localeCompare(b.title));
}

export default function DepositScreen() {
  const [channels, setChannels] = useState<DepositChannel[]>([]);
  const [channelsLoading, setChannelsLoading] = useState(true);
  const [channelsError, setChannelsError] = useState('');
  const [selectedAmount, setSelectedAmount] = useState('');
  const [customAmount, setCustomAmount] = useState('');
  const [selectedChannel, setSelectedChannel] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const selectedChannelInfo = channels.find((item) => item.key === selectedChannel);
  const amountOptions = useMemo(() => selectedChannelInfo?.amountOptions ?? [], [selectedChannelInfo]);
  const isCustomSelected = selectedAmount === CUSTOM_AMOUNT;
  const effectiveAmount = isCustomSelected ? customAmount.trim() : selectedAmount;

  const handleChannelSelect = (channelKey: string) => {
    const channel = channels.find((item) => item.key === channelKey);
    setSelectedChannel(channelKey);
    setSelectedAmount(channel?.amountOptions[0] ?? '');
    setCustomAmount('');
  };

  const loadChannels = async () => {
    setChannelsLoading(true);
    setChannelsError('');

    try {
      const result = await getPayApiList();
      if (!result.success || !Array.isArray(result.data)) {
        setChannels([]);
        setChannelsError(result.message || '支付通道加载失败');
        return;
      }

      const nextChannels = buildDepositChannels(result.data);
      setChannels(nextChannels);
      if (nextChannels.length === 0) {
        setChannelsError('暂无可用支付通道');
      }
    } catch (error) {
      console.error('加载支付通道失败:', error);
      setChannels([]);
      setChannelsError('支付通道加载失败');
    } finally {
      setChannelsLoading(false);
    }
  };

  useEffect(() => {
    void loadChannels();
  }, []);

  useEffect(() => {
    if (channels.length === 0) {
      setSelectedChannel('');
      return;
    }

    if (!channels.some((item) => item.key === selectedChannel)) {
      setSelectedChannel('');
    }
  }, [channels, selectedChannel]);

  useEffect(() => {
    if (selectedAmount && !amountOptions.includes(selectedAmount)) {
      setSelectedAmount('');
      setCustomAmount('');
    }
  }, [amountOptions, selectedAmount]);

  const handleCustomAmountChange = (text: string) => {
    setCustomAmount(text.replace(/[^0-9]/g, ''));
  };

  const handleRecharge = async () => {
    if (!selectedChannelInfo) {
      Toast.show({ type: 'error', text1: '请选择通道', text2: '请先选择可用的充值方式。' });
      return;
    }

    if (!selectedAmount) {
      Toast.show({ type: 'error', text1: '请选择金额', text2: '请先选择充值金额。' });
      return;
    }

    const amount = Number(effectiveAmount);
    const selectedAmountIsPreset =
      selectedAmount !== CUSTOM_AMOUNT && selectedChannelInfo.amountOptions.includes(selectedAmount);
    if (!Number.isFinite(amount) || amount <= 0) {
      Toast.show({ type: 'error', text1: '金额无效', text2: '请选择正确的充值金额。' });
      return;
    }

    if (!selectedChannelInfo.isUserInput && !selectedAmountIsPreset) {
      Toast.show({
        type: 'error',
        text1: '金额不可用',
        text2: '当前通道仅支持选择固定金额。',
      });
      return;
    }

    if (amount < selectedChannelInfo.minAmount) {
      Toast.show({
        type: 'error',
        text1: '金额过低',
        text2: `当前通道最低充值 ${selectedChannelInfo.minAmount} 元。`,
      });
      return;
    }

    if (amount > selectedChannelInfo.maxAmount) {
      Toast.show({
        type: 'error',
        text1: '金额过高',
        text2: `当前通道最高充值 ${selectedChannelInfo.maxAmount} 元。`,
      });
      return;
    }

    setSubmitting(true);

    try {
      const { ok, message } = await createRechargeOrderAndOpenPayUrl(
        amount,
        selectedChannelInfo.payApiId,
        selectedChannelInfo.ip
      );
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
              <Text className="text-[13px] font-semibold text-[#9fa8be]">充值方式</Text>
              <View className="gap-3">
                {channelsLoading ? (
                  <View className="rounded-[22px] border border-[#39435a] bg-[#212838] px-4 py-5">
                    <ActivityIndicator color="#f6c453" />
                    <Text className="mt-2 text-center text-[12px] text-[#9fa8be]">
                      正在加载支付通道...
                    </Text>
                  </View>
                ) : channels.length > 0 ? (
                  channels.map((item) => (
                    <ChannelOption
                      key={item.key}
                      title={item.title}
                      description={item.description}
                      badge={item.badge}
                      icon={item.icon}
                      iconColor={item.iconColor}
                      iconBg={item.iconBg}
                      active={selectedChannel === item.key}
                      onPress={() => handleChannelSelect(item.key)}
                    />
                  ))
                ) : (
                  <Pressable
                    onPress={loadChannels}
                    className="rounded-[22px] border border-[#5d4965] bg-[#212838] px-4 py-5 active:opacity-90">
                    <View className="items-center gap-2">
                      <Icon as={RefreshCcw} size={18} className="text-[#f6c453]" />
                      <Text className="text-center text-[13px] font-bold text-white">
                        {channelsError || '支付通道加载失败'}
                      </Text>
                      <Text className="text-center text-[11px] text-[#9fa8be]">点击重试</Text>
                    </View>
                  </Pressable>
                )}
              </View>
            </View>

            <View className="gap-2">
              <Text className="text-[13px] font-semibold text-[#9fa8be]">选择金额</Text>
              {amountOptions.length > 0 ? (
                <View className="flex-row gap-3">
                  <View className="flex-1 gap-3">
                    {amountOptions
                      .filter((_, index) => index % 3 === 0)
                      .map((item) => (
                        <AmountOption
                          key={item}
                          amount={item}
                          active={selectedAmount === item}
                          onPress={() => setSelectedAmount(item)}
                        />
                      ))}
                  </View>
                  <View className="flex-1 gap-3">
                    {amountOptions
                      .filter((_, index) => index % 3 === 1)
                      .map((item) => (
                        <AmountOption
                          key={item}
                          amount={item}
                          active={selectedAmount === item}
                          onPress={() => setSelectedAmount(item)}
                        />
                      ))}
                  </View>
                  <View className="flex-1 gap-3">
                    {amountOptions
                      .filter((_, index) => index % 3 === 2)
                      .map((item) => (
                        <AmountOption
                          key={item}
                          amount={item}
                          active={selectedAmount === item}
                          onPress={() => setSelectedAmount(item)}
                        />
                      ))}
                  </View>
                </View>
              ) : (
                <View className="rounded-[18px] border border-[#39435a] bg-[#212838] px-4 py-4">
                  <Text className="text-center text-[12px] text-[#9fa8be]">
                    {channelsLoading ? '正在加载支付通道...' : '请先选择充值方式'}
                  </Text>
                </View>
              )}

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

            <Pressable
              onPress={handleRecharge}
              disabled={submitting || channelsLoading || !selectedChannelInfo || !selectedAmount}
              className="rounded-[22px] bg-[#6f1dff] px-4 py-4 active:opacity-90"
              style={{
                opacity: submitting || channelsLoading || !selectedChannelInfo || !selectedAmount ? 0.75 : 1,
              }}>
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
                  : `当前方式：${selectedChannelInfo?.title ?? '暂无可用通道'}`}
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
      className="items-center justify-center rounded-[16px] py-3.5 active:opacity-90"
      style={{
        backgroundColor: active ? '#2d2618' : '#212838',
        borderWidth: 1,
        borderColor: active ? '#b79249' : '#39435a',
      }}>
      <Text className="text-center text-[16px] font-black text-white">
        {isCustom ? '自定义' : `¥${amount}`}
      </Text>
    </Pressable>
  );
}

function ChannelOption({
  title,
  description,
  badge,
  icon: ChannelIcon,
  iconColor,
  iconBg,
  active,
  onPress,
}: {
  title: string;
  description: string;
  badge: string;
  icon: ChannelIconComponent;
  iconColor: string;
  iconBg: string;
  active: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      onPress={onPress}
      className="flex-row items-center gap-2 rounded-[18px] px-3 py-2 active:opacity-90"
      style={{
        backgroundColor: active ? '#241c38' : '#212838',
        borderWidth: 1,
        borderColor: active ? '#6f1dff' : '#2B3448',
      }}>
      <View
        className="h-[46px] w-[46px] items-center justify-center rounded-[14px]"
        style={{ backgroundColor: iconBg }}>
        <ChannelIcon size={22} color={iconColor} />
      </View>

      <View className="min-w-0 flex-1">
        <View className="flex-row items-center gap-2">
          <Text className="text-[13px] font-semibold text-white">{title}</Text>
          <View
            className="rounded-full px-2 py-0.5"
            style={{ backgroundColor: active ? '#2d2618' : '#FFFFFF14' }}>
            <Text className="text-[10px] font-bold text-[#f6c453]">
              {badge}
            </Text>
          </View>
        </View>
        <Text className="mt-0.5 text-[10px] leading-[14px] text-[#9da7bd]">{description}</Text>
      </View>

      <View
        className="size-6 items-center justify-center rounded-full border"
        style={{
          borderColor: active ? '#9b5cff' : '#586179',
          backgroundColor: active ? '#6f1dff' : 'transparent',
        }}>
        {active ? <Icon as={Check} size={14} className="text-white" /> : null}
      </View>
    </Pressable>
  );
}
