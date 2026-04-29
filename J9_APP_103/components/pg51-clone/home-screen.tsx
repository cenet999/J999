import { useAuthModal } from '@/components/auth/auth-modal-provider';
import {
  Pg51HeaderInner,
  Pg51PageShell,
  Pg51SideNav,
  Pg51TrackedScrollView,
} from '@/components/pg51-clone/chrome';
import { Pg51GameCard } from '@/components/pg51-clone/game-card';
import type { Pg51Category, Pg51CategoryId, Pg51GameItem } from '@/components/pg51-clone/types';
import { getGameList, type BackendGame } from '@/lib/api/game';
import { getNotices, type Notice } from '@/lib/api/notice';
import { apiOk, toAbsoluteUrl } from '@/lib/api/request';
import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import * as React from 'react';
import {
  ActivityIndicator,
  Animated,
  Easing,
  Image,
  Modal,
  Platform,
  Pressable,
  ScrollView,
  View,
} from 'react-native';

const pg51Categories: Pg51Category[] = [
  { id: 'hot', label: '热门', icon: 'flame' },
  { id: 'card', label: '棋牌', icon: 'grid' },
  { id: 'electronic', label: '电子', icon: 'monitor' },
  { id: 'live', label: '视讯', icon: 'tv' },
  { id: 'fishing', label: '捕鱼', icon: 'fish' },
  { id: 'lottery', label: '彩票', icon: 'ticket' },
  { id: 'sports', label: '体育', icon: 'trophy' },
  { id: 'other', label: '电竞', icon: 'shield' },
];

const singleColumnCategories: Pg51CategoryId[] = ['live', 'sports', 'other'];
const defaultBannerImage = require('@/assets/pg51/brand/brand-logo.png');
const GAME_PAGE_SIZE = 50;
const LOAD_MORE_THRESHOLD = 120;
const NOTICE_ROW_HEIGHT = 18;
const NOTICE_SCROLL_INTERVAL = 5000;
const NOTICE_SCROLL_DURATION = 320;
const WEB_DOWNLOAD_BANNER_IMAGE = '/images/download-app-banner.png';
const WEB_DOWNLOAD_BANNER_LINK = '/downloads/1.apk.zip';

type HomeNotice = {
  id: string;
  title: string;
  content: string;
  createdTime: string;
};

function pickText(...values: Array<string | number | null | undefined>) {
  for (const value of values) {
    if (typeof value === 'string' && value.trim()) return value.trim();
    if (typeof value === 'number') return String(value);
  }
  return '';
}

function stripHtml(html: string) {
  return html
    .replace(/<[^>]+>/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function normalizeApiCode(value?: string) {
  return value?.trim().toUpperCase() ?? '';
}

function resolveDGamePlatformName(item: BackendGame) {
  const platform = item.dGamePlatform ?? item.DGamePlatform;
  if (!platform) return '';

  if (typeof platform === 'string') {
    return platform.trim();
  }

  return pickText(platform.name, platform.Name);
}

function formatNoticeTime(value?: string) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  return `${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(
    2,
    '0'
  )}`;
}

function resolveGameType(item: BackendGame) {
  const rawValue = item.gameType ?? item.GameType ?? 0;

  if (typeof rawValue === 'number') return rawValue;

  if (typeof rawValue === 'string') {
    const numericValue = Number(rawValue);
    if (!Number.isNaN(numericValue)) return numericValue;

    const normalized = rawValue.trim().toLowerCase();
    if (normalized === 'live') return 1;
    if (normalized === 'fishing') return 2;
    if (normalized === 'electronic') return 3;
    if (normalized === 'lottery') return 4;
    if (normalized === 'sports') return 5;
    if (normalized === 'card') return 6;
    if (normalized === 'other') return 7;
  }

  return 0;
}

function resolveGameName(item: BackendGame) {
  return pickText(item.gameCnName, item.GameCnName, item.gameName, item.GameName) || '热门游戏';
}

type BadgeTone = 'purple' | 'blue' | 'orange' | 'gold' | 'gray' | 'green';

const BADGE_TONE_BY_CODE: Record<string, BadgeTone> = {
  PG: 'gray',
  JDB: 'orange',
  MG: 'gold',
  CQ9: 'orange',
  DB: 'blue',
  TF: 'purple',
  IM: 'blue',
  BBIN: 'blue',
  KY: 'green',
  AG: 'purple',
  PA: 'orange',
  GDQ: 'gold',
  KX: 'green',
  J9: 'purple',
};

const BADGE_TONE_PALETTE: BadgeTone[] = ['purple', 'blue', 'orange', 'gold', 'gray', 'green'];

function pickToneFromCode(code: string): BadgeTone {
  if (!code) return 'purple';
  let hash = 0;
  for (let i = 0; i < code.length; i += 1) {
    hash = (hash * 31 + code.charCodeAt(i)) >>> 0;
  }
  return BADGE_TONE_PALETTE[hash % BADGE_TONE_PALETTE.length];
}

function resolveBadge(_name: string, apiCode: string) {
  const upperCode = apiCode.toUpperCase();

  if (!upperCode) {
    return { badge: 'J9', badgeTone: 'purple' as BadgeTone };
  }

  const tone = BADGE_TONE_BY_CODE[upperCode] ?? pickToneFromCode(upperCode);
  return { badge: upperCode, badgeTone: tone };
}

function mapBackendCategory(item: BackendGame): Pg51CategoryId {
  const gameType = resolveGameType(item);

  if (gameType === 1) return 'live';
  if (gameType === 2) return 'fishing';
  if (gameType === 3) return 'electronic';
  if (gameType === 4) return 'lottery';
  if (gameType === 5) return 'sports';
  if (gameType === 6) return 'card';
  if (gameType === 7) return 'other';

  return 'other';
}

function buildMultiplier(name: string, item: BackendGame) {
  const description = pickText(item.description, item.Description).toLowerCase();
  if (description.includes('万倍')) {
    const matched = description.match(/(\d+(\.\d+)?万倍)/);
    if (matched?.[1]) return matched[1];
  }

  if (name.includes('赏金')) return '高热度';
  return undefined;
}

function mapGameItem(item: BackendGame): Pg51GameItem {
  const name = resolveGameName(item);
  const apiCode = normalizeApiCode(pickText(item.apiCode, item.ApiCode));
  const icon = pickText(item.icon, item.Icon);
  const { badge, badgeTone } = resolveBadge(name, apiCode);

  return {
    id: pickText(item.id, item.Id, name),
    title: name,
    category: mapBackendCategory(item),
    apiCode,
    gameId: pickText(item.id, item.Id),
    dGamePlatform: resolveDGamePlatformName(item),
    badge,
    badgeTone,
    multiplier: buildMultiplier(name, item),
    image: icon ? { uri: toAbsoluteUrl(icon) } : defaultBannerImage,
    aspectRatio: singleColumnCategories.includes(mapBackendCategory(item)) ? 650 / 218 : undefined,
  };
}

function mapNotice(item: Notice): HomeNotice {
  return {
    id: pickText(item.id, item.Id),
    title: pickText(item.title, item.Title) || '平台公告',
    content: stripHtml(pickText(item.content, item.Content) || '暂无最新公告内容'),
    createdTime: pickText(item.createdTime, item.CreatedTime),
  };
}

function mergeGameItems(current: Pg51GameItem[], incoming: Pg51GameItem[]) {
  const gameMap = new Map(current.map((item) => [item.id, item]));

  for (const item of incoming) {
    gameMap.set(item.id, item);
  }

  return Array.from(gameMap.values());
}

function matchesCategory(category: Pg51CategoryId, item: Pg51GameItem) {
  if (category === 'hot') return true;
  return item.category === category;
}

function getRequestType(category: Pg51CategoryId) {
  if (category === 'hot') return 0;
  if (category === 'live') return 1;
  if (category === 'fishing') return 2;
  if (category === 'electronic') return 3;
  if (category === 'lottery') return 4;
  if (category === 'sports') return 5;
  if (category === 'card') return 6;
  if (category === 'other') return 7;
  return 0;
}

export function Pg51CloneHomeScreen() {
  const [activeCategory, setActiveCategory] = React.useState<Pg51CategoryId>('hot');
  const [categoryAnchorY, setCategoryAnchorY] = React.useState<number | null>(null);
  const [categorySticky, setCategorySticky] = React.useState(false);
  const [gameLoading, setGameLoading] = React.useState(true);
  const [noticeLoading, setNoticeLoading] = React.useState(true);
  const [noticeModalVisible, setNoticeModalVisible] = React.useState(false);
  const [notices, setNotices] = React.useState<HomeNotice[]>([]);
  const [activeNoticeIndex, setActiveNoticeIndex] = React.useState(0);
  const [queuedNoticeIndex, setQueuedNoticeIndex] = React.useState<number | null>(null);
  const [games, setGames] = React.useState<Pg51GameItem[]>([]);
  const [apiCodeOptions, setApiCodeOptions] = React.useState<string[]>([]);
  const [selectedApiCode, setSelectedApiCode] = React.useState('');
  const [listPage, setListPage] = React.useState(1);
  const [hasMore, setHasMore] = React.useState(false);
  const [loadingMore, setLoadingMore] = React.useState(false);
  const loadingMoreRef = React.useRef(false);
  const categoryStickyRef = React.useRef(false);
  const noticeTranslateY = React.useRef(new Animated.Value(0)).current;
  const activeNoticeIndexRef = React.useRef(0);
  const noticeAnimatingRef = React.useRef(false);
  const { openAuthModal, isAuthenticated, displayName } = useAuthModal();
  const isSingleColumnCategory = singleColumnCategories.includes(activeCategory);
  const supportsApiCodeFilter = activeCategory === 'electronic' || activeCategory === 'card';

  React.useEffect(() => {
    activeNoticeIndexRef.current = activeNoticeIndex;
  }, [activeNoticeIndex]);

  React.useEffect(() => {
    if (!supportsApiCodeFilter) {
      setSelectedApiCode('');
    }
  }, [supportsApiCodeFilter]);

  React.useEffect(() => {
    setListPage(1);
    setHasMore(false);
    setLoadingMore(false);
    loadingMoreRef.current = false;
  }, [activeCategory, selectedApiCode]);

  React.useEffect(() => {
    let cancelled = false;

    const loadNotices = async () => {
      setNoticeLoading(true);
      try {
        const result = await getNotices();
        if (!cancelled && apiOk(result) && result.data?.length) {
          setNotices(result.data.map(mapNotice));
        } else if (!cancelled) {
          setNotices([]);
        }
      } finally {
        if (!cancelled) setNoticeLoading(false);
      }
    };

    loadNotices();

    return () => {
      cancelled = true;
    };
  }, []);

  React.useEffect(() => {
    let cancelled = false;

    const loadGames = async () => {
      setGameLoading(true);
      try {
        const requestType = getRequestType(activeCategory);
        const requestLimit = GAME_PAGE_SIZE;

        if (supportsApiCodeFilter) {
          const tagResult = await getGameList('', requestType, 1, 200);
          const result = selectedApiCode
            ? await getGameList('', requestType, 1, GAME_PAGE_SIZE, '', selectedApiCode)
            : tagResult;

          if (!cancelled) {
            if (apiOk(tagResult) && tagResult.data) {
              const nextApiCodes = Array.from(
                new Set(
                  tagResult.data
                    .map((item) => normalizeApiCode(pickText(item.apiCode, item.ApiCode)))
                    .filter(Boolean)
                )
              );
              setApiCodeOptions(nextApiCodes);
            } else {
              setApiCodeOptions([]);
            }
          }

          if (!cancelled && apiOk(result) && result.data) {
            let usedFallback = false;
            let mapped = result.data
              .map(mapGameItem)
              .filter((item) => matchesCategory(activeCategory, item));

            if (selectedApiCode) {
              mapped = mapped.filter((item) => item.apiCode === selectedApiCode);
            }

            if (result.data.length > 0 && mapped.length === 0) {
              const fallbackResult = await getGameList('', 0, 1, 200);
              if (apiOk(fallbackResult) && fallbackResult.data) {
                usedFallback = true;
                mapped = fallbackResult.data
                  .map(mapGameItem)
                  .filter((item) => matchesCategory(activeCategory, item));

                if (selectedApiCode) {
                  mapped = mapped.filter((item) => item.apiCode === selectedApiCode);
                }
              }
            }

            setGames(selectedApiCode ? mapped : mapped.slice(0, GAME_PAGE_SIZE));
            setListPage(1);
            setHasMore(
              selectedApiCode
                ? !usedFallback && result.data.length === GAME_PAGE_SIZE
                : !usedFallback && result.data.length > GAME_PAGE_SIZE
            );
          } else if (!cancelled) {
            setGames([]);
            setHasMore(false);
          }

          return;
        }

        if (!cancelled) {
          setApiCodeOptions([]);
        }

        const result = await getGameList('', requestType, 1, requestLimit);

        if (!cancelled && apiOk(result) && result.data) {
          let usedFallback = false;
          let mapped = result.data
            .map(mapGameItem)
            .filter((item) => matchesCategory(activeCategory, item));

          if (requestType > 0 && result.data.length > 0 && mapped.length === 0) {
            const fallbackResult = await getGameList('', 0, 1, 200);
            if (apiOk(fallbackResult) && fallbackResult.data) {
              usedFallback = true;
              mapped = fallbackResult.data
                .map(mapGameItem)
                .filter((item) => matchesCategory(activeCategory, item));
            }
          }

          setGames(mapped);
          setListPage(1);
          setHasMore(
            activeCategory !== 'hot' && !usedFallback && result.data.length === GAME_PAGE_SIZE
          );
        } else if (!cancelled) {
          setGames([]);
          setHasMore(false);
        }
      } finally {
        if (!cancelled) setGameLoading(false);
      }
    };

    loadGames();

    return () => {
      cancelled = true;
    };
  }, [activeCategory, selectedApiCode, supportsApiCodeFilter]);

  React.useEffect(() => {
    noticeTranslateY.stopAnimation();
    noticeTranslateY.setValue(0);
    noticeAnimatingRef.current = false;
    setQueuedNoticeIndex(null);
    setActiveNoticeIndex(0);
    activeNoticeIndexRef.current = 0;
  }, [notices, noticeTranslateY]);

  React.useEffect(() => {
    if (noticeLoading || notices.length <= 1) return;

    const timer = setInterval(() => {
      if (noticeAnimatingRef.current) return;

      const nextIndex = (activeNoticeIndexRef.current + 1) % notices.length;
      if (nextIndex === activeNoticeIndexRef.current) return;

      noticeAnimatingRef.current = true;
      setQueuedNoticeIndex(nextIndex);
      noticeTranslateY.setValue(0);

      Animated.timing(noticeTranslateY, {
        toValue: -NOTICE_ROW_HEIGHT,
        duration: NOTICE_SCROLL_DURATION,
        easing: Easing.out(Easing.cubic),
        useNativeDriver: true,
      }).start(({ finished }) => {
        noticeTranslateY.setValue(0);
        setQueuedNoticeIndex(null);
        noticeAnimatingRef.current = false;

        if (!finished) return;

        activeNoticeIndexRef.current = nextIndex;
        setActiveNoticeIndex(nextIndex);
      });
    }, NOTICE_SCROLL_INTERVAL);

    return () => {
      clearInterval(timer);
      noticeTranslateY.stopAnimation();
      noticeTranslateY.setValue(0);
      setQueuedNoticeIndex(null);
      noticeAnimatingRef.current = false;
    };
  }, [noticeLoading, notices, noticeTranslateY]);

  const visibleGames =
    activeCategory === 'hot'
      ? games
      : games.filter(
          (item) =>
            item.category === activeCategory &&
            (!supportsApiCodeFilter || !selectedApiCode || item.apiCode === selectedApiCode)
        );
  const latestNotice = notices[activeNoticeIndex] ?? null;
  const upcomingNotice = queuedNoticeIndex === null ? null : (notices[queuedNoticeIndex] ?? null);

  const handleLoadMore = React.useCallback(async () => {
    if (gameLoading || loadingMoreRef.current || !hasMore) return;

    const nextPage = listPage + 1;
    const requestType = getRequestType(activeCategory);
    const apiCode = supportsApiCodeFilter ? selectedApiCode : '';

    loadingMoreRef.current = true;
    setLoadingMore(true);

    try {
      const result = await getGameList('', requestType, nextPage, GAME_PAGE_SIZE, '', apiCode);

      if (apiOk(result) && result.data) {
        let mapped = result.data
          .map(mapGameItem)
          .filter((item) => matchesCategory(activeCategory, item));

        if (apiCode) {
          mapped = mapped.filter((item) => item.apiCode === selectedApiCode);
        }

        setGames((current) => mergeGameItems(current, mapped));
        setListPage(nextPage);
        setHasMore(result.data.length === GAME_PAGE_SIZE);
      } else {
        setHasMore(false);
      }
    } finally {
      loadingMoreRef.current = false;
      setLoadingMore(false);
    }
  }, [activeCategory, gameLoading, hasMore, listPage, selectedApiCode, supportsApiCodeFilter]);

  const handleScroll = React.useCallback(
    (event: any) => {
      const currentY = event.nativeEvent.contentOffset.y;
      const nextSticky = categoryAnchorY !== null && currentY >= Math.max(0, categoryAnchorY);

      if (categoryStickyRef.current !== nextSticky) {
        categoryStickyRef.current = nextSticky;
        setCategorySticky(nextSticky);
      }

      if (!hasMore || gameLoading || loadingMoreRef.current) return;

      const { contentOffset, contentSize, layoutMeasurement } = event.nativeEvent;
      const distanceToBottom = contentSize.height - (contentOffset.y + layoutMeasurement.height);

      if (distanceToBottom <= LOAD_MORE_THRESHOLD) {
        void handleLoadMore();
      }
    },
    [categoryAnchorY, gameLoading, handleLoadMore, hasMore]
  );

  return (
    <Pg51PageShell>
      <View className="flex-1">
        <Pg51TrackedScrollView
          className="flex-1"
          contentContainerStyle={{ paddingBottom: 126 }}
          onScroll={handleScroll}
          showsVerticalScrollIndicator={false}>
          <WebDownloadBanner />
          <Pg51HeaderInner
            isAuthenticated={isAuthenticated}
            displayName={displayName}
            onLoginPress={() => openAuthModal('login')}
            onRegisterPress={() => openAuthModal('register')}
          />

          <View className="px-3.5 pb-1.5">
            <NoticeBar
              loading={noticeLoading}
              notice={latestNotice}
              nextNotice={upcomingNotice}
              translateY={noticeTranslateY}
              onPress={() => latestNotice && setNoticeModalVisible(true)}
            />
          </View>
          <View
            className="flex-row items-start gap-2 px-2"
            onLayout={(event) => setCategoryAnchorY(event.nativeEvent.layout.y)}>
            <View
              pointerEvents={categorySticky ? 'none' : 'auto'}
              style={{ opacity: categorySticky ? 0 : 1 }}>
              <Pg51SideNav
                categories={pg51Categories}
                activeCategory={activeCategory}
                onChange={setActiveCategory}
              />
            </View>

            <View className="flex-1">
              {supportsApiCodeFilter ? (
                <SlotApiCodeFilters
                  options={apiCodeOptions}
                  value={selectedApiCode}
                  onChange={setSelectedApiCode}
                />
              ) : null}

              <View
                className={`flex-row justify-start gap-2 ${
                  isSingleColumnCategory ? 'flex-col' : 'flex-wrap'
                }`}>
                {gameLoading ? (
                  <View className="w-full items-center rounded-[22px] bg-[#2b3141] px-4 py-8">
                    <ActivityIndicator color="#9b5cff" />
                    <Text className="mt-2 text-[12px] text-[#9ea6b7]">游戏列表加载中...</Text>
                  </View>
                ) : visibleGames.length > 0 ? (
                  visibleGames.map((item) => (
                    <Pg51GameCard key={item.id} item={item} singleColumn={isSingleColumnCategory} />
                  ))
                ) : (
                  <View className="w-full rounded-[22px] bg-[#2b3141] px-4 py-8">
                    <Text className="text-center text-[12px] text-[#9ea6b7]">
                      该分类暂无游戏内容
                    </Text>
                  </View>
                )}
              </View>

              <View className="mt-3 items-center">
                {loadingMore ? (
                  <View className="flex-row items-center gap-2">
                    <ActivityIndicator size="small" color="#9b5cff" />
                    <Text className="text-[12px] text-[#9ea6b7]">正在加载更多...</Text>
                  </View>
                ) : hasMore ? (
                  <Text className="text-center text-[12px] text-[#9ea6b7]">
                    滑到底部自动加载更多
                  </Text>
                ) : (
                  <Text className="text-center text-[12px] text-[#9ea6b7]">已展示全部内容</Text>
                )}
              </View>

              {!isAuthenticated ? (
                <View className="items-center pt-4">
                  <Pressable
                    onPress={() => openAuthModal('register')}
                    className="rounded-full bg-[#6f1dff] px-10 py-3">
                    <Text className="text-[15px] font-bold text-white">开户注册</Text>
                  </Pressable>
                </View>
              ) : null}
            </View>
          </View>
        </Pg51TrackedScrollView>

        {categorySticky ? (
          <View
            pointerEvents="box-none"
            style={{ position: 'absolute', top: 0, left: 8, zIndex: 20 }}>
            <Pg51SideNav
              categories={pg51Categories}
              activeCategory={activeCategory}
              onChange={setActiveCategory}
            />
          </View>
        ) : null}
      </View>

      <NoticeModal
        visible={noticeModalVisible}
        notices={notices}
        onClose={() => setNoticeModalVisible(false)}
      />
    </Pg51PageShell>
  );
}

function WebDownloadBanner() {
  const handlePress = React.useCallback(() => {
    if (Platform.OS !== 'web') return;
    window.location.assign(WEB_DOWNLOAD_BANNER_LINK);
  }, []);

  if (Platform.OS !== 'web') return null;

  return (
    <View className="px-2 pb-2 pt-2">
      <Pressable
        onPress={handlePress}
        accessibilityRole="link">
        <Image
          source={{ uri: WEB_DOWNLOAD_BANNER_IMAGE }}
          style={{ width: '100%', aspectRatio: 577 / 81 }}
          resizeMode="contain"
        />
      </Pressable>
    </View>
  );
}

function SlotApiCodeFilters({
  options,
  value,
  onChange,
}: {
  options: string[];
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={{ paddingRight: 12 }}
      className="mb-3">
      <View className="flex-row items-center gap-2">
        <ApiCodeTag label="全部" active={!value} onPress={() => onChange('')} />
        {options.map((item) => (
          <ApiCodeTag
            key={item}
            label={item}
            active={value === item}
            onPress={() => onChange(item)}
          />
        ))}
      </View>
    </ScrollView>
  );
}

function ApiCodeTag({
  label,
  active,
  onPress,
}: {
  label: string;
  active: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      onPress={onPress}
      className={`rounded-full border px-3 py-1.5 ${
        active ? 'border-[#9b5cff] bg-[#6f1dff]' : 'border-[#424a60] bg-[#2b3141]'
      }`}>
      <Text className={`text-[12px] font-semibold ${active ? 'text-white' : 'text-[#c7cddd]'}`}>
        {label}
      </Text>
    </Pressable>
  );
}

function NoticeBar({
  loading,
  notice,
  nextNotice,
  translateY,
  onPress,
}: {
  loading: boolean;
  notice: HomeNotice | null;
  nextNotice: HomeNotice | null;
  translateY: Animated.Value;
  onPress: () => void;
}) {
  const currentText = notice ? `${formatNoticeTime(notice.createdTime)} ${notice.title}` : '';
  const nextText = nextNotice
    ? `${formatNoticeTime(nextNotice.createdTime)} ${nextNotice.title}`
    : '';

  return (
    <Pressable
      onPress={onPress}
      disabled={!notice}
      className="my-1 flex-row items-center rounded-[18px] border border-[#444d63] bg-[#2c3447] px-3 py-2">
      <Text className="mr-2 rounded-full bg-[#6f1dff] px-2 py-1 text-[10px] font-bold text-white">
        公告
      </Text>
      <View className="flex-1 overflow-hidden" style={{ height: NOTICE_ROW_HEIGHT }}>
        {loading ? (
          <Text
            className="text-[12px] text-[#d6dbeb]"
            numberOfLines={1}
            style={{ lineHeight: NOTICE_ROW_HEIGHT }}>
            公告加载中...
          </Text>
        ) : notice ? (
          <Animated.View
            style={{
              transform: [{ translateY: nextNotice ? translateY : 0 }],
            }}>
            <Text
              className="text-[12px] text-[#d6dbeb]"
              numberOfLines={1}
              style={{ height: NOTICE_ROW_HEIGHT, lineHeight: NOTICE_ROW_HEIGHT }}>
              {currentText}
            </Text>
            {nextNotice ? (
              <Text
                className="text-[12px] text-[#d6dbeb]"
                numberOfLines={1}
                style={{ height: NOTICE_ROW_HEIGHT, lineHeight: NOTICE_ROW_HEIGHT }}>
                {nextText}
              </Text>
            ) : null}
          </Animated.View>
        ) : (
          <Text
            className="text-[12px] text-[#d6dbeb]"
            numberOfLines={1}
            style={{ lineHeight: NOTICE_ROW_HEIGHT }}>
            暂无最新公告
          </Text>
        )}
      </View>
      <Text className="ml-2 text-[12px] text-[#9b5cff]">{notice ? '详情' : ''}</Text>
    </Pressable>
  );
}

function NoticeModal({
  visible,
  notices,
  onClose,
}: {
  visible: boolean;
  notices: HomeNotice[];
  onClose: () => void;
}) {
  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <Pressable className="flex-1 items-center justify-center bg-black/70 px-4" onPress={onClose}>
        <Pressable
          onPress={(event) => event.stopPropagation()}
          className="max-h-[72%] w-[460px] max-w-full items-start justify-start rounded-[28px] border border-[#41495f] bg-[#202737]">
          <View className="flex-row items-center justify-between border-b border-[#353d53] px-4 py-4">
            <Text className="text-[18px] font-black text-white">平台公告</Text>
            <Pressable
              onPress={onClose}
              className="size-8 items-center justify-center rounded-full bg-[#31384a]">
              <Text className="text-[18px] font-bold text-[#d2d7e4]">×</Text>
            </Pressable>
          </View>

          <ScrollView
            className="px-4 py-4"
            contentContainerStyle={{ paddingBottom: 8 }}
            showsVerticalScrollIndicator={false}>
            {notices.map((item, index) => (
              <View
                key={item.id || `${item.title}-${index}`}
                className={index === 0 ? '' : 'mt-4 border-t border-[#353d53] pt-4'}>
                <Text className="text-[15px] font-bold text-white">{item.title}</Text>
                <Text className="mt-1 text-[11px] text-[#94a0ba]">
                  {item.createdTime || '最新发布'}
                </Text>
                <Text className="mt-3 text-[13px] leading-[21px] text-[#d6dbeb]">
                  {item.content || '暂无最新公告内容'}
                </Text>
              </View>
            ))}
          </ScrollView>
        </Pressable>
      </Pressable>
    </Modal>
  );
}
