import { copyFileSync, existsSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';

const root = resolve(import.meta.dirname, '..');
const filesToCopy = [
  {
    source: resolve(root, 'assets/1.apk.zip'),
    targets: [
      resolve(root, 'public/downloads/1.apk.zip'),
      resolve(root, 'dist/downloads/1.apk.zip'),
    ],
  },
];

for (const file of filesToCopy) {
  if (!existsSync(file.source)) {
    console.warn(`[sync-web-download-assets] source file not found: ${file.source}`);
    continue;
  }

  for (const target of file.targets) {
    mkdirSync(dirname(target), { recursive: true });
    copyFileSync(file.source, target);
    console.log(`[sync-web-download-assets] copied to ${target}`);
  }
}
