import { useAuthModal } from '@/components/auth/auth-modal-provider';
import { Pg51PageShell } from '@/components/pg51-clone/chrome';
import { Text } from '@/components/ui/text';
import { Pressable, View } from 'react-native';

type Pg51PlaceholderScreenProps = {
  title: string;
  description?: string;
  showAuthActions?: boolean;
};

export function Pg51PlaceholderScreen({
  title,
  description,
  showAuthActions = false,
}: Pg51PlaceholderScreenProps) {
  const { openAuthModal } = useAuthModal();

  return (
    <Pg51PageShell>
      <View className="flex-1 px-4 pt-5">
        <View className="rounded-[28px] border border-[#323a4d] bg-[#242c3d] px-5 py-6">
          <Text className="text-[28px] font-black text-white">{title}</Text>
          <Text className="mt-2 text-[14px] leading-[22px] text-[#a4aec4]">
            {description ?? '相关内容正在完善中，敬请留意后续更新。'}
          </Text>

          {showAuthActions ? (
            <View className="mt-5 rounded-[24px] border border-[#3c4560] bg-[#171d2a] p-4">
              <Text className="text-[18px] font-bold text-white">请先登录账户</Text>
              <Text className="mt-2 text-[13px] leading-[20px] text-[#9ea8c0]">
                登录后可查看个人资料、订单记录与账户信息。
              </Text>

              <View className="mt-4 flex-row gap-3">
                <Pressable
                  onPress={() => openAuthModal('login')}
                  className="flex-1 items-center justify-center rounded-2xl bg-[#b79249] px-4 py-3">
                  <Text className="text-[14px] text-white">登录账户</Text>
                </Pressable>

                <Pressable
                  onPress={() => openAuthModal('register')}
                  className="flex-1 items-center justify-center rounded-2xl bg-[#6f1dff] px-4 py-3">
                  <Text className="text-[14px] text-white">开户注册</Text>
                </Pressable>
              </View>
            </View>
          ) : null}
        </View>

        <View className="mt-4 flex-1 rounded-[32px] border border-dashed border-[#39435a] bg-[#171d2a]" />
      </View>
    </Pg51PageShell>
  );
}
