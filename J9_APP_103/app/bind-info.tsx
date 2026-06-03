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
  Building2,
  Camera,
  CreditCard,
  IdCard,
  Link,
  Mail,
  MessageCircle,
  Phone,
  User,
  UserCheck,
  Wallet,
} from 'lucide-react-native';
import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';

type FormState = {
  telegram: string;
  usdtAddress: string;
  username: string;
  nickname: string;
  realName: string;
  email: string;
  bankName: string;
  bankAccount: string;
  alipayAccount: string;
  avatar: string;
};

function isValidEmail(value: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}

function isValidPhone(value: string) {
  return /^1[3-9]\d{9}$/.test(value);
}

const emptyForm: FormState = {
  telegram: '',
  usdtAddress: '',
  username: '',
  nickname: '',
  realName: '',
  email: '',
  bankName: '',
  bankAccount: '',
  alipayAccount: '',
  avatar: '',
};

export default function BindInfoScreen() {
  const router = useRouter();
  const [form, setForm] = useState<FormState>(emptyForm);
  const [initialForm, setInitialForm] = useState<FormState>(emptyForm);
  const [initializing, setInitializing] = useState(true);
  const [loading, setLoading] = useState(false);
  const [uploadingAvatar, setUploadingAvatar] = useState(false);

  const leaveSettings = () => {
    if (router.canGoBack()) {
      router.back();
    } else {
      router.replace('/mine');
    }
  };

  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const result = await getMemberInfo();
        if (!result.success || !result.data || cancelled) return;

        const nextForm: FormState = {
          telegram: String(result.data.Telegram ?? result.data.telegram ?? ''),
          usdtAddress: String(result.data.USDTAddress ?? result.data.usdtAddress ?? ''),
          username: String(result.data.Username ?? result.data.username ?? ''),
          nickname: String(result.data.Nickname ?? result.data.nickname ?? ''),
          realName: String(result.data.RealName ?? result.data.realName ?? ''),
          email: String(result.data.Email ?? result.data.email ?? ''),
          bankName: String(result.data.BankName ?? result.data.bankName ?? ''),
          bankAccount: String(result.data.BankAccount ?? result.data.bankAccount ?? ''),
          alipayAccount: String(result.data.AlipayAccount ?? result.data.alipayAccount ?? ''),
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
    form.username !== initialForm.username ||
    form.nickname !== initialForm.nickname ||
    form.realName !== initialForm.realName ||
    form.email !== initialForm.email ||
    form.bankName !== initialForm.bankName ||
    form.bankAccount !== initialForm.bankAccount ||
    form.alipayAccount !== initialForm.alipayAccount;

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

    const username = form.username.trim();
    if (!username) {
      Toast.show({ type: 'error', text1: '请填写登录账号（手机号）' });
      return;
    }
    if (!isValidPhone(username)) {
      Toast.show({ type: 'error', text1: '手机号格式错误', text2: '请输入正确的 11 位手机号。' });
      return;
    }

    const realName = form.realName.trim();
    if (!realName) {
      Toast.show({ type: 'error', text1: '请填写真实姓名' });
      return;
    }

    const email = form.email.trim();
    if (!email) {
      Toast.show({ type: 'error', text1: '请填写邮箱地址' });
      return;
    }
    if (!isValidEmail(email)) {
      Toast.show({ type: 'error', text1: '邮箱格式不正确', text2: '请输入有效的邮箱地址。' });
      return;
    }

    setLoading(true);

    try {
      const result = await updateMemberInfo(
        form.telegram.trim(),
        form.usdtAddress.trim(),
        username,
        form.nickname.trim(),
        realName,
        email,
        form.bankName.trim(),
        form.bankAccount.trim(),
        form.alipayAccount.trim(),
        // 提现密码不再在此页维护，传空字符串，后端将保留原值。
        ''
      );

      if (result.success) {
        setInitialForm(form);
        Toast.show({ type: 'success', text1: '资料已经更新好了' });
        leaveSettings();
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
        title="账号信息"
        subtitle="手机号、实名、TG 与 USDT 提现地址"
        tag="资料维护"
        tone="purple"
        hideHero>
        <Pg51InnerPageTopBar
          onBack={leaveSettings}
          icon={UserCheck}
          iconColor="#9b5cff"
          title="账号信息"
          subtitle="手机号、实名、TG 与 USDT 提现地址"
          tone="purple"
        />

        <Pg51SectionCard>
          <View className="items-center gap-3">
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
              disabled={uploadingAvatar}
              className="flex-row items-center gap-2 rounded-full bg-[#212838] px-4 py-2">
              <Icon as={Camera} size={14} color="#9b5cff" />
              <Text className="text-[12px] font-bold text-white">
                {form.avatar ? '更换头像' : '上传头像'}
              </Text>
            </Pressable>
          </View>

          <FormField label="昵称" icon={User}>
            <Input
              placeholder="请输入昵称"
              placeholderTextColor="#7f879b"
              value={form.nickname}
              onChangeText={(value) => updateField('nickname', value)}
              className="border-[#39435a] bg-[#212838] pl-10 text-white"
            />
          </FormField>

          <FormField label="登录账号（手机号）" icon={Phone} required>
            <Input
              placeholder="请输入 11 位手机号（必填）"
              placeholderTextColor="#7f879b"
              value={form.username}
              onChangeText={(value) => updateField('username', value)}
              className="border-[#39435a] bg-[#212838] pl-10 text-white"
              keyboardType="phone-pad"
            />
          </FormField>

          <FormField label="邮箱地址" icon={Mail} required>
            <Input
              placeholder="请输入邮箱地址（必填）"
              placeholderTextColor="#7f879b"
              value={form.email}
              onChangeText={(value) => updateField('email', value)}
              className="border-[#39435a] bg-[#212838] pl-10 text-white"
              autoCapitalize="none"
              keyboardType="email-address"
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
        </Pg51SectionCard>

        <Pg51SectionCard
          title="实名信息"
          description="填写真实姓名、提现与收款方式，用于实名认证与提现到账">
          <FormField label="真实姓名" icon={IdCard} required>
            <Input
              placeholder="请输入真实姓名（必填）"
              placeholderTextColor="#7f879b"
              value={form.realName}
              onChangeText={(value) => updateField('realName', value)}
              className="border-[#39435a] bg-[#212838] pl-10 text-white"
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

          <FormField label="银行名称" icon={Building2}>
            <Input
              placeholder="请输入银行名称"
              placeholderTextColor="#7f879b"
              value={form.bankName}
              onChangeText={(value) => updateField('bankName', value)}
              className="border-[#39435a] bg-[#212838] pl-10 text-white"
            />
          </FormField>

          <FormField label="银行账号" icon={CreditCard}>
            <Input
              placeholder="请输入银行账号"
              placeholderTextColor="#7f879b"
              value={form.bankAccount}
              onChangeText={(value) => updateField('bankAccount', value)}
              className="border-[#39435a] bg-[#212838] pl-10 text-white"
              keyboardType="number-pad"
            />
          </FormField>

          <FormField label="支付宝账号" icon={Wallet}>
            <Input
              placeholder="请输入支付宝账号"
              placeholderTextColor="#7f879b"
              value={form.alipayAccount}
              onChangeText={(value) => updateField('alipayAccount', value)}
              className="border-[#39435a] bg-[#212838] pl-10 text-white"
              autoCapitalize="none"
            />
          </FormField>
        </Pg51SectionCard>

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
      </Pg51InnerPage>
    </>
  );
}

function FormField({
  label,
  icon,
  required = false,
  children,
}: {
  label: string;
  icon: any;
  required?: boolean;
  children: ReactNode;
}) {
  return (
    <View className="gap-2">
      <Text className="text-[13px] font-bold text-white">
        {label}
        {required ? <Text className="text-[#ff7e93]"> *</Text> : null}
      </Text>
      <View className="relative">
        <View className="absolute bottom-0 left-3 top-0 z-10 justify-center">
          <Icon as={icon} size={18} color="#9b5cff" />
        </View>
        {children}
      </View>
    </View>
  );
}
