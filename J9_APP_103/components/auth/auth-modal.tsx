import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import { clearPendingInvite, getPendingInvite } from '@/lib/pending-invite';
import { login, register } from '@/lib/api/auth';
import { apiOk, setToken } from '@/lib/api/request';
import * as React from 'react';
import {
  ActivityIndicator,
  Image,
  KeyboardAvoidingView,
  Modal,
  Pressable,
  ScrollView,
  TextInput,
  View,
} from 'react-native';

export type AuthMode = 'login' | 'register';

type AuthModalProps = {
  visible: boolean;
  mode: AuthMode;
  onClose: () => void;
  onAuthSuccess: () => Promise<void>;
};

type LoginFormState = {
  account: string;
  password: string;
};

type RegisterFormState = {
  phone: string;
  password: string;
  confirmPassword: string;
};

type PendingInviteState = {
  inviteCode: string;
  agentId: number;
  agentName: string;
};

const defaultLoginForm: LoginFormState = {
  account: __DEV__ ? '13012341234' : '',
  password: __DEV__ ? '13012341234' : '',
};

const defaultRegisterForm: RegisterFormState = {
  phone: '',
  password: '',
  confirmPassword: '',
};

const defaultPendingInvite: PendingInviteState = {
  inviteCode: '',
  agentId: 0,
  agentName: '',
};

const brandLogo = require('@/assets/images/ios-light.png');

function isValidPhone(value: string) {
  return /^1[3-9]\d{9}$/.test(value.trim());
}

function AuthInput({
  label,
  placeholder,
  value,
  onChangeText,
  secureTextEntry,
  keyboardType,
}: {
  label: string;
  placeholder: string;
  value: string;
  onChangeText: (value: string) => void;
  secureTextEntry?: boolean;
  keyboardType?: 'default' | 'number-pad' | 'phone-pad';
}) {
  return (
    <View className="mt-2">
      <Text className="mb-1 text-[13px] font-medium text-[#d6dbeb]">{label}</Text>
      <TextInput
        value={value}
        onChangeText={onChangeText}
        placeholder={placeholder}
        placeholderTextColor="#7f879b"
        secureTextEntry={secureTextEntry}
        keyboardType={keyboardType}
        className="h-11 rounded-[16px] border border-[#2f3548] bg-[#111827] px-4 text-[15px] text-white"
      />
    </View>
  );
}

export function AuthModal({ visible, mode, onClose, onAuthSuccess }: AuthModalProps) {
  const [activeMode, setActiveMode] = React.useState<AuthMode>(mode);
  const [loginForm, setLoginForm] = React.useState<LoginFormState>(defaultLoginForm);
  const [registerForm, setRegisterForm] = React.useState<RegisterFormState>(defaultRegisterForm);
  const [pendingInvite, setPendingInvite] = React.useState<PendingInviteState>(defaultPendingInvite);
  const [inviteHydrated, setInviteHydrated] = React.useState(false);
  const [submitting, setSubmitting] = React.useState(false);
  const [errorText, setErrorText] = React.useState('');

  React.useEffect(() => {
    if (visible) {
      setActiveMode(mode);
      setErrorText('');
      setInviteHydrated(false);

      let cancelled = false;
      (async () => {
        const stored = await getPendingInvite();
        if (cancelled) return;

        setPendingInvite({
          inviteCode: stored?.invite ?? '',
          agentId: stored?.agentId ?? 0,
          agentName: stored?.agentName ?? '',
        });
        setInviteHydrated(true);
      })();

      return () => {
        cancelled = true;
      };
    }
  }, [mode, visible]);

  const handleClose = React.useCallback(() => {
    setLoginForm(defaultLoginForm);
    setRegisterForm(defaultRegisterForm);
    setPendingInvite(defaultPendingInvite);
    setInviteHydrated(false);
    setErrorText('');
    onClose();
  }, [onClose]);

  const handleLogin = React.useCallback(async () => {
    if (!loginForm.account.trim() || !loginForm.password.trim()) {
      Toast.show({ type: 'error', text1: '信息不完整', text2: '请输入手机号和登录密码。' });
      return;
    }

    if (!isValidPhone(loginForm.account)) {
      Toast.show({ type: 'error', text1: '手机号格式错误', text2: '请输入正确的 11 位手机号。' });
      return;
    }

    setSubmitting(true);
    setErrorText('');

    try {
      const result = await login(loginForm.account.trim(), loginForm.password);

      if (apiOk(result) && result.data) {
        await setToken(result.data);
        await onAuthSuccess();
        handleClose();
        return;
      }

      setErrorText(result.message || '登录失败，请检查账号和密码');
    } catch (error) {
      console.error('登录失败:', error);
      setErrorText('登录失败，请稍后再试');
    } finally {
      setSubmitting(false);
    }
  }, [handleClose, loginForm.account, loginForm.password, onAuthSuccess]);

  const handleRegister = React.useCallback(async () => {
    if (
      !registerForm.phone.trim() ||
      !registerForm.password.trim() ||
      !registerForm.confirmPassword.trim()
    ) {
      Toast.show({ type: 'error', text1: '信息不完整', text2: '请完整填写注册信息。' });
      return;
    }

    if (!inviteHydrated) {
      Toast.show({ type: 'info', text1: '请稍候', text2: '邀请信息加载中，请稍后重试。' });
      return;
    }

    if (registerForm.password !== registerForm.confirmPassword) {
      Toast.show({ type: 'error', text1: '密码不一致', text2: '两次输入的密码需保持一致。' });
      return;
    }

    if (!isValidPhone(registerForm.phone)) {
      Toast.show({ type: 'error', text1: '手机号格式错误', text2: '请输入正确的 11 位手机号。' });
      return;
    }

    if (registerForm.password.length < 4) {
      Toast.show({ type: 'error', text1: '密码长度不足', text2: '密码长度至少为 4 位。' });
      return;
    }

    setSubmitting(true);
    setErrorText('');

    try {
      const payload = {
        Username: registerForm.phone.trim(),
        Password: registerForm.password,
        BrowserFingerprint: `AppUser-${Date.now()}`,
        AgentId: pendingInvite.agentId,
        AgentName: pendingInvite.agentName || undefined,
        InviteCode: pendingInvite.inviteCode || '',
      };

      const registerResult = await register(payload);
      if (!apiOk(registerResult)) {
        setErrorText(registerResult.message || '注册失败，请稍后再试');
        return;
      }

      await clearPendingInvite();

      const loginResult = await login(payload.Username, payload.Password);
      if (apiOk(loginResult) && loginResult.data) {
        await setToken(loginResult.data);
        await onAuthSuccess();
        handleClose();
        return;
      }

      setErrorText(loginResult.message || '注册成功，请使用新账号重新登录。');
      setActiveMode('login');
    } catch (error) {
      console.error('注册失败:', error);
      setErrorText('注册失败，请稍后再试');
    } finally {
      setSubmitting(false);
    }
  }, [handleClose, inviteHydrated, onAuthSuccess, pendingInvite.agentId, pendingInvite.agentName, pendingInvite.inviteCode, registerForm]);

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      presentationStyle="overFullScreen"
      statusBarTranslucent
      onRequestClose={handleClose}>
      <KeyboardAvoidingView
        behavior="padding"
        className="flex-1">
        <Pressable
          className="flex-1 items-center justify-center bg-black/75 px-4 py-4"
          onPress={handleClose}>
          <Pressable
            onPress={(event) => event.stopPropagation()}
            className="relative w-full max-w-[420px] overflow-visible rounded-[24px] border border-[#3f4760] bg-[#1a1a1d] px-4 pb-3.5 pt-4">
            <Pressable
              onPress={handleClose}
              accessibilityRole="button"
              accessibilityLabel="关闭"
              className="absolute -right-2 -top-3 z-10 size-9 items-center justify-center rounded-full bg-[#6f1dff]">
              <Text className="text-[20px] font-bold leading-[20px] text-white">×</Text>
            </Pressable>

            <View className="flex-row items-center justify-center">
              <Image
                source={brandLogo}
                style={{ width: 36, height: 36, borderRadius: 9 }}
                resizeMode="cover"
              />
              <Text className="ml-2.5 text-[20px] font-black leading-tight text-white">
                {activeMode === 'login' ? '会员登录' : '开户注册'}
              </Text>
            </View>

            <Text className="mt-1 text-center text-[13px] leading-[18px] text-[#d6dbeb]">
              {activeMode === 'login'
                ? '请输入账号信息以继续访问会员服务'
                : '请填写基础信息，完成账户注册'}
            </Text>

            <View className="mt-2.5 flex-row rounded-full border border-[#2f3548] bg-[#111827] p-1">
              <Pressable
                onPress={() => setActiveMode('login')}
                className={`flex-1 rounded-full px-4 py-2 ${
                  activeMode === 'login' ? 'bg-[#6f1dff]' : ''
                }`}>
                <Text
                  className={`text-center text-[14px] font-bold ${
                    activeMode === 'login' ? 'text-white' : 'text-[#9fa8be]'
                  }`}>
                  登录
                </Text>
              </Pressable>

              <Pressable
                onPress={() => setActiveMode('register')}
                className={`flex-1 rounded-full px-4 py-2 ${
                  activeMode === 'register' ? 'bg-[#6f1dff]' : ''
                }`}>
                <Text
                  className={`text-center text-[14px] font-bold ${
                    activeMode === 'register' ? 'text-white' : 'text-[#9fa8be]'
                  }`}>
                  注册
                </Text>
              </Pressable>
            </View>

            <ScrollView
              className="mt-2.5 max-h-[380px]"
              contentContainerStyle={{ paddingBottom: 2 }}
              keyboardShouldPersistTaps="handled"
              showsVerticalScrollIndicator={false}>
              {activeMode === 'register' && pendingInvite.agentName ? (
                <Text className="text-center text-[12px] font-medium text-[#f0c05a]">
                  所属渠道：{pendingInvite.agentName}
                </Text>
              ) : null}
              {activeMode === 'register' && pendingInvite.inviteCode ? (
                <Text className="mt-1 text-center text-[12px] text-[#9fa8be]">
                  邀请码：{pendingInvite.inviteCode}
                </Text>
              ) : null}

              <View className="mt-2 rounded-[16px] border border-[#2f3548] bg-[#111827] px-3 py-0.5">
                {activeMode === 'login' ? (
                  <>
                    <AuthInput
                      label="手机号"
                      placeholder="请输入 11 位手机号"
                      value={loginForm.account}
                      onChangeText={(value) =>
                        setLoginForm((current) => ({ ...current, account: value }))
                      }
                      keyboardType="phone-pad"
                    />
                    <AuthInput
                      label="密码"
                      placeholder="请输入登录密码"
                      value={loginForm.password}
                      onChangeText={(value) =>
                        setLoginForm((current) => ({ ...current, password: value }))
                      }
                      secureTextEntry
                    />
                  </>
                ) : (
                  <>
                    <AuthInput
                      label="手机号"
                      placeholder="请输入手机号"
                      value={registerForm.phone}
                      onChangeText={(value) =>
                        setRegisterForm((current) => ({ ...current, phone: value }))
                      }
                      keyboardType="phone-pad"
                    />
                    <AuthInput
                      label="密码"
                      placeholder="设置登录密码"
                      value={registerForm.password}
                      onChangeText={(value) =>
                        setRegisterForm((current) => ({ ...current, password: value }))
                      }
                      secureTextEntry
                    />
                    <AuthInput
                      label="确认密码"
                      placeholder="再输一次密码"
                      value={registerForm.confirmPassword}
                      onChangeText={(value) =>
                        setRegisterForm((current) => ({
                          ...current,
                          confirmPassword: value,
                        }))
                      }
                      secureTextEntry
                    />
                  </>
                )}
              </View>

              {errorText ? (
                <View className="mt-2 rounded-[16px] border border-[#5a2f3d] bg-[#3a1e28] px-4 py-2">
                  <Text className="text-[13px] leading-[18px] text-[#ffb8c8]">{errorText}</Text>
                </View>
              ) : null}

              {activeMode === 'login' ? (
                <>
                  <Pressable
                    onPress={handleLogin}
                    disabled={submitting}
                    className={`mt-3 items-center rounded-full py-2.5 ${
                      submitting ? 'bg-[#4d2f9c]' : 'bg-[#6f1dff]'
                    }`}>
                    {submitting ? (
                      <ActivityIndicator color="#fff" />
                    ) : (
                      <Text className="text-[15px] font-black text-white">登录账户</Text>
                    )}
                  </Pressable>

                  <Pressable onPress={() => setActiveMode('register')} className="mt-2 items-center py-0.5">
                    <Text className="text-[13px] text-[#f0c05a] underline">还没有账户？立即注册</Text>
                  </Pressable>
                </>
              ) : (
                <>
                  <Pressable
                    onPress={handleRegister}
                    disabled={submitting || !inviteHydrated}
                    className={`mt-3 items-center rounded-full py-2.5 ${
                      submitting || !inviteHydrated ? 'bg-[#4d2f9c]' : 'bg-[#6f1dff]'
                    }`}>
                    {submitting || !inviteHydrated ? (
                      <ActivityIndicator color="#fff" />
                    ) : (
                      <Text className="text-[15px] font-black text-white">提交注册</Text>
                    )}
                  </Pressable>

                  <Pressable onPress={() => setActiveMode('login')} className="mt-2 items-center py-0.5">
                    <Text className="text-[13px] text-[#f0c05a] underline">已有账户？前往登录</Text>
                  </Pressable>
                </>
              )}
            </ScrollView>
          </Pressable>
        </Pressable>
      </KeyboardAvoidingView>
    </Modal>
  );
}
