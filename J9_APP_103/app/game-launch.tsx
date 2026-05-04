import { Button } from '@/components/ui/button';
import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import { getMemberInfo } from '@/lib/api/auth';
import {
  formatRecycleRecentGamesAmountLine,
  recycleRecentGames,
  startMsGame,
  startXhGame,
} from '@/lib/api/game';
import { toAbsoluteUrl } from '@/lib/api/request';
import { router, useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Image, Linking, Platform, View } from 'react-native';

const START_LAUNCH_DELAY_MS = 120;
const SLOW_HINT_MS = 4000;
const LAUNCH_COUNTDOWN_SECONDS = 30;

function pickNumber(...values: Array<string | number | null | undefined>) {
  for (const value of values) {
    if (value === null || value === undefined || value === '') continue;
    const num = typeof value === 'number' ? value : Number(value);
    if (!Number.isNaN(num)) return num;
  }
  return 0;
}

async function openGameUrl(url: string) {
  if (Platform.OS === 'web' && typeof window !== 'undefined') {
    const targetWindow = window as Window & {
      __J9_E2E_SKIP_EXTERNAL_GAME_NAV__?: boolean;
    };

    if (targetWindow.__J9_E2E_SKIP_EXTERNAL_GAME_NAV__) return;
    window.location.href = url;
    return;
  }

  await Linking.openURL(url);
}

function markAutoLaunchConsumed() {
  if (Platform.OS === 'web' && typeof window !== 'undefined') {
    const currentUrl = new URL(window.location.href);
    currentUrl.searchParams.set('autoLaunch', 'false');
    window.history.replaceState(
      window.history.state,
      '',
      `${currentUrl.pathname}${currentUrl.search}${currentUrl.hash}`
    );
    return;
  }

  router.setParams({ autoLaunch: 'false' });
}

export default function GameLaunchScreen() {
  const localRouter = useRouter();
  const params = useLocalSearchParams<{
    gameId?: string | string[];
    title?: string | string[];
    dGamePlatform?: string | string[];
    gameIcon?: string | string[];
    autoLaunch?: string | string[];
  }>();
  const [status, setStatus] = useState<'idle' | 'preparing' | 'slow' | 'failed'>('idle');
  const [stage, setStage] = useState<'recycling' | 'requesting' | 'opening'>('requesting');
  const [launchAttempt, setLaunchAttempt] = useState(0);
  const [countdown, setCountdown] = useState(LAUNCH_COUNTDOWN_SECONDS);

  const gameId = useMemo(() => {
    const raw = Array.isArray(params.gameId) ? params.gameId[0] : params.gameId;
    return raw?.trim() || '';
  }, [params.gameId]);

  const title = useMemo(() => {
    const raw = Array.isArray(params.title) ? params.title[0] : params.title;
    return raw || '游戏';
  }, [params.title]);

  const dGamePlatform = useMemo(() => {
    const raw = Array.isArray(params.dGamePlatform) ? params.dGamePlatform[0] : params.dGamePlatform;
    return raw?.trim() || '';
  }, [params.dGamePlatform]);

  const gameIconUri = useMemo(() => {
    const raw = Array.isArray(params.gameIcon) ? params.gameIcon[0] : params.gameIcon;
    const trimmed = raw?.trim() || '';
    if (!trimmed) return '';
    return toAbsoluteUrl(trimmed);
  }, [params.gameIcon]);

  const autoLaunch = useMemo(() => {
    const raw = Array.isArray(params.autoLaunch) ? params.autoLaunch[0] : params.autoLaunch;
    return raw?.trim().toLowerCase() === 'true';
  }, [params.autoLaunch]);

  const launchGameByPlatform = async (playerId: string, targetGameId: string) => {
    const normalizedPlatform = dGamePlatform.toUpperCase();
    if (normalizedPlatform.includes('XH') || normalizedPlatform.includes('星汇')) {
      return startXhGame(playerId, targetGameId);
    }
    return startMsGame(playerId, targetGameId);
  };

  const openResolvedUrl = async (url: string) => {
    try {
      setStage('opening');
      markAutoLaunchConsumed();
      await openGameUrl(url);
      if (Platform.OS !== 'web') {
        setTimeout(() => {
          if (localRouter.canGoBack()) {
            localRouter.back();
          } else {
            router.replace('/');
          }
        }, 150);
      }
    } catch {
      setStatus('failed');
      Toast.show({ type: 'error', text1: '打开失败', text2: '请稍后重试' });
    }
  };

  useEffect(() => {
    let active = true;
    const shouldLaunch = autoLaunch || launchAttempt > 0;

    if (!shouldLaunch) {
      setStatus('idle');
      setStage('requesting');
      setCountdown(LAUNCH_COUNTDOWN_SECONDS);
      return;
    }

    if (!gameId) {
      setStatus('failed');
      Toast.show({ type: 'error', text1: '缺少游戏参数', text2: '请返回后重试' });
      return;
    }

    setStatus('preparing');
    setStage('requesting');
    setCountdown(LAUNCH_COUNTDOWN_SECONDS);

    const countdownTimer = setInterval(() => {
      setCountdown((current) => (current > 0 ? current - 1 : 0));
    }, 1000);

    const launchTimer = setTimeout(() => {
      void (async () => {
        try {
          const memberRes = await getMemberInfo();
          if (!active) return;

          if (!memberRes.success || !memberRes.data) {
            setStatus('failed');
            if (Platform.OS !== 'ios') {
              Toast.show({
                type: 'error',
                text1: '请先登录后开始游戏',
                modal: true,
                duration: 5000,
              });
            }
            return;
          }

          const playerId = String(memberRes.data.id ?? memberRes.data.Id ?? '');
          if (!playerId) {
            setStatus('failed');
            Toast.show({
              type: 'error',
              text1: '账号信息不完整',
              text2: '请重新登录后再试',
              modal: true,
              duration: 5000,
            });
            return;
          }

          const walletBalance = pickNumber(
            memberRes.data.CreditAmount,
            memberRes.data.creditAmount
          );
          if (walletBalance <= 1) {
            setStage('recycling');
            const recycleResult = await recycleRecentGames(playerId);
            if (!active) return;

            if (!recycleResult.ok) {
              console.warn('[game-launch] 回收失败', recycleResult);
              setStatus('failed');
              Toast.show({
                type: 'error',
                text1: '回收失败',
                text2: recycleResult.message || '主账户余额不足，请稍后再试或先充值。',
                modal: true,
                duration: 5000,
              });
              return;
            }

            console.log('[game-launch] 回收结果', {
              ok: recycleResult.ok,
              partial: recycleResult.partial,
              totalRecycledCny: recycleResult.totalRecycledCny,
              message: recycleResult.message,
              details: recycleResult.details.map((d) => ({
                platform: d.platform,
                ok: d.ok,
                recycledCny: d.recycledCny,
                message: d.message,
                lines: d.lines,
              })),
            });

            const amountLine = formatRecycleRecentGamesAmountLine(recycleResult);

            if (recycleResult.partial) {
              Toast.show({
                type: 'info',
                text1: '部分回收完成',
                text2: amountLine,
                duration: 4500,
              });
            } else {
              Toast.show({
                type: 'success',
                text1: '回收成功',
                text2: amountLine,
                duration: 4500,
              });
            }
            setStage('requesting');
          }

          const launchRes = await launchGameByPlatform(playerId, gameId);
          if (!active) return;

          const url =
            launchRes.success && launchRes.data
              ? typeof launchRes.data === 'string'
                ? launchRes.data
                : launchRes.data.gameUrl
              : '';

          if (!url) {
            setStatus('failed');
            Toast.show({
              type: 'error',
              text1: launchRes.message || '游戏启动失败，请稍后重试',
              modal: true,
              duration: 5000,
            });
            return;
          }

          await openResolvedUrl(url);
        } catch {
          if (!active) return;
          setStatus('failed');
          Toast.show({
            type: 'error',
            text1: '网络连接异常',
            text2: '请检查网络后重试',
            modal: true,
            duration: 5000,
          });
        }
      })();
    }, START_LAUNCH_DELAY_MS);

    const slowTimer = setTimeout(() => {
      setStatus((current) => (current === 'failed' ? current : 'slow'));
    }, SLOW_HINT_MS);

    return () => {
      active = false;
      clearInterval(countdownTimer);
      clearTimeout(launchTimer);
      clearTimeout(slowTimer);
    };
  }, [autoLaunch, dGamePlatform, gameId, launchAttempt]);

  return (
    <View className="flex-1 items-center justify-center bg-[#0f1420] px-2">
      <View className="w-full max-w-[420px] rounded-[28px] border border-[#313a4f] bg-[#171d2a] px-6 py-8">
        <View className="mb-5 self-center">
          {gameIconUri ? (
            <View className="relative size-[104px] overflow-hidden rounded-[22px] border border-[#313a4f] bg-[#232b3d]">
              <Image
                accessibilityLabel={`${title} 图标`}
                source={{ uri: gameIconUri }}
                style={{ width: '100%', height: '100%' }}
                resizeMode="cover"
              />
              {status === 'idle' ? null : (
                <View className="absolute inset-0 items-center justify-center rounded-[22px] bg-black/45">
                  <ActivityIndicator size="large" color="#7B5CFF" />
                </View>
              )}
            </View>
          ) : (
            <View className="size-16 items-center justify-center rounded-full bg-[#232b3d]">
              {status === 'idle' ? null : <ActivityIndicator size="large" color="#7B5CFF" />}
            </View>
          )}
        </View>

        <Text className="text-center text-[22px] font-extrabold text-white">
          {status === 'idle' ? title : `正在进入 ${title}`}
          {status !== 'idle' && status !== 'failed' ? `（${countdown}s）` : ''}
        </Text>

        <View
          className="mt-6 rounded-[20px] px-4 py-4"
          style={{ backgroundColor: status === 'failed' ? '#3a1e28' : '#212838' }}>
          <Text className="text-center text-[14px] font-bold text-white">
            {status === 'idle'
              ? '游戏已准备就绪'
              : status === 'failed'
              ? '游戏地址打开失败'
              : stage === 'recycling'
                ? '正在从游戏平台回收余额'
                : stage === 'requesting'
                  ? '正在获取游戏入口'
                  : '正在打开外部游戏地址'}
          </Text>
          <Text
            className="mt-2 text-center text-[13px] font-medium leading-[20px]"
            style={{ color: status === 'failed' ? '#ff9fbb' : '#9ea8c0' }}>
            {status === 'idle'
              ? '点击下方按钮获取游戏入口。'
              : status === 'slow'
              ? stage === 'recycling'
                ? '主账户余额为 0，正在尝试把各游戏平台余额收回。'
                : stage === 'requesting'
                  ? '稍慢一点，请稍候。'
                  : '外部页面打开较慢，请耐心等待。'
              : status === 'failed'
                ? '请重试一次，或返回上一页继续操作。'
                : stage === 'recycling'
                  ? '检测到可用余额为 0，先执行回收再进入游戏。'
                  : stage === 'requesting'
                    ? '请稍候。'
                    : '游戏地址已获取，正在为您打开外部页面。'}
          </Text>
        </View>

        <View className="mt-6 gap-3">
          <Button
            onPress={() => {
              setLaunchAttempt((current) => current + 1);
            }}
            className="h-12 rounded-2xl"
            style={{ backgroundColor: '#7B5CFF' }}>
            <Text className="text-[15px] font-bold text-white">
              {status === 'idle' ? '开始游戏' : '重新打开'}
            </Text>
          </Button>

          <Button
            variant="outline"
            onPress={() => {
              if (localRouter.canGoBack()) {
                localRouter.back();
              } else {
                router.replace('/');
              }
            }}
            className="h-12 rounded-2xl border-0"
            style={{ backgroundColor: '#232b3d' }}>
            <Text className="text-[15px] font-bold text-[#d7def0]">返回上一页</Text>
          </Button>
        </View>
      </View>
    </View>
  );
}
