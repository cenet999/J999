import type { TimeLimitedEvent } from '@/lib/api/event';

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

function hashText(text: string) {
  let hash = 0;

  for (let i = 0; i < text.length; i += 1) {
    hash = (hash * 33 + text.charCodeAt(i)) % 1000003;
  }

  return hash;
}

export function getEventDisplayNumbers(item: TimeLimitedEvent) {
  const total = Math.max(item.total || 0, 0);
  const current = clamp(item.progress || 0, 0, total);

  if (total <= 0) {
    return {
      current: 0,
      total: 0,
      text: '0/0',
      heatText: '热度 0%',
    };
  }

  if (current >= total) {
    return {
      current: total,
      total,
      text: `${total}/${total}`,
      heatText: '热度 100%',
    };
  }

  const seed = hashText(`${item.name}|${item.desc}|${item.timeLeft}`);
  const minExtra = Math.max(1, Math.floor(total * 0.08));
  const maxExtra = Math.max(minExtra, Math.floor(total * 0.22));
  const extra = minExtra + (seed % (maxExtra - minExtra + 1));
  const displayCurrent = clamp(current + extra, current, Math.max(total - 1, current));
  const percent = Math.round((displayCurrent / total) * 100);

  return {
    current: displayCurrent,
    total,
    text: `${displayCurrent}/${total}`,
    heatText: `热度 ${percent}%`,
  };
}
