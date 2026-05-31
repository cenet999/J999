import AsyncStorage from '@react-native-async-storage/async-storage';

const STORAGE_KEY = '@j9_domain_reminder_dismissed_at';
const SNOOZE_MS = 30 * 60 * 1000;

export async function markDomainReminderSnoozed(): Promise<void> {
  try {
    await AsyncStorage.setItem(STORAGE_KEY, String(Date.now()));
  } catch {
    // ignore cache write failures
  }
}

export async function isDomainReminderSnoozed(): Promise<boolean> {
  try {
    const raw = await AsyncStorage.getItem(STORAGE_KEY);
    if (!raw) return false;

    const dismissedAt = Number(raw);
    if (!Number.isFinite(dismissedAt) || dismissedAt <= 0) {
      await AsyncStorage.removeItem(STORAGE_KEY);
      return false;
    }

    const remainingMs = SNOOZE_MS - (Date.now() - dismissedAt);
    if (remainingMs <= 0) {
      await AsyncStorage.removeItem(STORAGE_KEY);
      return false;
    }

    return true;
  } catch {
    return false;
  }
}
