import * as React from 'react';
import { Platform, TextInput, type TextInputProps } from 'react-native';

import { cn } from '@/lib/utils';

const Input = React.forwardRef<React.ElementRef<typeof TextInput>, TextInputProps>(
  ({ className, placeholderClassName, style, multiline, ...props }, ref) => {
    const isMultiline = Boolean(multiline);
    const platformStyle =
      Platform.OS === 'web'
        ? ({ outline: 'none' } as object)
        : isMultiline
          ? {
              paddingVertical: 10,
              textAlignVertical: 'top' as const,
              ...(Platform.OS === 'android' && { includeFontPadding: false }),
            }
          : {
              paddingVertical: 0,
              textAlignVertical: 'center' as const,
              ...(Platform.OS === 'android' && { includeFontPadding: false }),
              ...(Platform.OS === 'ios' && { lineHeight: 20 }),
            };

    return (
      <TextInput
        ref={ref}
        multiline={multiline}
        className={cn(
          'w-full rounded-xl border border-input bg-background px-4 text-base text-foreground placeholder:text-muted-foreground focus-visible:outline-none disabled:opacity-50',
          isMultiline ? 'min-h-12 h-auto' : 'h-12',
          className
        )}
        placeholderClassName={cn('text-muted-foreground', placeholderClassName)}
        style={[platformStyle, style].filter(Boolean) as TextInputProps['style']}
        {...props}
      />
    );
  }
);

Input.displayName = 'Input';

export { Input };
