


const { getDefaultConfig } = require('expo/metro-config');
const { withNativeWind } = require('nativewind/metro');
const path = require('path');
const { createApiProxyMiddleware } = require('./scripts/create-api-proxy-handler.mjs');

const config = getDefaultConfig(__dirname);

// 配置路径别名
config.resolver.alias = {
  '@': path.resolve(__dirname),
};

const finalConfig = withNativeWind(config, { input: './global.css', inlineRem: 16 });

// 确保路径别名在应用 withNativeWind 后仍然保留
finalConfig.resolver.alias = {
  ...finalConfig.resolver.alias,
  '@': path.resolve(__dirname),
};

const apiProxyMiddleware = createApiProxyMiddleware();
const previousEnhanceMiddleware = finalConfig.server?.enhanceMiddleware;

finalConfig.server = {
  ...finalConfig.server,
  enhanceMiddleware: (middleware, server) => {
    const baseMiddleware = previousEnhanceMiddleware
      ? previousEnhanceMiddleware(middleware, server)
      : middleware;

    return (request, response, next) => {
      apiProxyMiddleware(request, response, () => baseMiddleware(request, response, next));
    };
  },
};

module.exports = finalConfig;
