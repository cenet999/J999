import type { ImageSourcePropType } from 'react-native';

export type Pg51CategoryId =
  | 'hot'
  | 'live'
  | 'fishing'
  | 'electronic'
  | 'lottery'
  | 'sports'
  | 'card'
  | 'other';

export type Pg51Category = {
  id: Pg51CategoryId;
  label: string;
  icon: 'flame' | 'monitor' | 'tv' | 'fish' | 'grid' | 'ticket' | 'trophy' | 'shield';
};

export type Pg51QuickAction = {
  id: string;
  label: string;
};

export type Pg51Promo = {
  id: string;
  label: string;
  extra?: string;
  icon: ImageSourcePropType;
  dots?: number;
  activeDot?: number;
};

export type Pg51GameItem = {
  id: string;
  title: string;
  category: Pg51CategoryId;
  apiCode?: string;
  gameId?: string;
  dGamePlatform?: string;
  badge: string;
  badgeTone: 'purple' | 'blue' | 'orange' | 'gold' | 'gray' | 'green';
  multiplier?: string;
  image: ImageSourcePropType;
  aspectRatio?: number;
};
