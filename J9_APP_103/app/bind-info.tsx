import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Pg51InnerPageTopBar } from '@/components/pg51-clone/inner-page-top-bar';
import { Pg51InnerPage, Pg51SectionCard } from '@/components/pg51-clone/page-ui';
import { Icon } from '@/components/ui/icon';
import { Input } from '@/components/ui/input';
import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import { getMemberInfo, updateMemberInfo, uploadAvatar } from '@/lib/api/auth';
import { toAbsoluteUrl } from '@/lib/api/request';
import * as ImagePicker from 'expo-image-picker';
import { Stack, useRouter } from 'expo-router';
import { ActivityIndicator, Image, Pressable, TouchableOpacity, View } from 'react-native';
import {
  Camera,
  Link,
  MessageCircle,
  Phone,
  ShieldCheck,
  UserCheck,
} from 'lucide-react-native';
import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';

type FormState = {
  telegram: string;
  usdtAddress: string;
  phoneNumber: string;
  avatar: string;
};

const emptyForm: FormState = {
  telegram: '',
  usdtAddress: '',
  phoneNumber: '',
  avatar: '',
};

export default function BindInfoScreen() {
  const router = useRouter();
  const [form, setForm] = useState<FormState>(emptyForm);
  const [initialForm, setInitialForm] = useState<FormState>(emptyForm);
  const [initializing, setInitializing] = useState(true);
  const [loading, setLoading] = useState(false);
  const [uploadingAvatar, setUploadingAvatar] = useState(false);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const result = await getMemberInfo();
        if (!result.success || !result.data || cancelled) return;

        const nextForm: FormState = {
          telegram: String(result.data.Telegram ?? result.data.telegram ?? ''),
          usdtAddress: String(result.data.USDTAddress ?? result.data.usdtAddress ?? ''),
          phoneNumber: String(result.data.PhoneNumber ?? result.data.phoneNumber ?? ''),
          avatar: String(result.data.Avatar ?? result.data.avatar ?? ''),
        };

        setForm(nextForm);
        setInitialForm(nextForm);
      } finally {
        if (!cancelled) setInitializing(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const canSubmit =
    form.telegram !== initialForm.telegram ||
    form.usdtAddress !== initialForm.usdtAddress ||
    form.phoneNumber !== initialForm.phoneNumber;

  const updateField = (key: keyof FormState, value: string) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const handlePickAvatar = async () => {
    const { status } = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (status !== 'granted') {
      Toast.show({ type: 'error', text1: '需要相册权限才能上传头像' });
      return;
    }

    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ImagePicker.MediaTypeOptions.Images,
      allowsEditing: true,
      aspect: [1, 1],
      quality: 0.8,
      base64: true,
    });

    if (result.canceled || !result.assets?.[0]?.base64) return;

    const asset = result.assets[0];
    const mimeType = asset.mimeType || 'image/jpeg';
    const base64 = `data:${mimeType};base64,${asset.base64}`;

    setUploadingAvatar(true);
    try {
      const response = await uploadAvatar(base64);
      const avatarPath = response.data?.avatar || response.data?.Avatar || '';
      if (response.success && avatarPath) {
        setForm((prev) => ({ ...prev, avatar: avatarPath }));
        Toast.show({ type: 'success', text1: '头像已更新' });
      } else {
        Toast.show({ type: 'error', text1: response.message || '头像上传失败' });
      }
    } catch {
      Toast.show({ type: 'error', text1: '头像上传失败，请稍后再试' });
    } finally {
      setUploadingAvatar(false);
    }
  };

  const handleSubmit = async () => {
    if (!canSubmit) return;
    setLoading(true);

    try {
      const result = await updateMemberInfo(
        form.telegram.trim(),
        form.usdtAddress.trim(),
        form.phoneNumber.trim(),
        // 提现密码不再在此页维护，传空字符串，后端将保留原值。
        ''
      );

      if (result.success) {
        setInitialForm(form);
        Toast.show({ type: 'success', text1: '资料已经更新好了' });
        router.back();
      } else {
        Toast.show({ type: 'error', text1: result.message || '更新失败，请稍后再试' });
      }
    } catch {
      Toast.show({ type: 'error', text1: '网络异常，请稍后再试' });
    } finally {
      setLoading(false);
    }
  };

  if (initializing) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center' }}>
        <ActivityIndicator size="large" color="#7B5CFF" />
      </View>
    );
  }

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51InnerPage
        title="系统设置"
        subtitle="统一维护手机号、Telegram、提现地址与安全信息。"
        tag="资料维护"
        tone="purple"
        hideHero>
        <Pg51InnerPageTopBar
          onBack={() => router.back()}
          icon={UserCheck}
          iconColor="#9b5cff"
          title="系统设置"
          subtitle="统一维护手机号、Telegram、提现地址与安全信息。"
          tone="purple"
        />

        <View className="items-center rounded-[28px] border border-[#39435a] bg-[#171d2a] p-5">
          <Pressable
            onPress={handlePickAvatar}
            disabled={uploadingAvatar}
            className="items-center justify-center overflow-hidden rounded-full"
            style={{
              width: 84,
              height: 84,
              backgroundColor: form.avatar ? 'transparent' : '#241d39',
              borderWidth: 2,
              borderColor: '#4f3a80',
            }}>
            {uploadingAvatar ? (
              <ActivityIndicator size="large" color="#7B5CFF" />
            ) : form.avatar ? (
              <Image
                source={{ uri: toAbsoluteUrl(form.avatar) }}
                style={{ width: 80, height: 80 }}
                resizeMode="cover"
              />
            ) : (
              <Icon as={UserCheck} size={36} color="#9b5cff" />
            )}
          </Pressable>

          <Pressable
            onPress={handlePickAvatar}
            className="mt-3 flex-row items-center gap-2 rounded-full bg-[#212838] px-4 py-2">
            <Icon as={Camera} size={14} color="#9b5cff" />
            <Text className="text-[12px] font-bold text-white">
              {form.avatar ? '更换头像' : '上传头像'}
            </Text>
          </Pressable>

          <View className="mt-4 flex-row items-center gap-3 rounded-[20px] bg-[#2d2618] px-4 py-3">
            <Pg51LucideIconBadge icon={ShieldCheck} />
            <Text className="flex-1 text-[11px] leading-[18px] text-[#d3c299]">
              以上资料将用于安全验证、联系通知与提现服务，请准确填写。
            </Text>
          </View>
        </View>

        <Pg51SectionCard title="资料设置" description="信息更新后，请提交保存。">
          <FormField label="手机号" icon={Phone}>
            <Input
              placeholder="请输入手机号"
              placeholderTextColor="#7f879b"
              value={form.phoneNumber}
              onChangeText={(value) => updateField('phoneNumber', value)}
              className="border-[#39435a] bg-[#212838] pl-10 text-white"
              keyboardType="phone-pad"
            />
          </FormField>

          <FormField label="USDT (TRC-20) 提现地址" icon={Link}>
            <Input
              placeholder="请输入 TRC-20 收款地址"
              placeholderTextColor="#7f879b"
              value={form.usdtAddress}
              onChangeText={(value) => updateField('usdtAddress', value)}
              className="border-[#39435a] bg-[#212838] pl-10 text-white"
              autoCapitalize="none"
            />
          </FormField>

          <FormField label="Telegram" icon={MessageCircle}>
            <Input
              placeholder="请输入 Telegram 账号"
              placeholderTextColor="#7f879b"
              value={form.telegram}
              onChangeText={(value) => updateField('telegram', value)}
              className="border-[#39435a] bg-[#212838] pl-10 text-white"
              autoCapitalize="none"
            />
          </FormField>

          <Pressable
            onPress={() => router.push('/change-password')}
            className="flex-row items-center justify-between rounded-[20px] bg-[#212838] px-4 py-3.5">
            <View className="flex-1">
              <Text className="text-[13px] font-bold text-white">修改提现密码</Text>
              <Text className="mt-1 text-[11px] text-[#97a1b8]">
                已迁移到「修改密码」页面，需先校验登录密码。
              </Text>
            </View>
            <Text className="text-[18px] text-[#9b5cff]">›</Text>
          </Pressable>

          <TouchableOpacity
            onPress={handleSubmit}
            disabled={!canSubmit || loading}
            className="items-center justify-center rounded-[22px] px-4 py-4"
            style={{
              backgroundColor: canSubmit ? '#6f1dff' : '#3a4256',
              opacity: loading ? 0.75 : 1,
            }}>
            {loading ? (
              <ActivityIndicator color="#fff" />
            ) : (
              <Text className="text-[15px] font-black text-white">更新信息</Text>
            )}
          </TouchableOpacity>
        </Pg51SectionCard>
      </Pg51InnerPage>
    </>
  );
}

function FormField({ label, icon, children }: { label: string; icon: any; children: ReactNode }) {
  return (
    <View className="gap-2">
      <Text className="text-[13px] font-bold text-white">{label}</Text>
      <View className="relative">
        <View className="absolute bottom-0 left-3 top-0 z-10 justify-center">
          <Icon as={icon} size={18} color="#9b5cff" />
        </View>
        {children}
      </View>
    </View>
  );
}
