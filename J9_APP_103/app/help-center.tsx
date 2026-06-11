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
  Headphones,
  Mail,
  MessageCircleQuestion,
  ShieldCheck,
  Users,
} from 'lucide-react-native';
import { type ReactNode, useState } from 'react';
import { Pressable, View } from 'react-native';

type FaqItem = {
  question: string;
  answer?: string;
  content?: ReactNode;
};

type FaqCategory = {
  key: string;
  label: string;
  icon: typeof ShieldCheck;
  color: string;
  bg: string;
  items: FaqItem[];
};

const AGENT_DISPLAY_RATIO_ROWS = [
  {
    relation: '你直接发展的会员',
    ratio: '0.8%',
    example: '会员归属 A21',
  },
  {
    relation: '你下级代理的会员',
    ratio: '0.5%',
    example: '会员归属 A211',
  },
  {
    relation: '你下下级代理的会员',
    ratio: '0.2%',
    example: '会员归属 A2111',
  },
] as const;

const AGENT_FAQ_ITEMS: FaqItem[] = [
  {
    question: '返水和代理返利有什么区别？',
    content: <RebateVsAgentGuide />,
  },
  {
    question: '如何申请成为代理？',
    answer:
      '需先通过邀请链接成功拉满 5 名真实玩家，且每人须完成注册、有充值并有正常游戏。达标后请联系在线客服，核验通过后即可为您开通代理后台。',
  },
  {
    question: '邀请链接怎么用？',
    answer:
      '把邀请链接或邀请码发给好友，对方使用该链接注册后会自动记在你的邀请名下。建议链接带有渠道信息，注册后对方会显示所属渠道和邀请码。',
  },
  {
    question: '代理返利怎么算？',
    content: <AgentDisplayRatioGuide />,
  },
  {
    question: '代理层级是什么意思？',
    content: <AgentHierarchyGuide />,
  },
  {
    question: '不同游戏，返利一样吗？',
    content: <GameTypeCoefficientGuide />,
  },
  {
    question: '返利多久结一次？',
    answer:
      '按自然周统计，从每周一 00:00 开始计算。代理可在后台「代理结算」中查看，平台确认后会按流程发放。',
  },
  {
    question: '返水中心为什么看不到下级流水？',
    answer:
      '返水中心只统计你自己的投注，不包括你邀请的人。邀请来的玩家有各自账号和流水；你作为代理，对应收益请在代理后台查看。',
  },
  {
    question: '代理后台能看到多少层下级？',
    answer:
      '一般能看到 3 层：你自己、你的直属下级代理、下级的直属下级。更深层级的数据在后台里看不到。',
  },
  {
    question: '没通过邀请链接注册会归到哪个代理？',
    answer:
      '按顺序判断：链接里带了渠道名则归到该渠道；带了代理编号则归到该代理；都没有则归到平台默认代理。推广时请务必让对方使用你的邀请链接注册。',
  },
  {
    question: '代理和返水能同时拿吗？',
    answer:
      '可以，但是两笔钱：你自己玩游戏去「返水中心」领返水；你名下会员玩游戏在代理后台看返利。互不影响，各算各的。',
  },
  {
    question: '「代理分销」是什么意思？',
    answer:
      '平台支持邀请推广与多级代理合作，规则公开透明。达标可申请专属代理后台，返利比例按帮助中心说明计算，细则也可咨询在线客服。',
  },
];

const FAQ_DATA: FaqCategory[] = [
  {
    key: 'account',
    label: '账户、资金与记录',
    icon: ShieldCheck,
    color: '#9b5cff',
    bg: '#241d39',
    items: [
      {
        question: '如何修改登录密码？',
        answer: '进入“我的”-“修改密码”，按页面提示完成验证后即可更新。',
      },
      {
        question: '忘记密码怎么办？',
        answer: '请联系在线客服，并在核验信息后申请处理。',
      },
      {
        question: '账号信息可修改哪些内容？',
        answer: '可维护手机号、Telegram、USDT 地址、提现密码及头像信息。',
      },
      {
        question: '支持哪些充值方式？',
        answer: '当前支持 USDT 充值通道，具体以页面展示为准。',
      },
      {
        question: '提现前需要完成什么？',
        answer: '请先在账号信息中维护 USDT 提现地址与提现密码。',
      },
      {
        question: '如何领取返水？',
        answer: '进入“返水中心”后，可按页面提示提交返水申请。',
      },
      {
        question: '交易明细在哪里查看？',
        answer: '进入“交易明细”即可查看充值、提现及游戏流水记录。',
      },
      {
        question: '消息通知包含哪些内容？',
        answer: '系统公告、客服回复及个人留言记录都会汇总展示。',
      },
    ],
  },
  {
    key: 'agent',
    label: '代理相关',
    icon: Users,
    color: '#7B5CFF',
    bg: '#241d39',
    items: AGENT_FAQ_ITEMS,
  },
  {
    key: 'bonus',
    label: '活动福利',
    icon: CreditCard,
    color: '#f6c453',
    bg: '#2d2618',
    items: [
      {
        question: '如何邀请好友？',
        answer: '复制邀请码或邀请链接分享给好友，对方注册后将自动计入邀请记录。',
      },
      {
        question: '返水记录在哪里查看？',
        answer: '返水中心页面会展示近期返水到账记录。',
      },
    ],
  },
];

export default function HelpCenterScreen() {
  const router = useRouter();
  const [expandedCategory, setExpandedCategory] = useState<string>('account');
  const [expandedQuestion, setExpandedQuestion] = useState<string | null>(null);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51InnerPage
        title="帮助中心"
        subtitle="常见问题速查"
        tag="使用说明"
        tone="blue"
        hideHero>
        <Pg51InnerPageTopBar
          onBack={() => router.back()}
          icon={MessageCircleQuestion}
          iconColor="#4ea3ff"
          title="帮助中心"
          subtitle="常见问题速查"
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
            涵盖账户资金、代理返利、活动福利等高频问题，规则以平台最新公告为准。
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
                  <FaqQuestionList
                    items={category.items}
                    color={category.color}
                    questionKeyPrefix={category.key}
                    expandedQuestion={expandedQuestion}
                    onToggleQuestion={setExpandedQuestion}
                  />
                </View>
              ) : null}
            </Pg51SectionCard>
          );
        })}

        <Pg51SectionCard title="联系我们" description="仍可联系在线客服">
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

function FaqQuestionList({
  items,
  color,
  questionKeyPrefix,
  expandedQuestion,
  onToggleQuestion,
}: {
  items: FaqItem[];
  color: string;
  questionKeyPrefix: string;
  expandedQuestion: string | null;
  onToggleQuestion: (key: string | null) => void;
}) {
  return (
    <>
      {items.map((item, index) => {
        const key = `${questionKeyPrefix}-${index}`;
        const opened = expandedQuestion === key;

        return (
          <View key={key} className="rounded-[20px] bg-[#212838] px-4 py-3">
            <Pressable
              onPress={() => onToggleQuestion(opened ? null : key)}
              className="flex-row items-start gap-3">
              <View
                className="mt-0.5 size-6 items-center justify-center rounded-full"
                style={{ backgroundColor: opened ? color : '#30384b' }}>
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
                  {item.question}
                </Text>
                {opened ? (
                  item.content ? (
                    <View className="mt-2">{item.content}</View>
                  ) : (
                    <Text className="mt-2 text-[12px] leading-[20px] text-[#9fa8be]">
                      {item.answer}
                    </Text>
                  )
                ) : null}
              </View>
            </Pressable>
          </View>
        );
      })}
    </>
  );
}

function FaqInfoTable({
  columns,
  rows,
}: {
  columns: { key: string; label: string; flex: number; align?: 'left' | 'center' | 'right' }[];
  rows: Record<string, string>[];
}) {
  const alignClass = (align: 'left' | 'center' | 'right' = 'left') =>
    align === 'center' ? 'text-center' : align === 'right' ? 'text-right' : '';

  return (
    <View className="overflow-hidden rounded-[16px] border border-[#39435a]">
      <View className="flex-row border-b border-[#39435a] bg-[#1a2030] px-2 py-2.5">
        {columns.map((column) => (
          <Text
            key={column.key}
            className={`text-[10px] font-bold leading-[14px] text-[#c5cee0] ${alignClass(column.align)}`}
            style={{ flex: column.flex }}>
            {column.label}
          </Text>
        ))}
      </View>

      {rows.map((row, index) => (
        <View
          key={index}
          className={`flex-row px-2 py-2.5 ${
            index < rows.length - 1 ? 'border-b border-[#30384b]' : ''
          }`}
          style={{ backgroundColor: index % 2 === 0 ? '#212838' : '#1d2433' }}>
          {columns.map((column) => (
            <Text
              key={column.key}
              className={`text-[10px] leading-[16px] text-[#dbe3f4] ${alignClass(column.align)}`}
              style={{ flex: column.flex }}>
              {row[column.key]}
            </Text>
          ))}
        </View>
      ))}
    </View>
  );
}

function RebateVsAgentGuide() {
  return (
    <View className="gap-3">
      <FaqInfoTable
        columns={[
          { key: 'item', label: '', flex: 0.7 },
          { key: 'rebate', label: '返水', flex: 1.2 },
          { key: 'agent', label: '代理返利', flex: 1.2 },
        ]}
        rows={[
          { item: '给谁', rebate: '你自己玩游戏', agent: '代理（推广人）' },
          { item: '在哪看', rebate: 'App「返水中心」', agent: '代理后台「代理结算」' },
          { item: '怎么算', rebate: '按你自己的有效投注', agent: '按你名下会员的投注流水' },
        ]}
      />
      <Text className="text-[11px] leading-[18px] text-[#8f9ab2]">
        你邀请的朋友玩游戏，不会增加你自己的返水；代理可在后台拿到对应返利。
      </Text>
    </View>
  );
}

function AgentDisplayRatioGuide() {
  return (
    <View className="gap-3">
      <Text className="text-[12px] leading-[20px] text-[#9fa8be]">
        看你和这名会员的关系，基础比例如下。层级越深，基础比例越低。
      </Text>

      <View className="overflow-hidden rounded-[16px] border border-[#39435a]">
        <View className="flex-row border-b border-[#39435a] bg-[#1a2030] px-2 py-2.5">
          <Text className="flex-[1.2] text-[10px] font-bold leading-[14px] text-[#c5cee0]">
            关系
          </Text>
          <Text className="flex-[0.8] text-center text-[10px] font-bold leading-[14px] text-[#c5cee0]">
            基础比例
          </Text>
          <Text className="flex-1 text-right text-[10px] font-bold leading-[14px] text-[#c5cee0]">
            举例（你是 A21）
          </Text>
        </View>

        {AGENT_DISPLAY_RATIO_ROWS.map((row, index) => (
          <View
            key={row.relation}
            className={`flex-row px-2 py-2.5 ${
              index < AGENT_DISPLAY_RATIO_ROWS.length - 1 ? 'border-b border-[#30384b]' : ''
            }`}
            style={{ backgroundColor: index % 2 === 0 ? '#212838' : '#1d2433' }}>
            <Text className="flex-[1.2] text-[11px] font-semibold leading-[16px] text-white">
              {row.relation}
            </Text>
            <Text
              className="flex-[0.8] text-center text-[11px] font-bold leading-[16px]"
              style={{ color: '#7B5CFF' }}>
              {row.ratio}
            </Text>
            <Text className="flex-1 text-right text-[10px] leading-[16px] text-[#9fa8be]">
              {row.example}
            </Text>
          </View>
        ))}
      </View>

      <Text className="text-[11px] leading-[18px] text-[#8f9ab2]">
        实际返利 = 基础比例 × 游戏系数，详见「不同游戏，返利一样吗？」。
      </Text>
    </View>
  );
}

function AgentHierarchyGuide() {
  return (
    <View className="gap-3">
      <View className="rounded-[16px] border border-[#39435a] bg-[#1a2030] px-3 py-3">
        <Text className="text-[11px] leading-[20px] text-[#dbe3f4]">
          A21（你）{'\n'}
          {'  '}└── A211（你的下级代理）{'\n'}
          {'      '}└── A2111（下级的下级代理）
        </Text>
      </View>
      <Text className="text-[12px] leading-[20px] text-[#9fa8be]">
        A21 直接发展的玩家按 0.8% 算；A211 发展的玩家对 A21 按 0.5% 算；A2111 发展的玩家对 A21
        按 0.2% 算。
      </Text>
    </View>
  );
}

const GAME_TYPE_COEFFICIENT_ROWS = [
  { type: '真人、体育', coefficient: '× 0.2' },
  { type: '电子、捕鱼、彩票、棋牌、电竞', coefficient: '× 1' },
] as const;

function GameTypeCoefficientGuide() {
  return (
    <View className="gap-3">
      <Text className="text-[12px] leading-[20px] text-[#9fa8be]">
        不一样，还要再乘一个「游戏系数」。实际返利 = 基础比例 × 游戏系数。
      </Text>

      <View className="overflow-hidden rounded-[16px] border border-[#39435a]">
        <View className="flex-row border-b border-[#39435a] bg-[#1a2030] px-2 py-2.5">
          <Text className="flex-1 text-[10px] font-bold leading-[14px] text-[#c5cee0]">
            游戏类型
          </Text>
          <Text className="w-16 text-right text-[10px] font-bold leading-[14px] text-[#c5cee0]">
            系数
          </Text>
        </View>

        {GAME_TYPE_COEFFICIENT_ROWS.map((row, index) => (
          <View
            key={row.type}
            className={`flex-row px-2 py-2.5 ${
              index < GAME_TYPE_COEFFICIENT_ROWS.length - 1 ? 'border-b border-[#30384b]' : ''
            }`}
            style={{ backgroundColor: index % 2 === 0 ? '#212838' : '#1d2433' }}>
            <Text className="flex-1 text-[11px] leading-[16px] text-white">{row.type}</Text>
            <Text
              className="w-16 text-right text-[11px] font-bold leading-[16px]"
              style={{ color: '#7B5CFF' }}>
              {row.coefficient}
            </Text>
          </View>
        ))}
      </View>

      <View className="rounded-[16px] border border-[#39435a] bg-[#1a2030] px-3 py-3">
        <Text className="text-[11px] leading-[18px] text-[#c5cee0]">
          举例：基础 0.8%，会员这周真人占 30%、棋牌占 70%，实际约为：
        </Text>
        <Text className="mt-1 text-[11px] font-bold leading-[18px] text-[#7B5CFF]">
          0.8% ×（30%×0.2 + 70%×1）≈ 0.7%
        </Text>
      </View>

      <Text className="text-[11px] leading-[18px] text-[#8f9ab2]">
        会员若混玩多种游戏，实际比例会低于满额基础比例，这是正常的。
      </Text>
    </View>
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
