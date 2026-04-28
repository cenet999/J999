import { ScrollViewStyleReset } from 'expo-router/html';
import { type PropsWithChildren } from 'react';

// This file is web-only and used to configure the root HTML for every
// web page during static rendering.
// The contents of this function only run in Node.js environments and
// do not have access to the DOM or browser APIs.
export default function Root({ children }: PropsWithChildren) {
  return (
    <html lang="zh-CN" className="bg-background">
      <head>
        <title>久游俱乐部</title>
        <meta charSet="utf-8" />
        <meta httpEquiv="X-UA-Compatible" content="IE=edge" />
        <meta
          name="viewport"
          content="width=device-width, initial-scale=1, shrink-to-fit=no, maximum-scale=1, minimum-scale=1, user-scalable=no, viewport-fit=cover"
        />
        <style
          dangerouslySetInnerHTML={{
            __html: `
              *,
              *::before,
              *::after {
                touch-action: manipulation;
              }
            `,
          }}
        />
        <script
          dangerouslySetInnerHTML={{
            __html: `
              document.addEventListener('gesturestart', function (event) {
                event.preventDefault();
              });

              document.addEventListener(
                'touchmove',
                function (event) {
                  if (event.touches.length > 1) {
                    event.preventDefault();
                  }
                },
                { passive: false }
              );

              var lastTouchEnd = 0;
              document.addEventListener(
                'touchend',
                function (event) {
                  var now = Date.now();
                  if (now - lastTouchEnd <= 300) {
                    event.preventDefault();
                  }
                  lastTouchEnd = now;
                },
                { passive: false }
              );
            `,
          }}
        />

        {/*
          Disable body scrolling on web. This makes ScrollView components work closer to how they do on native.
          However, body scrolling is often nice to have for mobile web. If you want to enable it, remove this line.
        */}
        <ScrollViewStyleReset />

        {/* Add any additional <head> elements that you want globally available on web... */}
      </head>
      <body>{children}</body>
    </html>
  );
}
