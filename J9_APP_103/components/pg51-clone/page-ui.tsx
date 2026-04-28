import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Pg51PageShell, Pg51TrackedScrollView } from '@/components/pg51-clone/chrome';
import type { LucideIcon } from 'lucide-react-native';
import type { ReactNode } from 'react';
import { View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

type PageTone = 'purple' | 'gold' | 'green' | 'blue';
type AccentTone = 'purple' | 'gold' | 'green' | 'blue' | 'red';

const heroToneStyles: Record<PageTone, { border: string; pill: string; dot: string }> = {
  purple: {
    border: 'border-[#5d43a0]',
    pill: 'bg-[#6f1dff]',
    dot: 'bg-[#9b5cff]',
  },
  gold: {
    border: 'border-[#7a6534]',
    pill: 'bg-[#b79249]',
    dot: 'bg-[#f6c453]',
  },
  green: {
    border: 'border-[#2e6c57]',
    pill: 'bg-[#2f9f76]',
    dot: 'bg-[#48d39b]',
  },
  blue: {
    border: 'border-[#2f5479]',
    pill: 'bg-[#2563eb]',
    dot: 'bg-[#4ea3ff]',
  },
};

const statToneStyles: Record<AccentTone, { bg: string }> = {
  purple: {
    bg: 'bg-[#221b35]',
  },
  gold: {
    bg: 'bg-[#2d2618]',
  },
  green: {
    bg: 'bg-[#172b26]',
  },
  blue: {
    bg: 'bg-[#172535]',
  },
  red: {
    bg: 'bg-[#311d24]',
  },
};

export function Pg51InnerPage({
  title,
  subtitle,
  tag,
  tone = 'purple',
  hideHero = false,
  children,
}: {
  title: string;
  subtitle: string;
  tag?: string;
  tone?: PageTone;
  hideHero?: boolean;
  children: ReactNode;
}) {
  const toneStyle = heroToneStyles[tone];
  const insets = useSafeAreaInsets();

  return (
    <Pg51PageShell>
      <Pg51TrackedScrollView
        className="flex-1"
        showsVerticalScrollIndicator={false}
        contentContainerStyle={{
          paddingHorizontal: 16,
          paddingTop: insets.top + 20,
          paddingBottom: 124,
        }}>
        {!hideHero ? (
          <View
            className={`overflow-hidden rounded-[30px] border bg-[#171d2a] px-5 pb-5 pt-5 ${toneStyle.border}`}>
            <View className="flex-row items-start justify-between gap-3">
              <View className="flex-1">
                <Text className="text-[30px] font-black text-white">{title}</Text>
                <Text className="mt-2 text-[13px] leading-[21px] text-[#aab4ca]">{subtitle}</Text>
              </View>

              {tag ? (
                <View className={`rounded-full px-3 py-1.5 ${toneStyle.pill}`}>
                  <Text className="text-[11px] font-bold text-white">{tag}</Text>
                </View>
              ) : null}
            </View>

            <View className="mt-5 flex-row gap-2">
              <View className={`h-[4px] w-12 rounded-full ${toneStyle.dot}`} />
              <View className="h-[4px] w-5 rounded-full bg-[#30384b]" />
              <View className="h-[4px] w-3 rounded-full bg-[#30384b]" />
            </View>
          </View>
        ) : null}

        <View className={`${hideHero ? '' : 'mt-4'} gap-4`}>{children}</View>
      </Pg51TrackedScrollView>
    </Pg51PageShell>
  );
}

export function Pg51SectionCard({
  title,
  description,
  right,
  children,
}: {
  title?: string;
  description?: string;
  right?: ReactNode;
  children: ReactNode;
}) {
  const hasHeader = Boolean(title || description || right);

  return (
    <View className="rounded-[28px] border border-[#39435a] bg-[#171d2a] p-4">
      {hasHeader ? (
        <View className="flex-row items-start justify-between gap-3">
          <View className="flex-1">
            {title ? <Text className="text-[18px] font-black text-white">{title}</Text> : null}
            {description ? (
              <Text className="mt-1 text-[12px] leading-[20px] text-[#97a1b8]">{description}</Text>
            ) : null}
          </View>
          {right}
        </View>
      ) : null}

      <View className={`${hasHeader ? 'mt-4' : ''} gap-3`}>{children}</View>
    </View>
  );
}

export function Pg51StatCard({
  icon,
  label,
  value,
  hint,
  tone = 'purple',
}: {
  icon: LucideIcon;
  label: string;
  value: string;
  hint?: string;
  tone?: AccentTone;
}) {
  const toneStyle = statToneStyles[tone];

  return (
    <View className={`flex-1 rounded-[22px] px-4 py-4 ${toneStyle.bg}`}>
      <View className="flex-row items-center gap-3">
        <Pg51LucideIconBadge icon={icon} />
        <Text className="flex-1 text-[12px] font-medium text-[#c6cee0]">{label}</Text>
      </View>

      <Text className="mt-3 text-[22px] font-black text-white">{value}</Text>
      {hint ? <Text className="mt-1 text-[11px] text-[#8f9ab2]">{hint}</Text> : null}
    </View>
  );
}

export function Pg51ActionCard({
  icon,
  title,
  description,
  badge,
  tone = 'purple',
}: {
  icon: LucideIcon;
  title: string;
  description: string;
  badge?: string;
  tone?: AccentTone;
}) {
  const toneStyle = statToneStyles[tone];

  return (
    <View className="flex-1 rounded-[22px] border border-[#39435a] bg-[#212838] p-4">
      <View className="flex-row items-start justify-between gap-3">
        <Pg51LucideIconBadge icon={icon} size={44} iconSize={19} />
        {badge ? (
          <View className={`rounded-full px-2 py-1 ${toneStyle.bg}`}>
            <Text className="text-[10px] font-bold text-[#dbe3f4]">{badge}</Text>
          </View>
        ) : null}
      </View>

      <Text className="mt-3 text-[15px] font-bold text-white">{title}</Text>
      <Text className="mt-1 text-[12px] leading-[19px] text-[#9fa8be]">{description}</Text>
    </View>
  );
}

export function Pg51InfoRow({
  label,
  value,
  valueTone = 'default',
}: {
  label: string;
  value: string;
  valueTone?: 'default' | 'success' | 'warning' | 'danger';
}) {
  const toneClass =
    valueTone === 'success'
      ? 'text-[#4ade80]'
      : valueTone === 'warning'
        ? 'text-[#ffcc66]'
        : valueTone === 'danger'
          ? 'text-[#ff8aa2]'
          : 'text-white';

  return (
    <View className="flex-row items-center justify-between rounded-[18px] bg-[#222a3a] px-4 py-3">
      <Text className="text-[12px] text-[#9da7bd]">{label}</Text>
      <Text className={`text-[13px] font-semibold ${toneClass}`}>{value}</Text>
    </View>
  );
}

export function Pg51ProgressRow({
  title,
  detail,
  current,
  total,
  tone = 'purple',
}: {
  title: string;
  detail: string;
  current: number;
  total: number;
  tone?: AccentTone;
}) {
  const percent = total > 0 ? Math.max(0, Math.min(current / total, 1)) : 0;
  const barTone =
    tone === 'green'
      ? 'bg-[#2f9f76]'
      : tone === 'gold'
        ? 'bg-[#b79249]'
        : tone === 'blue'
          ? 'bg-[#2563eb]'
          : tone === 'red'
            ? 'bg-[#d14d72]'
            : 'bg-[#6f1dff]';

  return (
    <View className="rounded-[22px] bg-[#212838] p-4">
      <View className="flex-row items-center justify-between gap-3">
        <View className="flex-1">
          <Text className="text-[14px] font-bold text-white">{title}</Text>
          <Text className="mt-1 text-[12px] text-[#9da7bd]">{detail}</Text>
        </View>
        <Text className="text-[12px] font-semibold text-white">
          {current} / {total}
        </Text>
      </View>

      <View className="mt-3 h-[8px] overflow-hidden rounded-full bg-[#31394c]">
        <View className={`h-full rounded-full ${barTone}`} style={{ width: `${percent * 100}%` }} />
      </View>
    </View>
  );
}

export function Pg51Pill({ label, active = false }: { label: string; active?: boolean }) {
  return (
    <View
      className={`rounded-full border px-3 py-1.5 ${
        active ? 'border-[#9b5cff] bg-[#6f1dff]' : 'border-[#414a61] bg-[#222a3a]'
      }`}>
      <Text className={`text-[12px] font-semibold ${active ? 'text-white' : 'text-[#b7c0d6]'}`}>
        {label}
      </Text>
    </View>
  );
}
