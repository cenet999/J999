import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import type { Pg51GameItem } from '@/components/pg51-clone/types';
import { cn } from '@/lib/utils';
import { router } from 'expo-router';
import { LinearGradient } from 'expo-linear-gradient';
import { Image, Pressable, View } from 'react-native';

type Pg51GameCardProps = {
  item: Pg51GameItem;
  singleColumn?: boolean;
};

const LIVE_CARD_ASPECT_RATIO = 650 / 218;
const BADGE_SKEW = '-14deg';
const BADGE_SKEW_INVERSE = '14deg';
let isNavigatingToGameLaunch = false;

const BADGE_TONE: Record<Pg51GameItem['badgeTone'], string> = {
  purple: '#7b2bff',
  blue: '#1fb1ff',
  orange: '#ff7a2b',
  gold: '#d49a3a',
  gray: '#4a505c',
  green: '#1aa86d',
};

export function Pg51GameCard({ item, singleColumn = false }: Pg51GameCardProps) {
  const isLiveCard = singleColumn;
  const singleColumnAspectRatio = item.aspectRatio ?? LIVE_CARD_ASPECT_RATIO;

  const handleLaunch = async () => {
    if (isNavigatingToGameLaunch) {
      Toast.show({ type: 'info', text1: '正在进入，请稍候' });
      return;
    }

    if (!item.gameId) {
      Toast.show({ type: 'info', text1: '该游戏暂未开放启动' });
      return;
    }

    try {
      isNavigatingToGameLaunch = true;
      router.push({
        pathname: '/game-launch',
        params: {
          title: item.title,
          gameId: item.gameId,
          dGamePlatform: item.dGamePlatform,
        },
      });
    } finally {
      setTimeout(() => {
        isNavigatingToGameLaunch = false;
      }, 300);
    }
  };

  return (
    <Pressable
      onPress={() => void handleLaunch()}
      className={cn(
        'overflow-hidden rounded-[22px] bg-[#2b3141]',
        isLiveCard ? 'w-full' : 'w-[31%]'
      )}>
      <View
        className={cn(
          'relative overflow-hidden rounded-[22px]',
          isLiveCard ? '' : 'h-[125px]'
        )}
        style={isLiveCard ? { aspectRatio: singleColumnAspectRatio } : undefined}>
        <Image source={item.image} style={{ width: '100%', height: '100%' }} resizeMode="cover" />

        {isLiveCard ? null : (
          <>
            {item.multiplier ? (
              <View
                className="absolute left-0 top-0 overflow-hidden"
                style={{
                  transform: [{ skewX: BADGE_SKEW }],
                  marginLeft: -4,
                  borderBottomRightRadius: 10,
                }}>
                <LinearGradient
                  colors={['#ff3b5c', '#ff7a00']}
                  start={{ x: 0, y: 0 }}
                  end={{ x: 1, y: 1 }}
                  style={{ paddingHorizontal: 10, paddingVertical: 2 }}>
                  <Text
                    className="text-[13px] font-black text-white"
                    style={{ transform: [{ skewX: BADGE_SKEW_INVERSE }] }}>
                    {item.multiplier}
                  </Text>
                </LinearGradient>
              </View>
            ) : null}

            <View
              className="absolute right-0 top-0 overflow-hidden"
              style={{
                transform: [{ skewX: BADGE_SKEW }],
                marginRight: -4,
                backgroundColor: BADGE_TONE[item.badgeTone],
                borderBottomLeftRadius: 10,
              }}>
              <Text
                className="text-[13px] font-black text-white"
                style={{
                  paddingHorizontal: 10,
                  paddingVertical: 2,
                  transform: [{ skewX: BADGE_SKEW_INVERSE }],
                }}>
                {item.badge}
              </Text>
            </View>

            <View className="absolute bottom-0 left-0 right-0 bg-black/35 px-2 py-2">
              <Text className="text-center text-[13px] font-semibold text-white" numberOfLines={1}>
                {item.title}
              </Text>
            </View>
          </>
        )}
      </View>
    </Pressable>
  );
}
