import { Text } from '@/components/ui/text';
import { cn, truncateMiddle } from '@/lib/utils';
import { useMemo, useState } from 'react';
import { View } from 'react-native';

type MiddleEllipsisTextProps = {
  children: string;
  className?: string;
  /** 估算单字符宽度（px），用于按容器宽度计算截断长度 */
  charWidth?: number;
  selectable?: boolean;
};

export function MiddleEllipsisText({
  children,
  className,
  charWidth = 7.2,
  selectable,
}: MiddleEllipsisTextProps) {
  const [width, setWidth] = useState(0);

  const display = useMemo(() => {
    if (!children || width <= 0) return children;
    const maxLength = Math.max(8, Math.floor(width / charWidth));
    return truncateMiddle(children, maxLength);
  }, [children, width, charWidth]);

  return (
    <View className="w-full" onLayout={(event) => setWidth(event.nativeEvent.layout.width)}>
      <Text className={cn(className)} numberOfLines={1} selectable={selectable}>
        {display}
      </Text>
    </View>
  );
}
