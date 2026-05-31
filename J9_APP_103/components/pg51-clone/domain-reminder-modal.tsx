import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import { markDomainReminderSnoozed } from '@/lib/domain-reminder-snooze';
import * as Clipboard from 'expo-clipboard';
import { useRouter } from 'expo-router';
import { Search } from 'lucide-react-native';
import * as React from 'react';
import { Image, Modal, Pressable, View } from 'react-native';

const PRIMARY_DOMAIN = '999137.com';
const PRIMARY_URL = 'https://999137.com';
const MIRROR_DOMAINS = [
  'bcpt0.com',
  'bcpt1.com',
  'bcpt3.com',
  'bcpt4.com',
  'bcpt7.com',
  'bcpt9.com',
] as const;

const brandLogo = require('@/assets/images/ios-light.png');

async function copyText(label: string, value: string) {
  try {
    await Clipboard.setStringAsync(value);
    Toast.show({ type: 'success', text1: `${label}已复制`, text2: value });
  } catch {
    Toast.show({ type: 'error', text1: '复制失败', text2: '请稍后重试' });
  }
}

async function copyAllDomains() {
  const lines = [PRIMARY_URL, ...MIRROR_DOMAINS.map((domain) => `https://${domain}`)];
  await copyText('全部域名', lines.join('\n'));
}

type DomainReminderModalProps = {
  visible: boolean;
  onClose: () => void;
};

export function DomainReminderModal({ visible, onClose }: DomainReminderModalProps) {
  const router = useRouter();

  const handleDismiss = React.useCallback(() => {
    void markDomainReminderSnoozed();
    onClose();
  }, [onClose]);

  const handleDownload = React.useCallback(() => {
    handleDismiss();
    router.push('/download');
  }, [handleDismiss, router]);

  const handleOpenTutorial = React.useCallback(() => {
    handleDismiss();
    router.push('/download');
  }, [handleDismiss, router]);

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={handleDismiss}>
      <Pressable
        className="flex-1 items-center justify-center bg-black/75 px-4 py-4"
        onPress={handleDismiss}>
        <Pressable
          onPress={(event) => event.stopPropagation()}
          className="relative w-full max-w-[420px] overflow-visible rounded-[24px] border border-[#3f4760] bg-[#1a1a1d] px-4 pb-3.5 pt-4">
          <Pressable
            onPress={handleDismiss}
            accessibilityRole="button"
            accessibilityLabel="关闭"
            className="absolute -right-2 -top-3 z-10 size-9 items-center justify-center rounded-full bg-[#6f1dff]">
            <Text className="text-[20px] font-bold leading-[20px] text-white">×</Text>
          </Pressable>

          <Text className="text-center text-[20px] font-black leading-tight text-white">
            牢记最新域名
          </Text>
          <View className="mt-1 flex-row flex-wrap items-center justify-center gap-2">
            <Text className="text-[13px] text-[#d6dbeb]">截图保存可以防止走丢哦</Text>
            <Pressable
              onPress={() => void copyAllDomains()}
              className="rounded-full bg-[#6f1dff] px-3 py-1">
              <Text className="text-[12px] font-bold text-white">保存</Text>
            </Pressable>
          </View>

          <View className="mt-2.5 flex-row items-center rounded-[16px] border border-[#2f3548] bg-[#111827] px-3 py-2">
            <Image
              source={brandLogo}
              style={{ width: 36, height: 36, borderRadius: 9 }}
              resizeMode="cover"
            />
            <Text className="mx-3 flex-1 text-[18px] font-black text-white">{PRIMARY_DOMAIN}</Text>
            <Pressable
              onPress={() => void copyText('域名', PRIMARY_URL)}
              className="rounded-full bg-[#6f1dff] px-4 py-1.5">
              <Text className="text-[12px] font-bold text-white">复制</Text>
            </Pressable>
          </View>

          <View className="mt-2 rounded-[16px] border border-[#2f3548] bg-[#111827] px-2 py-0.5">
            {MIRROR_DOMAINS.map((domain, index) => (
              <View
                key={domain}
                className={`flex-row items-center px-2 py-1.5 ${
                  index > 0 ? 'border-t border-[#2f3548]' : ''
                }`}>
                <Icon as={Search} size={16} className="text-[#9fa8be]" />
                <Text className="mx-2 flex-1 text-[14px] font-semibold text-white">
                  {domain.toUpperCase()}
                </Text>
                <Pressable
                  onPress={() => void copyText('域名', `https://${domain}`)}
                  className="rounded-full bg-[#6f1dff] px-3 py-1">
                  <Text className="text-[11px] font-bold text-white">复制</Text>
                </Pressable>
              </View>
            ))}
          </View>

          <Pressable
            onPress={handleDownload}
            className="mt-3 items-center rounded-full bg-[#6f1dff] py-2.5">
            <Text className="text-[15px] font-black text-white">下载 APP</Text>
          </Pressable>

          <Pressable onPress={handleOpenTutorial} className="mt-2 items-center py-0.5">
            <Text className="text-[13px] text-[#f0c05a] underline">点击查看安装教程</Text>
          </Pressable>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

export { MIRROR_DOMAINS, PRIMARY_DOMAIN, PRIMARY_URL };
