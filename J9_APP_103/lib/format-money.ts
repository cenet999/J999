export function formatCny(amount: number | string, decimals: number = 2): string {
  const value = typeof amount === 'string' ? Number.parseFloat(amount) : amount;
  const safeValue = Number.isFinite(value) ? value : 0;
  const text = decimals === 0 ? String(Math.round(safeValue)) : safeValue.toFixed(decimals);
  return `¥${text}`;
}

export function formatSignedCny(amount: number): string {
  const abs = Math.abs(amount).toFixed(2);
  return amount >= 0 ? `+¥${abs}` : `-¥${abs}`;
}
