import { Platform } from 'react-native';

function isIosWebRuntime() {
  if (Platform.OS !== 'web' || typeof navigator === 'undefined') return false;

  const userAgent = navigator.userAgent || '';
  const platform = navigator.platform || '';
  const isTouchMac =
    platform === 'MacIntel' &&
    typeof navigator.maxTouchPoints === 'number' &&
    navigator.maxTouchPoints > 1;

  return /iPad|iPhone|iPod/.test(userAgent) || isTouchMac;
}

export function isIosHomeScreenRuntime() {
  if (!isIosWebRuntime()) return false;

  const standaloneNavigator = navigator as Navigator & { standalone?: boolean };
  const standaloneMedia =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(display-mode: standalone)').matches;

  return standaloneNavigator.standalone === true || standaloneMedia;
}

export function isInstalledAppRuntime() {
  return Platform.OS === 'ios' || Platform.OS === 'android' || isIosHomeScreenRuntime();
}

export function getClientPlatform() {
  if (isIosHomeScreenRuntime()) return 'ios-home-screen';
  return Platform.OS;
}
