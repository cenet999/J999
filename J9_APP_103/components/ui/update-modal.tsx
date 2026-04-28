import { Pg51LucideIcon } from '@/components/pg51-clone/original-icons';
import { Text } from '@/components/ui/text';
import { CheckCircle2, Download, RefreshCw } from 'lucide-react-native';
import React, { useEffect, useRef } from 'react';
import { ActivityIndicator, Animated, Dimensions, Modal, View } from 'react-native';

const ACCENT = '#7B5CFF';
const SUCCESS = '#35D07F';
const ERROR = '#EF4444';

export type UpdateStatus = 'checking' | 'downloading' | 'ready' | 'error';

export function UpdateModal({
  visible,
  status,
  progress,
  errorMessage,
}: {
  visible: boolean;
  status: UpdateStatus;
  progress?: number;
  errorMessage?: string;
}) {
  const fadeAnim = useRef(new Animated.Value(0)).current;
  const scaleAnim = useRef(new Animated.Value(0.9)).current;

  useEffect(() => {
    if (visible) {
      Animated.parallel([
        Animated.timing(fadeAnim, {
          toValue: 1,
          duration: 300,
          useNativeDriver: true,
        }),
        Animated.spring(scaleAnim, {
          toValue: 1,
          tension: 50,
          friction: 7,
          useNativeDriver: true,
        }),
      ]).start();
    } else {
      Animated.parallel([
        Animated.timing(fadeAnim, {
          toValue: 0,
          duration: 200,
          useNativeDriver: true,
        }),
        Animated.timing(scaleAnim, {
          toValue: 0.9,
          duration: 200,
          useNativeDriver: true,
        }),
      ]).start();
    }
  }, [fadeAnim, scaleAnim, visible]);

  const statusContent = (() => {
    switch (status) {
      case 'checking':
        return {
          icon: <Pg51LucideIcon icon={RefreshCw} size={48} color={ACCENT} />,
          title: '检查更新中...',
          message: '正在检查是否有新版本可用',
          showProgress: false,
        };
      case 'downloading':
        return {
          icon: <Pg51LucideIcon icon={Download} size={48} color={ACCENT} />,
          title: '下载更新中...',
          message:
            progress !== undefined
              ? `正在下载更新包 ${Math.round(progress)}%`
              : '正在下载更新包，请稍候',
          showProgress: true,
        };
      case 'ready':
        return {
          icon: <Pg51LucideIcon icon={CheckCircle2} size={48} color={SUCCESS} />,
          title: '更新完成',
          message: '正在重启应用以应用更新',
          showProgress: false,
        };
      case 'error':
        return {
          icon: <Pg51LucideIcon icon={RefreshCw} size={48} color={ERROR} />,
          title: '更新失败',
          message: errorMessage || '更新过程中出现错误，请稍后重试',
          showProgress: false,
        };
      default:
        return {
          icon: <Pg51LucideIcon icon={RefreshCw} size={48} color={ACCENT} />,
          title: '更新中...',
          message: '正在处理更新',
          showProgress: false,
        };
    }
  })();

  return (
    <Modal visible={visible} transparent animationType="none" statusBarTranslucent>
      <Animated.View
        style={{
          flex: 1,
          backgroundColor: 'rgba(0, 0, 0, 0.5)',
          justifyContent: 'center',
          alignItems: 'center',
          opacity: fadeAnim,
        }}>
        <Animated.View
          style={{
            backgroundColor: 'white',
            borderRadius: 16,
            padding: 24,
            width: Dimensions.get('window').width * 0.8,
            maxWidth: 400,
            alignItems: 'center',
            transform: [{ scale: scaleAnim }],
            shadowColor: '#000',
            shadowOffset: { width: 0, height: 4 },
            shadowOpacity: 0.3,
            shadowRadius: 8,
            elevation: 8,
          }}>
          <View className="mb-4">
            {status === 'checking' || status === 'downloading' ? (
              <ActivityIndicator size="large" color={ACCENT} />
            ) : (
              statusContent.icon
            )}
          </View>

          <Text className="mb-2 text-center text-[18px] font-bold">{statusContent.title}</Text>
          <Text className="mb-4 text-center text-[14px] text-muted-foreground">
            {statusContent.message}
          </Text>

          {statusContent.showProgress ? (
            <View className="mb-2 w-full">
              <View className="h-2 overflow-hidden rounded-full bg-gray-200">
                <Animated.View
                  style={{
                    height: '100%',
                    backgroundColor: ACCENT,
                    width: progress !== undefined ? `${progress}%` : '0%',
                  }}
                />
              </View>
            </View>
          ) : null}
        </Animated.View>
      </Animated.View>
    </Modal>
  );
}
