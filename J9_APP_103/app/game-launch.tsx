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
import { router, useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Linking, Platform, View } from 'react-native';

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

export default function GameLaunchScreen() {
  const localRouter = useRouter();
  const params = useLocalSearchParams<{
    gameId?: string | string[];
    title?: string | string[];
    dGamePlatform?: string | string[];
  }>();
  const [status, setStatus] = useState<'preparing' | 'slow' | 'failed'>('preparing');
  const [stage, setStage] = useState<'recycling' | 'requesting' | 'opening'>('requesting');
  const [retryKey, setRetryKey] = useState(0);
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
  }, [dGamePlatform, gameId, retryKey]);

  return (
    <View className="flex-1 items-center justify-center bg-[#0f1420] px-6">
      <View className="w-full max-w-[420px] rounded-[28px] border border-[#313a4f] bg-[#171d2a] px-6 py-8">
        <View className="mb-5 size-16 items-center justify-center self-center rounded-full bg-[#232b3d]">
          <ActivityIndicator size="large" color="#7B5CFF" />
        </View>

        <Text className="text-center text-[22px] font-extrabold text-white">
          正在进入 {title}
          {status !== 'failed' ? `（${countdown}s）` : ''}
        </Text>

        <View
          className="mt-6 rounded-[20px] px-4 py-4"
          style={{ backgroundColor: status === 'failed' ? '#3a1e28' : '#212838' }}>
          <Text className="text-center text-[14px] font-bold text-white">
            {status === 'failed'
              ? '游戏地址打开失败'
              : stage === 'recycling'
                ? '正在从游戏平台回收余额'
                : stage === 'requesting'
                  ? '正在验证安全链接并获取游戏入口'
                  : '正在打开外部游戏地址'}
          </Text>
          <Text
            className="mt-2 text-center text-[13px] font-medium leading-[20px]"
            style={{ color: status === 'failed' ? '#ff9fbb' : '#9ea8c0' }}>
            {status === 'slow'
              ? stage === 'recycling'
                ? '主账户余额为 0，正在尝试把各游戏平台余额收回。'
                : stage === 'requesting'
                  ? '链接认证中，请稍候。'
                  : '外部页面打开较慢，请耐心等待。'
              : status === 'failed'
                ? '请重试一次，或返回上一页继续操作。'
                : stage === 'recycling'
                  ? '检测到可用余额为 0，先执行回收再进入游戏。'
                  : stage === 'requesting'
                    ? '正在通过加密认证安全获取游戏入口，请稍候。'
                    : '游戏地址已获取，正在为您打开外部页面。'}
          </Text>
        </View>

        <View className="mt-6 gap-3">
          <Button
            onPress={() => {
              setRetryKey((current) => current + 1);
            }}
            className="h-12 rounded-2xl"
            style={{ backgroundColor: '#7B5CFF' }}>
            <Text className="text-[15px] font-bold text-white">重新打开</Text>
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
