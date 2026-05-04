import { clsx, type ClassValue } from 'clsx';
import type { ImageSourcePropType } from 'react-native';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/** 从 React Native 图片来源里取出远程 URL（本地 require 资源返回 undefined） */
export function extractImageSourceUri(source: ImageSourcePropType): string | undefined {
  if (typeof source === 'number') return undefined;
  const uri = (source as { uri?: string }).uri?.trim();
  return uri || undefined;
}
