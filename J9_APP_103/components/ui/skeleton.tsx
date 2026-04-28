import { useEffect, useRef } from 'react';
import {
  Animated,
  Easing,
  View,
  type DimensionValue,
  type ViewProps,
} from 'react-native';

type SkeletonProps = ViewProps & {
  width?: DimensionValue;
  height?: DimensionValue;
  radius?: number;
};

export function Skeleton({
  width,
  height,
  radius = 8,
  style,
  className,
  ...rest
}: SkeletonProps) {
  const opacity = useRef(new Animated.Value(0.55)).current;

  useEffect(() => {
    const animation = Animated.loop(
      Animated.sequence([
        Animated.timing(opacity, {
          toValue: 1,
          duration: 800,
          easing: Easing.inOut(Easing.ease),
          useNativeDriver: true,
        }),
        Animated.timing(opacity, {
          toValue: 0.55,
          duration: 800,
          easing: Easing.inOut(Easing.ease),
          useNativeDriver: true,
        }),
      ])
    );

    animation.start();
    return () => animation.stop();
  }, [opacity]);

  return (
    <Animated.View
      className={className}
      style={[
        {
          width,
          height,
          borderRadius: radius,
          backgroundColor: '#2e2744',
          opacity,
        },
        style,
      ]}
      {...rest}
    />
  );
}

export function SkeletonCircle({
  size,
  style,
  ...rest
}: Omit<SkeletonProps, 'width' | 'height' | 'radius'> & { size: number }) {
  return (
    <Skeleton
      width={size}
      height={size}
      radius={size / 2}
      style={style}
      {...rest}
    />
  );
}

export function SkeletonRow({ style, ...rest }: ViewProps) {
  return <View className="flex-row items-center gap-2" style={style} {...rest} />;
}
