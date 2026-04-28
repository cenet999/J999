


const { getDefaultConfig } = require('expo/metro-config');
const { withNativeWind } = require('nativewind/metro');
const path = require('path');

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

module.exports = finalConfig;
