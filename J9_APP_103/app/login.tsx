import { useAuthModal } from '@/components/auth/auth-modal-provider';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Stack, useRouter } from 'expo-router';
import { ArrowLeft, LogIn, UserPlus } from 'lucide-react-native';
import { useEffect } from 'react';
import { Pressable, View } from 'react-native';

export default function LoginScreen() {
  const router = useRouter();
  const { isAuthenticated, openAuthModal } = useAuthModal();

  useEffect(() => {
    if (isAuthenticated) {
      router.replace('/mine');
      return;
    }

    openAuthModal('login');
  }, [isAuthenticated, openAuthModal, router]);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <View className="flex-1 items-center justify-center bg-[#0f1420] px-5">
        <View className="w-full max-w-[420px] rounded-[32px] border border-[#313a4f] bg-[#171d2a] p-5">
          <Text className="text-[28px] font-black text-white">会员登录</Text>
          <Text className="mt-2 text-[14px] leading-[22px] text-[#9ea8c0]">
            登录窗口已为您开启，如需再次进入，可使用下方入口。
          </Text>

          <View className="mt-6 gap-3">
            <Pressable
              onPress={() => openAuthModal('login')}
              className="flex-row items-center justify-center gap-2 rounded-[20px] bg-[#b79249] px-4 py-4">
              <Icon as={LogIn} size={18} color="#ffffff" />
              <Text className="text-[15px] font-bold text-white">打开登录窗口</Text>
            </Pressable>

            <Pressable
              onPress={() => openAuthModal('register')}
              className="flex-row items-center justify-center gap-2 rounded-[20px] bg-[#6f1dff] px-4 py-4">
              <Icon as={UserPlus} size={18} color="#ffffff" />
              <Text className="text-[15px] font-bold text-white">开户注册</Text>
            </Pressable>

            <Pressable
              onPress={() => router.replace('/')}
              className="flex-row items-center justify-center gap-2 rounded-[20px] bg-[#232b3d] px-4 py-4">
              <Icon as={ArrowLeft} size={18} color="#d7def0" />
              <Text className="text-[15px] font-bold text-[#d7def0]">返回首页</Text>
            </Pressable>
          </View>
        </View>
      </View>
    </>
  );
}
