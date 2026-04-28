const path = require('path');
const worker = require('metro-transform-worker');

const PLACEHOLDER = '__CSS_INTEROP_DATA_PLACEHOLDER__';

async function transform(config, projectRoot, filename, data, options) {
  const baseTransform = config.cssInterop_transformerPath
    ? require(config.cssInterop_transformerPath).transform
    : worker.transform;

  if (path.dirname(filename) !== config.cssInterop_outputDirectory || filename.endsWith('.css')) {
    return baseTransform(config, projectRoot, filename, data, options);
  }

  const fakeFile =
    'import { injectData } from "react-native-css-interop/dist/runtime/native/styles";' +
    `injectData("${PLACEHOLDER}");`;

  const result = await baseTransform(
    config,
    projectRoot,
    filename,
    Buffer.from(fakeFile),
    options
  );

  const output = result.output[0];
  const code = output?.data?.code ?? '';
  const injectedCode = code.replace(
    new RegExp(`["']${PLACEHOLDER}["']`, 'g'),
    data.toString('utf8')
  );

  if (injectedCode === code) {
    throw new Error(`Failed to inject CSS interop data for ${filename}`);
  }

  return {
    ...result,
    output: [
      {
        ...output,
        data: {
          ...output.data,
          code: injectedCode,
        },
      },
    ],
  };
}

module.exports = { transform };
