import http from 'node:http';
import https from 'node:https';
import { URL } from 'node:url';

export const API_PROXY_PATH = '/__api_proxy';

function normalizeBaseUrl(url) {
  return String(url || '').replace(/\/+$/, '');
}

function setCorsHeaders(response) {
  response.setHeader('Access-Control-Allow-Origin', '*');
  response.setHeader('Access-Control-Allow-Methods', 'GET,POST,PUT,PATCH,DELETE,OPTIONS');
  response.setHeader(
    'Access-Control-Allow-Headers',
    'Content-Type, Authorization, X-Client-Platform, Cache-Control, Pragma, Expires'
  );
}

export function createApiProxyHandler(options = {}) {
  const targetBase = normalizeBaseUrl(
    options.targetBase || process.env.EXPO_PUBLIC_API_URL || 'https://bc.moneysb.com'
  );
  const proxyPath = options.proxyPath || API_PROXY_PATH;

  return (request, response, next) => {
    const rawUrl = request.url || '';
    if (!rawUrl.startsWith(proxyPath)) {
      next?.();
      return false;
    }

    if (request.method === 'OPTIONS') {
      setCorsHeaders(response);
      response.statusCode = 204;
      response.end();
      return true;
    }

    const targetUrl = new URL(rawUrl.slice(proxyPath.length) || '/', `${targetBase}/`);
    const client = targetUrl.protocol === 'https:' ? https : http;
    const headers = { ...request.headers };
    delete headers.host;
    headers.host = targetUrl.host;

    const proxyRequest = client.request(
      {
        protocol: targetUrl.protocol,
        hostname: targetUrl.hostname,
        port: targetUrl.port,
        path: `${targetUrl.pathname}${targetUrl.search}`,
        method: request.method,
        headers,
      },
      (proxyResponse) => {
        setCorsHeaders(response);
        response.writeHead(proxyResponse.statusCode || 502, proxyResponse.headers);
        proxyResponse.pipe(response);
      }
    );

    proxyRequest.on('error', (error) => {
      setCorsHeaders(response);
      response.statusCode = 502;
      response.setHeader('Content-Type', 'application/json; charset=utf-8');
      response.end(
        JSON.stringify({
          success: false,
          code: -1,
          message: `API 代理失败: ${error.message}`,
        })
      );
    });

    request.pipe(proxyRequest);
    return true;
  };
}

export function createApiProxyMiddleware(options = {}) {
  const handler = createApiProxyHandler(options);

  return (request, response, next) => {
    handler(request, response, next);
  };
}
