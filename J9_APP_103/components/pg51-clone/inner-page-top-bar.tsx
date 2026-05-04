import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { ChevronLeft, type LucideIcon } from 'lucide-react-native';
import { Pressable, View } from 'react-native';

type Pg51InnerPageTopBarProps = {
  onBack: () => void;
  icon: LucideIcon;
  iconColor?: string;
  title?: string;
  subtitle?: string;
  tone?: 'blue' | 'purple' | 'red';
  hideBackButton?: boolean;
  /** 点击右侧图标（如进入在线客服） */
  onTrailingIconPress?: () => void;
  trailingAccessibilityLabel?: string;
};

const toneStyles = {
  blue: { border: 'border-[#2f5479]' },
  purple: { border: 'border-[#4f3a80]' },
  red: { border: 'border-[#6e3145]' },
} as const;

function Pg51InnerPageTrailingIcon({
  icon: IconComponent,
  iconColor,
  onPress,
  accessibilityLabel,
}: {
  icon: LucideIcon;
  iconColor: string;
  onPress?: () => void;
  accessibilityLabel?: string;
}) {
  const inner = (
    <View className="size-[42px] items-center justify-center rounded-full bg-[#414559]">
      <IconComponent width={22} height={22} color={iconColor} stroke={iconColor} strokeWidth={2} />
    </View>
  );

  if (onPress) {
    return (
      <Pressable
        onPress={onPress}
        accessibilityRole="button"
        accessibilityLabel={accessibilityLabel}
        hitSlop={10}>
        {inner}
      </Pressable>
    );
  }

  return inner;
}

export function Pg51InnerPageTopBar({
  onBack,
  icon,
  iconColor = '#d8dcea',
  title,
  subtitle,
  tone = 'blue',
  hideBackButton = false,
  onTrailingIconPress,
  trailingAccessibilityLabel,
}: Pg51InnerPageTopBarProps) {
  const toneStyle = toneStyles[tone];

  if (title || subtitle) {
    return (
      <View className={`rounded-[28px] border bg-[#171d2a] px-4 py-4 ${toneStyle.border}`}>
        <View className="flex-row items-center gap-3">
          {hideBackButton ? null : (
            <Pressable
              onPress={onBack}
              hitSlop={10}
              className="size-11 items-center justify-center rounded-[16px] border border-[#39435a] bg-[#212838]">
              <Icon as={ChevronLeft} size={18} className="text-white" />
            </Pressable>
          )}

          <View className="flex-1 justify-center px-1">
            {title ? <Text className="text-[20px] font-black text-white">{title}</Text> : null}
            {subtitle ? (
              <Text
                className="mt-1 text-[12px] leading-[19px] text-[#97a1b8]"
                numberOfLines={1}>
                {subtitle}
              </Text>
            ) : null}
          </View>

          <Pg51InnerPageTrailingIcon
            icon={icon}
            iconColor={iconColor}
            onPress={onTrailingIconPress}
            accessibilityLabel={trailingAccessibilityLabel}
          />
        </View>
      </View>
    );
  }

  return (
    <View className="flex-row items-center justify-between">
      <Pressable
        onPress={onBack}
        className="flex-row items-center gap-2 rounded-full border border-[#39435a] bg-[#212838] px-4 py-2.5">
        <Icon as={ChevronLeft} size={16} className="text-white" />
        <Text className="text-[12px] font-bold text-white">返回</Text>
      </Pressable>

      <Pg51InnerPageTrailingIcon
        icon={icon}
        iconColor={iconColor}
        onPress={onTrailingIconPress}
        accessibilityLabel={trailingAccessibilityLabel}
      />
    </View>
  );
}
