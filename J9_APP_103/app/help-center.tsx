import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Pg51InnerPageTopBar } from '@/components/pg51-clone/inner-page-top-bar';
import { Pg51InnerPage, Pg51SectionCard } from '@/components/pg51-clone/page-ui';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Stack, useRouter } from 'expo-router';
import {
  ChevronDown,
  ChevronUp,
  Clock,
  CreditCard,
  Gamepad2,
  Headphones,
  Mail,
  MessageCircleQuestion,
  ShieldCheck,
  Wallet,
} from 'lucide-react-native';
import { useState } from 'react';
import { Pressable, View } from 'react-native';

const FAQ_DATA = [
  {
    key: 'account',
    label: '账户相关',
    icon: ShieldCheck,
    color: '#9b5cff',
    bg: '#241d39',
    items: [
      ['如何修改登录密码？', '进入“我的”-“修改密码”，按页面提示完成验证后即可更新。'],
      ['忘记密码怎么办？', '请联系在线客服，并在核验信息后申请处理。'],
      ['系统设置可修改哪些内容？', '可维护手机号、Telegram、USDT 地址、提现密码及头像信息。'],
    ],
  },
  {
    key: 'wallet',
    label: '充值提现',
    icon: Wallet,
    color: '#4ade80',
    bg: '#172b26',
    items: [
      ['支持哪些充值方式？', '当前支持 USDT 充值通道，具体以页面展示为准。'],
      ['提现前需要完成什么？', '请先在系统设置中维护 USDT 提现地址与提现密码。'],
      ['如何领取返水？', '进入“返水中心”后，可按页面提示提交返水申请。'],
    ],
  },
  {
    key: 'game',
    label: '游戏和记录',
    icon: Gamepad2,
    color: '#4ea3ff',
    bg: '#172535',
    items: [
      ['交易明细在哪里查看？', '进入“交易明细”即可查看充值、提现及游戏流水记录。'],
      ['消息通知包含哪些内容？', '系统公告、客服回复及个人留言记录都会汇总展示。'],
    ],
  },
  {
    key: 'bonus',
    label: '活动福利',
    icon: CreditCard,
    color: '#f6c453',
    bg: '#2d2618',
    items: [
      ['如何邀请好友？', '复制邀请码或邀请链接分享给好友，对方注册后将自动计入邀请记录。'],
      ['返水记录在哪里查看？', '返水中心页面会展示近期返水到账记录。'],
    ],
  },
] as const;

export default function HelpCenterScreen() {
  const router = useRouter();
  const [expandedCategory, setExpandedCategory] = useState<string>('account');
  const [expandedQuestion, setExpandedQuestion] = useState<string | null>(null);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51InnerPage
        title="帮助中心"
        subtitle="常见问题与操作说明已统一整理，方便快速查阅。"
        tag="使用说明"
        tone="blue"
        hideHero>
        <Pg51InnerPageTopBar
          onBack={() => router.back()}
          icon={MessageCircleQuestion}
          iconColor="#4ea3ff"
          title="帮助中心"
          subtitle="常见问题与操作说明已统一整理，方便快速查阅。"
          tone="blue"
        />

        <View className="items-center gap-3 rounded-[28px] border border-[#39435a] bg-[#171d2a] p-5">
          <View
            className="items-center justify-center rounded-full bg-[#172535]"
            style={{ width: 64, height: 64 }}>
            <Icon as={MessageCircleQuestion} size={32} color="#4ea3ff" />
          </View>
          <Text className="text-base font-extrabold text-white">常见问题</Text>
          <Text className="text-center text-xs leading-[20px] text-[#9fa8be]">
            涵盖账户、资金、活动与记录等高频问题，可按分类查看详细说明。
          </Text>
        </View>

        {FAQ_DATA.map((category) => {
          const isExpanded = expandedCategory === category.key;

          return (
            <Pg51SectionCard key={category.key} title={category.label} description="">
              <Pressable
                onPress={() => setExpandedCategory(isExpanded ? '' : category.key)}
                className="flex-row items-center gap-3 rounded-[20px] bg-[#212838] px-4 py-3">
                <Pg51LucideIconBadge icon={category.icon} />
                <Text className="flex-1 text-[14px] font-bold text-white">{category.label}</Text>
                <Icon as={isExpanded ? ChevronUp : ChevronDown} size={16} color="#8f9ab2" />
              </Pressable>

              {isExpanded ? (
                <View className="gap-3">
                  {category.items.map(([question, answer], index) => {
                    const key = `${category.key}-${index}`;
                    const opened = expandedQuestion === key;

                    return (
                      <View key={key} className="rounded-[20px] bg-[#212838] px-4 py-3">
                        <Pressable
                          onPress={() => setExpandedQuestion(opened ? null : key)}
                          className="flex-row items-start gap-3">
                          <View
                            className="mt-0.5 size-6 items-center justify-center rounded-full"
                            style={{ backgroundColor: opened ? category.color : '#30384b' }}>
                            <Text
                              className="text-[10px] font-black"
                              style={{ color: opened ? '#111827' : '#dbe3f4' }}>
                              Q
                            </Text>
                          </View>
                          <View className="flex-1">
                            <Text
                              className="text-[13px] font-bold"
                              style={{ color: opened ? '#ffffff' : '#dbe3f4' }}>
                              {question}
                            </Text>
                            {opened ? (
                              <Text className="mt-2 text-[12px] leading-[20px] text-[#9fa8be]">
                                {answer}
                              </Text>
                            ) : null}
                          </View>
                        </Pressable>
                      </View>
                    );
                  })}
                </View>
              ) : null}
            </Pg51SectionCard>
          );
        })}

        <Pg51SectionCard title="联系我们" description="如仍需协助，可通过以下方式联系客服。">
          <ContactItem
            icon={Headphones}
            color="#9b5cff"
            bg="#241d39"
            label="在线客服"
            desc="7 x 24 小时在线服务"
            action="发起咨询"
            onPress={() => router.push('/chat')}
          />
          <ContactItem
            icon={Mail}
            color="#ff7e93"
            bg="#3a1f29"
            label="邮件支持"
            desc="support@j9game.com"
          />
          <ContactItem
            icon={Clock}
            color="#f6c453"
            bg="#2d2618"
            label="服务时间"
            desc="全天候服务"
          />
        </Pg51SectionCard>
      </Pg51InnerPage>
    </>
  );
}

function ContactItem({
  icon,
  color,
  bg,
  label,
  desc,
  action,
  onPress,
}: {
  icon: any;
  color: string;
  bg: string;
  label: string;
  desc: string;
  action?: string;
  onPress?: () => void;
}) {
  return (
    <Pressable
      onPress={onPress}
      disabled={!onPress}
      className="flex-row items-center gap-3 rounded-[20px] bg-[#212838] px-4 py-3"
      style={{ opacity: onPress ? 1 : 0.96 }}>
      <Pg51LucideIconBadge icon={icon} />
      <View className="flex-1">
        <Text className="text-[14px] font-bold text-white">{label}</Text>
        <Text className="mt-1 text-[11px] text-[#8f9ab2]">{desc}</Text>
      </View>
      {action ? (
        <View className="rounded-full bg-[#2a3246] px-3 py-1.5">
          <Text className="text-[10px] font-bold" style={{ color }}>
            {action}
          </Text>
        </View>
      ) : null}
    </Pressable>
  );
}
