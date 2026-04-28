import { createReadStream, existsSync, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { extname, join, normalize, resolve } from 'node:path';

const root = resolve(process.cwd(), 'dist');
const port = Number(process.env.PORT || 4173);

const mimeTypes = {
  '.css': 'text/css; charset=utf-8',
  '.html': 'text/html; charset=utf-8',
  '.ico': 'image/x-icon',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.js': 'application/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.txt': 'text/plain; charset=utf-8',
  '.webp': 'image/webp',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
};

function sendNotFound(response) {
  response.statusCode = 404;
  response.setHeader('Content-Type', 'text/plain; charset=utf-8');
  response.end('Not Found');
}

function resolveFilePath(urlPath) {
  const pathname = decodeURIComponent(urlPath.split('?')[0] || '/');
  const normalizedPath = normalize(pathname).replace(/^(\.\.[/\\])+/, '');
  const directTarget = join(root, normalizedPath);

  if (existsSync(directTarget) && statSync(directTarget).isFile()) {
    return directTarget;
  }

  if (existsSync(directTarget) && statSync(directTarget).isDirectory()) {
    const indexTarget = join(directTarget, 'index.html');
    if (existsSync(indexTarget)) return indexTarget;
  }

  if (!extname(normalizedPath)) {
    const htmlTarget = join(root, `${normalizedPath.replace(/\/$/, '') || '/index'}.html`);
    if (existsSync(htmlTarget)) {
      return htmlTarget;
    }
  }

  const notFoundTarget = join(root, '+not-found.html');
  return existsSync(notFoundTarget) ? notFoundTarget : null;
}

const server = createServer((request, response) => {
  const method = request.method || 'GET';
  if (method !== 'GET' && method !== 'HEAD') {
    response.statusCode = 405;
    response.end();
    return;
  }

  const filePath = resolveFilePath(request.url || '/');
  if (!filePath) {
    sendNotFound(response);
    return;
  }

  const contentType = mimeTypes[extname(filePath).toLowerCase()] || 'application/octet-stream';
  response.statusCode = filePath.endsWith('+not-found.html') ? 404 : 200;
  response.setHeader('Content-Type', contentType);

  if (method === 'HEAD') {
    response.end();
    return;
  }

  createReadStream(filePath).pipe(response);
});

server.listen(port, '127.0.0.1', () => {
  console.log(`Serving dist at http://127.0.0.1:${port}`);
});
