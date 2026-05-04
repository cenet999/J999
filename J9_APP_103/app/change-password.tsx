import { useAuthModal } from '@/components/auth/auth-modal-provider';
import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Pg51InnerPageTopBar } from '@/components/pg51-clone/inner-page-top-bar';
import { Pg51InnerPage, Pg51SectionCard } from '@/components/pg51-clone/page-ui';
import { Icon } from '@/components/ui/icon';
import { Input } from '@/components/ui/input';
import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import { changePassword, changeWithdrawPassword } from '@/lib/api/auth';
import { clearToken } from '@/lib/api/request';
import { Stack, useRouter } from 'expo-router';
import { ActivityIndicator, TouchableOpacity, View } from 'react-native';
import { Check, Eye, EyeOff, Lock, ShieldCheck, Wallet } from 'lucide-react-native';
import { useState } from 'react';

export default function ChangePasswordScreen() {
  const router = useRouter();
  const { openAuthModal, refreshAuthState } = useAuthModal();

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51InnerPage
        title="修改密码"
        subtitle="修改登录与提现密码"
        tag="账户安全"
        tone="purple"
        hideHero>
        <Pg51InnerPageTopBar
          onBack={() => router.back()}
          icon={ShieldCheck}
          iconColor="#ff7e93"
          title="修改密码"
          subtitle="修改登录与提现密码"
          tone="red"
        />

        <View className="items-center gap-3 rounded-[28px] border border-[#39435a] bg-[#171d2a] p-5">
          <Pg51LucideIconBadge icon={ShieldCheck} size={72} iconSize={34} radius={36} />
          <Text className="text-[14px] font-semibold text-[#b7c0d6]">
            修改敏感信息需验证当前登录密码。
          </Text>
        </View>

        <LoginPasswordSection
          onChanged={async () => {
            await clearToken();
            await refreshAuthState();
            router.replace('/');
            openAuthModal('login');
          }}
        />

        <WithdrawPasswordSection />
      </Pg51InnerPage>
    </>
  );
}

function LoginPasswordSection({ onChanged }: { onChanged: () => void | Promise<void> }) {
  const [oldPassword, setOldPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showOld, setShowOld] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [loading, setLoading] = useState(false);

  const hasMinLength = newPassword.length >= 8;
  const hasLetter = /[a-zA-Z]/.test(newPassword);
  const hasNumber = /\d/.test(newPassword);
  const passwordsMatch = newPassword.length > 0 && newPassword === confirmPassword;
  const canSubmit =
    Boolean(oldPassword.trim()) && hasMinLength && hasLetter && hasNumber && passwordsMatch;

  const handleSubmit = async () => {
    if (!canSubmit) return;

    setLoading(true);
    try {
      const result = await changePassword(oldPassword, newPassword);
      if (result.success) {
        Toast.show({
          type: 'success',
          text1: '修改成功',
          text2: '登录密码已更新，请重新登录账户。',
        });
        void onChanged();
      } else {
        Toast.show({ type: 'error', text1: result.message || '修改失败，请稍后再试' });
      }
    } catch {
      Toast.show({ type: 'error', text1: '网络异常，请稍后重试' });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Pg51SectionCard title="修改登录密码" description="≥8 位，含字母与数字">
      <PasswordInput
        label="原登录密码"
        value={oldPassword}
        onChangeText={setOldPassword}
        visible={showOld}
        onToggle={() => setShowOld((value) => !value)}
        iconColor="#8f9ab2"
      />
      <PasswordInput
        label="新登录密码"
        value={newPassword}
        onChangeText={setNewPassword}
        visible={showNew}
        onToggle={() => setShowNew((value) => !value)}
        iconColor="#ff7e93"
      />

      {newPassword ? (
        <View className="gap-2 rounded-[20px] bg-[#212838] px-4 py-3">
          <PasswordRule passed={hasMinLength} label="至少 8 位字符" />
          <PasswordRule passed={hasLetter} label="包含字母" />
          <PasswordRule passed={hasNumber} label="包含数字" />
        </View>
      ) : null}

      <PasswordInput
        label="确认新登录密码"
        value={confirmPassword}
        onChangeText={setConfirmPassword}
        visible={showConfirm}
        onToggle={() => setShowConfirm((value) => !value)}
        iconColor="#ff7e93"
      />

      {confirmPassword ? (
        <Text
          className="text-[12px] font-semibold"
          style={{ color: passwordsMatch ? '#4ade80' : '#ff7e93' }}>
          {passwordsMatch ? '两次输入一致' : '两次输入的密码不一致'}
        </Text>
      ) : null}

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
          <Text className="text-[15px] font-black text-white">确认修改登录密码</Text>
        )}
      </TouchableOpacity>
    </Pg51SectionCard>
  );
}

function WithdrawPasswordSection() {
  const [loginPassword, setLoginPassword] = useState('');
  const [newWithdraw, setNewWithdraw] = useState('');
  const [confirmWithdraw, setConfirmWithdraw] = useState('');
  const [showLogin, setShowLogin] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [loading, setLoading] = useState(false);

  const hasMinLength = newWithdraw.length >= 6;
  const passwordsMatch = newWithdraw.length > 0 && newWithdraw === confirmWithdraw;
  const canSubmit = Boolean(loginPassword.trim()) && hasMinLength && passwordsMatch;

  const handleSubmit = async () => {
    if (!canSubmit) return;

    setLoading(true);
    try {
      const result = await changeWithdrawPassword(loginPassword, newWithdraw);
      if (result.success) {
        Toast.show({ type: 'success', text1: result.message || '提现密码已更新' });
        setLoginPassword('');
        setNewWithdraw('');
        setConfirmWithdraw('');
      } else {
        Toast.show({ type: 'error', text1: result.message || '修改失败，请稍后再试' });
      }
    } catch {
      Toast.show({ type: 'error', text1: '网络异常，请稍后重试' });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Pg51SectionCard
      title="修改提现密码"
      description="≥6 位，修改需验证登录密码">
      <View className="flex-row items-center gap-3 rounded-[20px] bg-[#2d2618] px-4 py-3">
        <Pg51LucideIconBadge icon={Wallet} />
        <Text className="flex-1 text-[11px] leading-[18px] text-[#d3c299]">
          用于出账校验，建议勿与登录密码相同。
        </Text>
      </View>

      <PasswordInput
        label="当前登录密码"
        value={loginPassword}
        onChangeText={setLoginPassword}
        visible={showLogin}
        onToggle={() => setShowLogin((value) => !value)}
        iconColor="#8f9ab2"
      />

      <PasswordInput
        label="新提现密码"
        value={newWithdraw}
        onChangeText={setNewWithdraw}
        visible={showNew}
        onToggle={() => setShowNew((value) => !value)}
        iconColor="#ffcc66"
      />

      {newWithdraw ? (
        <View className="gap-2 rounded-[20px] bg-[#212838] px-4 py-3">
          <PasswordRule passed={hasMinLength} label="至少 6 位字符" />
        </View>
      ) : null}

      <PasswordInput
        label="确认新提现密码"
        value={confirmWithdraw}
        onChangeText={setConfirmWithdraw}
        visible={showConfirm}
        onToggle={() => setShowConfirm((value) => !value)}
        iconColor="#ffcc66"
      />

      {confirmWithdraw ? (
        <Text
          className="text-[12px] font-semibold"
          style={{ color: passwordsMatch ? '#4ade80' : '#ff7e93' }}>
          {passwordsMatch ? '两次输入一致' : '两次输入的密码不一致'}
        </Text>
      ) : null}

      <TouchableOpacity
        onPress={handleSubmit}
        disabled={!canSubmit || loading}
        className="items-center justify-center rounded-[22px] px-4 py-4"
        style={{
          backgroundColor: canSubmit ? '#b79249' : '#3a4256',
          opacity: loading ? 0.75 : 1,
        }}>
        {loading ? (
          <ActivityIndicator color="#fff" />
        ) : (
          <Text className="text-[15px] font-black text-white">确认修改提现密码</Text>
        )}
      </TouchableOpacity>
    </Pg51SectionCard>
  );
}

function PasswordInput({
  label,
  value,
  onChangeText,
  visible,
  onToggle,
  iconColor,
}: {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  visible: boolean;
  onToggle: () => void;
  iconColor: string;
}) {
  return (
    <View className="gap-2">
      <Text className="text-[13px] font-bold text-white">{label}</Text>
      <View className="relative">
        <View className="absolute bottom-0 left-3 top-0 z-10 justify-center">
          <Icon as={Lock} size={18} color={iconColor} />
        </View>
        <Input
          placeholder={`请输入${label}`}
          placeholderTextColor="#7f879b"
          value={value}
          onChangeText={onChangeText}
          className="border-[#39435a] bg-[#212838] pl-10 pr-12 text-white"
          secureTextEntry={!visible}
          autoCapitalize="none"
        />
        <TouchableOpacity
          onPress={onToggle}
          style={{
            position: 'absolute',
            right: 12,
            top: 0,
            bottom: 0,
            justifyContent: 'center',
            zIndex: 10,
          }}>
          <Icon as={visible ? EyeOff : Eye} size={18} color="#8f9ab2" />
        </TouchableOpacity>
      </View>
    </View>
  );
}

function PasswordRule({ passed, label }: { passed: boolean; label: string }) {
  return (
    <View className="flex-row items-center gap-2">
      <View
        className="items-center justify-center rounded-full"
        style={{ width: 16, height: 16, backgroundColor: passed ? '#4ade80' : '#5a6378' }}>
        <Icon as={Check} size={10} color={passed ? '#111827' : '#dbe3f4'} />
      </View>
      <Text className="text-[11px] font-semibold" style={{ color: passed ? '#4ade80' : '#b7c0d6' }}>
        {label}
      </Text>
    </View>
  );
}
