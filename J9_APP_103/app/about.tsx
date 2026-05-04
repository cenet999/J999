import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Pg51InnerPageTopBar } from '@/components/pg51-clone/inner-page-top-bar';
import { Pg51InnerPage, Pg51SectionCard } from '@/components/pg51-clone/page-ui';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { LinearGradient } from 'expo-linear-gradient';
import { Stack, useRouter } from 'expo-router';
import { Award, ChevronRight, Globe, Heart, Shield } from 'lucide-react-native';
import { Pressable, View } from 'react-native';

export default function AboutScreen() {
  const router = useRouter();

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51InnerPage
        title="关于我们"
        subtitle="平台简介与服务说明"
        tag="平台信息"
        tone="purple"
        hideHero>
        <Pg51InnerPageTopBar
          onBack={() => router.back()}
          icon={Globe}
          iconColor="#c9b3ff"
          title="关于我们"
          subtitle="平台简介与服务说明"
          tone="purple"
        />

        <View className="items-center gap-4 rounded-[28px] border border-[#39435a] bg-[#171d2a] p-6">
          <View className="overflow-hidden rounded-[28px]" style={{ width: 84, height: 84 }}>
            <LinearGradient
              colors={['#7B5CFF', '#FF5FA2']}
              start={{ x: 0, y: 0 }}
              end={{ x: 1, y: 1 }}
              style={{ flex: 1, alignItems: 'center', justifyContent: 'center' }}>
              <Text className="text-3xl font-extrabold text-white">J9</Text>
            </LinearGradient>
          </View>

          <View className="items-center gap-1">
            <Text className="text-xl font-extrabold text-white">J9 Club</Text>
            <Text className="text-[13px] font-semibold text-[#9fa8be]">
              稳定安全的数字娱乐平台
            </Text>
          </View>

          <View className="rounded-full bg-[#241d39] px-3 py-1.5">
            <Text className="text-[11px] font-bold text-[#c9b3ff]">v1.0.3</Text>
          </View>
        </View>

        <Pg51SectionCard title="平台介绍" description="账户、资金与服务体验">
          <Text className="text-[12px] leading-[21px] text-[#b7c0d6]">
            J9 Club 围绕账户管理、资金服务、会员权益与消息触达等核心场景持续优化，
            致力于为用户提供更稳定、更高效、更一致的平台体验。
          </Text>
        </Pg51SectionCard>

        <Pg51SectionCard title="核心优势" description="稳定服务与一致体验">
          <FeatureItem icon={Shield} title="安全保障" desc="账户与资金相关操作均配备必要的安全保护机制。" />
          <FeatureItem icon={Award} title="高效服务" desc="常用功能集中展示，核心操作路径更清晰顺畅。" />
          <FeatureItem
            icon={Globe}
            title="统一体验"
            desc="页面视觉与交互保持统一标准，浏览体验更稳定。"
          />
          <FeatureItem icon={Heart} title="持续优化" desc="围绕会员体验持续完善服务细节与平台内容。" />
        </Pg51SectionCard>

        <Pg51SectionCard title="相关条款" description="规则以正式公告为准">
          <LegalItem label="用户协议" />
          <LegalItem label="隐私政策" />
          <LegalItem label="责任声明" />
        </Pg51SectionCard>
      </Pg51InnerPage>
    </>
  );
}

function FeatureItem({ icon, title, desc }: { icon: any; title: string; desc: string }) {
  return (
    <View className="flex-row items-center gap-3 rounded-[20px] bg-[#212838] px-4 py-3">
      <Pg51LucideIconBadge icon={icon} />
      <View className="flex-1">
        <Text className="text-[14px] font-bold text-white">{title}</Text>
        <Text className="mt-1 text-[11px] leading-[18px] text-[#8f9ab2]">{desc}</Text>
      </View>
    </View>
  );
}

function LegalItem({ label }: { label: string }) {
  return (
    <Pressable className="flex-row items-center rounded-[20px] bg-[#212838] px-4 py-3">
      <Text className="flex-1 text-[13px] font-semibold text-white">{label}</Text>
      <Icon as={ChevronRight} size={16} color="#8f9ab2" />
    </Pressable>
  );
}
