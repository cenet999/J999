import { Pg51LucideIcon } from '@/components/pg51-clone/original-icons';
import { useCallback, useEffect, useRef, useState } from 'react';
import {
  Animated,
  DeviceEventEmitter,
  Easing,
  Modal,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { AlertCircle, CheckCircle2, Info, X } from 'lucide-react-native';
import { LinearGradient } from 'expo-linear-gradient';

type ToastType = 'success' | 'error' | 'info';

type ToastOptions = {
  type: ToastType;
  text1: string;
  text2?: string;
  modal?: boolean;
  blocking?: boolean;
  duration?: number;
};

const SHOW_TOAST = 'SHOW_TOAST';
const DISMISS_TOAST = 'DISMISS_TOAST';

const TOAST_STYLES = {
  success: {
    gradient: ['#2A184B', '#152D3F'] as [string, string],
    iconGradient: ['#B8FFE0', '#35D07F'] as [string, string],
    accentGradient: ['#35D07F', '#3CBAFF'] as [string, string],
    iconBg: '#35D07F24',
    textColor: '#F7FFF9',
    subtextColor: '#B8FFE0',
    shadowColor: '#35D07F55',
    Icon: CheckCircle2,
  },
  error: {
    gradient: ['#2A184B', '#3B1738'] as [string, string],
    iconGradient: ['#FFD1E3', '#FF5FA2'] as [string, string],
    accentGradient: ['#FF5FA2', '#FF8A34'] as [string, string],
    iconBg: '#FF5FA224',
    textColor: '#FFF7FA',
    subtextColor: '#FFD1E3',
    shadowColor: '#FF5FA255',
    Icon: AlertCircle,
  },
  info: {
    gradient: ['#2A184B', '#171D3F'] as [string, string],
    iconGradient: ['#D8CEFF', '#7B5CFF'] as [string, string],
    accentGradient: ['#7B5CFF', '#3CBAFF'] as [string, string],
    iconBg: '#7B5CFF24',
    textColor: '#FFF7F1',
    subtextColor: '#D8CEFF',
    shadowColor: '#7B5CFF55',
    Icon: Info,
  },
};

export const Toast = {
  show: (options: ToastOptions) => {
    if (Platform.OS === 'web' && typeof window !== 'undefined') {
      window.dispatchEvent(new CustomEvent(SHOW_TOAST, { detail: options }));
      return;
    }
    DeviceEventEmitter.emit(SHOW_TOAST, options);
  },
  dismiss: () => {
    if (Platform.OS === 'web' && typeof window !== 'undefined') {
      window.dispatchEvent(new CustomEvent(DISMISS_TOAST));
      return;
    }
    DeviceEventEmitter.emit(DISMISS_TOAST);
  },
};

export function ToastProvider() {
  const [toast, setToast] = useState<ToastOptions | null>(null);
  const insets = useSafeAreaInsets();
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const opacity = useRef(new Animated.Value(0)).current;
  const translateY = useRef(new Animated.Value(-18)).current;
  const scale = useRef(new Animated.Value(0.97)).current;

  const dismiss = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }

    Animated.parallel([
      Animated.timing(opacity, {
        toValue: 0,
        duration: 160,
        easing: Easing.out(Easing.quad),
        useNativeDriver: true,
      }),
      Animated.timing(translateY, {
        toValue: -14,
        duration: 160,
        easing: Easing.out(Easing.quad),
        useNativeDriver: true,
      }),
      Animated.timing(scale, {
        toValue: 0.98,
        duration: 160,
        easing: Easing.out(Easing.quad),
        useNativeDriver: true,
      }),
    ]).start(() => setToast(null));
  }, [opacity, scale, translateY]);

  useEffect(() => {
    const apply = (data: ToastOptions) => {
      if (timerRef.current) clearTimeout(timerRef.current);
      opacity.setValue(0);
      translateY.setValue(-18);
      scale.setValue(0.97);
      setToast(data);

      Animated.parallel([
        Animated.timing(opacity, {
          toValue: 1,
          duration: 240,
          easing: Easing.out(Easing.cubic),
          useNativeDriver: true,
        }),
        Animated.spring(translateY, {
          toValue: 0,
          damping: 17,
          stiffness: 230,
          mass: 0.75,
          useNativeDriver: true,
        }),
        Animated.spring(scale, {
          toValue: 1,
          damping: 16,
          stiffness: 220,
          mass: 0.75,
          useNativeDriver: true,
        }),
      ]).start();

      if (data.blocking) return;
      timerRef.current = setTimeout(dismiss, data.duration ?? 3000);
    };

    const sub = DeviceEventEmitter.addListener(SHOW_TOAST, apply);
    const dismissSub = DeviceEventEmitter.addListener(DISMISS_TOAST, dismiss);

    let cleanupWeb: (() => void) | undefined;
    if (Platform.OS === 'web' && typeof window !== 'undefined') {
      const onShow = (event: Event) => {
        const detail = (event as CustomEvent<ToastOptions>).detail;
        if (detail) apply(detail);
      };
      window.addEventListener(SHOW_TOAST, onShow);
      window.addEventListener(DISMISS_TOAST, dismiss);
      cleanupWeb = () => {
        window.removeEventListener(SHOW_TOAST, onShow);
        window.removeEventListener(DISMISS_TOAST, dismiss);
      };
    }

    return () => {
      sub.remove();
      dismissSub.remove();
      cleanupWeb?.();
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [dismiss]);

  if (!toast) return null;

  const style = TOAST_STYLES[toast.type] ?? TOAST_STYLES.info;
  const IconComponent = style.Icon;
  const isModal = Boolean(toast.modal || toast.blocking);

  const card = (
    <Animated.View
      style={{
        opacity,
        transform: [{ translateY }, { scale }],
        width: '100%',
      }}>
      <LinearGradient
        colors={style.accentGradient}
        start={{ x: 0, y: 0 }}
        end={{ x: 1, y: 1 }}
        style={{
          borderRadius: 20,
          padding: 1,
          boxShadow: `0 12px 30px ${style.shadowColor}`,
        }}>
        <LinearGradient
          colors={style.gradient}
          start={{ x: 0, y: 0 }}
          end={{ x: 1, y: 1 }}
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            overflow: 'hidden',
            borderRadius: 19,
            borderWidth: 1,
            borderColor: 'rgba(255,255,255,0.12)',
            paddingHorizontal: 12,
            paddingVertical: 12,
          }}>
          <View
            style={{
              position: 'absolute',
              left: 0,
              right: 0,
              top: 0,
              height: 1,
              backgroundColor: 'rgba(255,255,255,0.35)',
            }}
          />
          <LinearGradient
            colors={style.iconGradient}
            start={{ x: 0, y: 0 }}
            end={{ x: 1, y: 1 }}
            style={{
              width: 38,
              height: 38,
              borderRadius: 14,
              alignItems: 'center',
              justifyContent: 'center',
              boxShadow: `0 0 18px ${style.shadowColor}`,
            }}>
            <View
              style={{
                position: 'absolute',
                inset: 2,
                borderRadius: 12,
                backgroundColor: style.iconBg,
              }}
            />
            <Pg51LucideIcon icon={IconComponent} size={20} color="#FFFFFF" />
          </LinearGradient>

          <View style={{ flex: 1, paddingLeft: 12, paddingRight: toast.blocking ? 2 : 8 }}>
            <Text
              numberOfLines={2}
              style={{ color: style.textColor, fontSize: 14, fontWeight: '800' }}>
              {toast.text1}
            </Text>
            {toast.text2 ? (
              <Text
                numberOfLines={3}
                style={{
                  color: style.subtextColor,
                  fontSize: 12,
                  fontWeight: '600',
                  marginTop: 4,
                }}>
                {toast.text2}
              </Text>
            ) : null}
          </View>

          {!toast.blocking ? (
            <Pressable
              onPress={dismiss}
              hitSlop={8}
              style={({ pressed }) => ({
                width: 30,
                height: 30,
                alignItems: 'center',
                justifyContent: 'center',
                borderRadius: 12,
                backgroundColor: pressed ? 'rgba(255,255,255,0.18)' : 'rgba(255,255,255,0.1)',
              })}>
              <Pg51LucideIcon icon={X} size={15} color="rgba(255,255,255,0.82)" />
            </Pressable>
          ) : null}
        </LinearGradient>
      </LinearGradient>
    </Animated.View>
  );

  if (isModal) {
    return (
      <Modal visible transparent statusBarTranslucent animationType="fade">
        <View style={styles.modalBackdrop}>
          <View style={{ width: '100%', maxWidth: 360 }}>{card}</View>
        </View>
      </Modal>
    );
  }

  return (
    <View pointerEvents="box-none" style={[StyleSheet.absoluteFill, { zIndex: 99999 }]}>
      <View
        pointerEvents="box-none"
        style={{
          alignItems: 'center',
          paddingHorizontal: 16,
          paddingTop: insets.top + 14,
        }}>
        <View style={{ width: '100%', maxWidth: 400 }}>{card}</View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  modalBackdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.35)',
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 24,
  },
});
