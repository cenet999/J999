import { setPendingGamePlay } from '@/lib/pending-game-play';
import { router } from 'expo-router';
import { Platform } from 'react-native';

type OpenGameUrlOptions = {
  title?: string;
};

export async function openGameUrl(url: string, options: OpenGameUrlOptions = {}) {
  const title = options.title?.trim() || '游戏';

  if (Platform.OS === 'web' && typeof window !== 'undefined') {
    const targetWindow = window as Window & {
      __J9_E2E_SKIP_EXTERNAL_GAME_NAV__?: boolean;
    };

    if (targetWindow.__J9_E2E_SKIP_EXTERNAL_GAME_NAV__) return;
    window.location.href = url;
    return;
  }

  setPendingGamePlay({ url, title });
  router.push({
    pathname: '/game-play',
    params: { title },
  });
}
