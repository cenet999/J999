import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Pg51InnerPageTopBar } from '@/components/pg51-clone/inner-page-top-bar';
import { Pg51InnerPage, Pg51SectionCard } from '@/components/pg51-clone/page-ui';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Stack, useRouter } from 'expo-router';
import {
  Apple,
  CheckCircle2,
  ChevronRight,
  Download,
  ExternalLink,
  PlusSquare,
  Share2,
  ShieldCheck,
  Smartphone,
} from 'lucide-react-native';
import { Platform, Pressable, View } from 'react-native';

const ANDROID_APK_URL = '/downloads/1.apk.zip';

function openAndroidDownload() {
  if (Platform.OS === 'web' && typeof window !== 'undefined') {
    window.location.assign(ANDROID_APK_URL);
  }
}

export default function DownloadScreen() {
  const router = useRouter();

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51InnerPage title="APP 下载" subtitle="安卓安装包与 iOS 桌面快捷入口" tone="purple" hideHero>
        <Pg51InnerPageTopBar
          onBack={() => router.replace('/')}
          icon={Smartphone}
          iconColor="#c9b3ff"
          title="APP 下载"
          subtitle="安卓安装包与 iOS 添加到主屏幕"
          tone="purple"
        />

        <Pg51SectionCard title="安卓下载" description="点击按钮下载安装包">
          <View className="rounded-[24px] bg-[#212838] p-4">
            <View className="flex-row items-start gap-3">
              <Pg51LucideIconBadge icon={Download} size={46} iconSize={20} />
              <View className="flex-1">
                <Text className="text-[16px] font-black text-white">Android 安装包</Text>
                <Text className="mt-1 text-[12px] leading-[20px] text-[#9fa8be]">
                  下载完成后按系统提示安装，如出现安全提示，请选择继续安装。
                </Text>
              </View>
            </View>

            <Pressable
              onPress={openAndroidDownload}
              accessibilityRole="link"
              className="mt-4 flex-row items-center justify-center gap-2 rounded-[18px] bg-[#6f1dff] px-4 py-3.5">
              <Icon as={Download} size={18} className="text-white" />
              <Text className="text-[15px] font-black text-white">立即下载安卓版</Text>
              <Icon as={ExternalLink} size={16} className="text-white" />
            </Pressable>
          </View>
        </Pg51SectionCard>

        <Pg51SectionCard title="iOS 使用方式" description="使用 Safari 添加到主屏幕">
          <GuideStep
            icon={Apple}
            title="用 Safari 打开本站"
            desc="如果当前在微信、Telegram 或其它浏览器内，请先选择用 Safari 打开。"
          />
          <GuideStep
            icon={Share2}
            title="点击底部分享按钮"
            desc="在 Safari 底部工具栏点击“共享”按钮，打开系统分享面板。"
          />
          <GuideStep
            icon={PlusSquare}
            title="选择添加到主屏幕"
            desc="找到“添加到主屏幕”，确认名称后点击添加，即可像 App 一样从桌面进入。"
          />
        </Pg51SectionCard>

        <Pg51SectionCard title="安装提示" description="更稳定的访问体验">
          <TipRow label="建议使用最新版本浏览器或系统。" />
          <TipRow label="安装后请保留桌面图标，后续可直接进入平台。" />
          <TipRow label="如下载或添加失败，请联系在线客服处理。" />
        </Pg51SectionCard>
      </Pg51InnerPage>
    </>
  );
}

function GuideStep({ icon, title, desc }: { icon: any; title: string; desc: string }) {
  return (
    <View className="flex-row items-start gap-3 rounded-[20px] bg-[#212838] px-4 py-3">
      <Pg51LucideIconBadge icon={icon} />
      <View className="flex-1">
        <Text className="text-[14px] font-bold text-white">{title}</Text>
        <Text className="mt-1 text-[12px] leading-[20px] text-[#9fa8be]">{desc}</Text>
      </View>
      <Icon as={ChevronRight} size={16} className="mt-3 text-[#8f9ab2]" />
    </View>
  );
}

function TipRow({ label }: { label: string }) {
  return (
    <View className="flex-row items-center gap-3 rounded-[18px] bg-[#212838] px-4 py-3">
      <Icon as={CheckCircle2} size={18} className="text-[#4ade80]" />
      <Text className="flex-1 text-[12px] leading-[19px] text-[#c6cee0]">{label}</Text>
      <Icon as={ShieldCheck} size={16} className="text-[#8f9ab2]" />
    </View>
  );
}
