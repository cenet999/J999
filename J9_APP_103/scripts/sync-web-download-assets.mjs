import { copyFileSync, existsSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';

const root = resolve(import.meta.dirname, '..');
const sourceApk = resolve(root, 'assets/1.apk');
const targets = [
  resolve(root, 'public/downloads/1.apk'),
  resolve(root, 'dist/downloads/1.apk'),
];

if (!existsSync(sourceApk)) {
  console.warn(`[sync-web-download-assets] source file not found: ${sourceApk}`);
  process.exit(0);
}

for (const target of targets) {
  mkdirSync(dirname(target), { recursive: true });
  copyFileSync(sourceApk, target);
  console.log(`[sync-web-download-assets] copied to ${target}`);
}
