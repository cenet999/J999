import { copyFileSync, existsSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';

const root = resolve(import.meta.dirname, '..');
const androidApkFileName = 'application-8ba10fc6-decc-49c0-a653-27a64f0b8ba8.apk';
const filesToCopy = [
  {
    source: resolve(root, 'assets', androidApkFileName),
    targets: [
      resolve(root, 'public/downloads', androidApkFileName),
      resolve(root, 'dist/downloads', androidApkFileName),
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
