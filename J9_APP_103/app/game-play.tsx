import { Button } from '@/components/ui/button';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { consumePendingGamePlay } from '@/lib/pending-game-play';
import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { ChevronLeft } from 'lucide-react-native';
import { useMemo, useState } from 'react';
import { ActivityIndicator, Linking, Platform, Pressable, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { WebView } from 'react-native-webview';

function pickParam(value?: string | string[]) {
  const raw = Array.isArray(value) ? value[0] : value;
  return raw?.trim() || '';
}

function shouldLoadInsideWebView(url: string) {
  return (
    url.startsWith('http://') ||
    url.startsWith('https://') ||
    url.startsWith('about:blank') ||
    url.startsWith('data:')
  );
}

export default function GamePlayScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const params = useLocalSearchParams<{ title?: string | string[] }>();
  const titleParam = pickParam(params.title);

  const session = useMemo(() => {
    const pending = consumePendingGamePlay();
    return {
      url: pending?.url ?? '',
      title: titleParam || pending?.title || '游戏',
    };
  }, [titleParam]);

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);

  const handleClose = () => {
    if (router.canGoBack()) {
      router.back();
      return;
    }
    router.replace('/');
  };

  if (Platform.OS === 'web') {
    return (
      <>
        <Stack.Screen options={{ headerShown: false }} />
        <View className="flex-1 items-center justify-center bg-[#0f1420] px-6">
          <Text className="text-center text-[15px] text-[#d7def0]">
            请在 iOS 或 Android 应用内打开游戏。
          </Text>
          <Button onPress={handleClose} className="mt-4 h-11 rounded-2xl">
            <Text className="font-bold text-white">返回</Text>
          </Button>
        </View>
      </>
    );
  }

  if (!session.url) {
    return (
      <>
        <Stack.Screen options={{ headerShown: false }} />
        <View className="flex-1 items-center justify-center bg-[#0f1420] px-6">
          <Text className="text-center text-[18px] font-bold text-white">游戏地址无效</Text>
          <Text className="mt-2 text-center text-[14px] text-[#9ea8c0]">
            请返回后重新进入游戏。
          </Text>
          <Button onPress={handleClose} className="mt-6 h-12 rounded-2xl">
            <Text className="font-bold text-white">返回</Text>
          </Button>
        </View>
      </>
    );
  }

  const floatingBackTop = insets.top + 8;

  return (
    <>
      <Stack.Screen options={{ headerShown: false, animation: 'fade' }} />
      <View className="flex-1 bg-black">
        <View className="relative flex-1">
          <WebView
            source={{ uri: session.url }}
            style={{ flex: 1, backgroundColor: '#000000' }}
            startInLoadingState
            javaScriptEnabled
            domStorageEnabled
            sharedCookiesEnabled
            allowsInlineMediaPlayback
            mediaPlaybackRequiresUserAction={false}
            setSupportMultipleWindows={false}
            onLoadStart={() => {
              setLoading(true);
              setLoadError(false);
            }}
            onLoadEnd={() => setLoading(false)}
            onError={() => {
              setLoading(false);
              setLoadError(true);
            }}
            onShouldStartLoadWithRequest={(request) => {
              if (shouldLoadInsideWebView(request.url)) {
                return true;
              }
              void Linking.openURL(request.url);
              return false;
            }}
          />

          {loading ? (
            <View className="absolute inset-0 items-center justify-center bg-[#0f1420]/80">
              <ActivityIndicator size="large" color="#7B5CFF" />
              <Text className="mt-3 text-[14px] text-[#d7def0]">游戏加载中...</Text>
            </View>
          ) : null}

          {loadError ? (
            <View
              className="absolute inset-x-4 rounded-2xl border border-[#6e3145] bg-[#3a1e28] px-4 py-3"
              style={{ top: floatingBackTop + 52 }}>
              <Text className="text-center text-[14px] font-bold text-white">页面加载失败</Text>
              <Text className="mt-1 text-center text-[13px] text-[#ff9fbb]">
                请检查网络后下拉刷新，或关闭后重试。
              </Text>
            </View>
          ) : null}

          <Pressable
            onPress={handleClose}
            hitSlop={10}
            accessibilityRole="button"
            accessibilityLabel="关闭游戏"
            style={{
              position: 'absolute',
              top: floatingBackTop,
              right: 12,
              zIndex: 30,
              elevation: 6,
              shadowColor: '#000',
              shadowOffset: { width: 0, height: 1 },
              shadowOpacity: 0.35,
              shadowRadius: 4,
            }}
            className="size-9 items-center justify-center rounded-full border border-[#3d4558] bg-[#171d2a]/90">
            <View style={{ transform: [{ rotate: '90deg' }] }}>
              <Icon as={ChevronLeft} size={14} color="#FFFFFF" />
            </View>
          </Pressable>
        </View>
      </View>
    </>
  );
}
