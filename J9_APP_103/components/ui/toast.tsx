import { Pg51LucideIcon } from '@/components/pg51-clone/original-icons';
import { useCallback, useEffect, useRef, useState } from 'react';
import {
  DeviceEventEmitter,
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
    gradient: ['#EDFCF2', '#F0FFF4'] as [string, string],
    iconBg: '#35D07F22',
    iconColor: '#35D07F',
    textColor: '#14532D',
    subtextColor: '#16A34A',
    borderColor: '#35D07F44',
    Icon: CheckCircle2,
  },
  error: {
    gradient: ['#FFF0F0', '#FFF5F5'] as [string, string],
    iconBg: '#FF5FA222',
    iconColor: '#FF5FA2',
    textColor: '#7F1D1D',
    subtextColor: '#DC2626',
    borderColor: '#FF5FA244',
    Icon: AlertCircle,
  },
  info: {
    gradient: ['#EEF2FF', '#F0F4FF'] as [string, string],
    iconBg: '#7B5CFF22',
    iconColor: '#7B5CFF',
    textColor: '#2A184B',
    subtextColor: '#6B5A8E',
    borderColor: '#7B5CFF44',
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

  const dismiss = useCallback(() => {
    setToast(null);
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  }, []);

  useEffect(() => {
    const apply = (data: ToastOptions) => {
      if (timerRef.current) clearTimeout(timerRef.current);
      setToast(data);
      if (data.blocking) return;
      timerRef.current = setTimeout(() => setToast(null), data.duration ?? 3000);
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
    <LinearGradient
      colors={style.gradient}
      start={{ x: 0, y: 0 }}
      end={{ x: 1, y: 1 }}
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        borderRadius: 18,
        borderWidth: 1,
        borderColor: style.borderColor,
        paddingHorizontal: 14,
        paddingVertical: 14,
      }}>
      <View
        style={{
          width: 36,
          height: 36,
          borderRadius: 12,
          backgroundColor: style.iconBg,
          alignItems: 'center',
          justifyContent: 'center',
        }}>
        <Pg51LucideIcon icon={IconComponent} size={20} color={style.iconColor} />
      </View>

      <View style={{ flex: 1, marginLeft: 12 }}>
        <Text style={{ color: style.textColor, fontSize: 14, fontWeight: '700' }}>
          {toast.text1}
        </Text>
        {toast.text2 ? (
          <Text style={{ color: style.subtextColor, fontSize: 12, marginTop: 4 }}>
            {toast.text2}
          </Text>
        ) : null}
      </View>

      {!toast.blocking ? (
        <Pressable
          onPress={dismiss}
          style={{
            width: 28,
            height: 28,
            alignItems: 'center',
            justifyContent: 'center',
            borderRadius: 10,
            backgroundColor: style.iconBg,
          }}>
          <Pg51LucideIcon icon={X} size={14} color={style.iconColor} />
        </Pressable>
      ) : null}
    </LinearGradient>
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
          paddingTop: insets.top + 48,
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
