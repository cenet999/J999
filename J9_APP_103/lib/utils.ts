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

/** 过长文本中间省略，保留首尾便于识别（如 URL、地址） */
export function truncateMiddle(text: string, maxLength: number, ellipsis = '...'): string {
  if (!text || maxLength <= ellipsis.length || text.length <= maxLength) {
    return text;
  }

  const keep = maxLength - ellipsis.length;
  const head = Math.ceil(keep / 2);
  const tail = Math.floor(keep / 2);
  return `${text.slice(0, head)}${ellipsis}${text.slice(-tail)}`;
}
