import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import { clearPendingInvite, getPendingInvite } from '@/lib/pending-invite';
import { login, register } from '@/lib/api/auth';
import { apiOk, setToken } from '@/lib/api/request';
import * as React from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
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
    <View className="mt-3 px-[2px]">
      <Text className="mb-2 text-[13px] font-medium text-[#7b849d]">{label}</Text>
      <TextInput
        value={value}
        onChangeText={onChangeText}
        placeholder={placeholder}
        placeholderTextColor="#7f879b"
        secureTextEntry={secureTextEntry}
        keyboardType={keyboardType}
        className="h-12 rounded-2xl border border-[#313a4f] bg-[#171d2a] px-4 text-[15px] text-white"
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
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={styles.keyboardContainer}>
        <View pointerEvents="box-none" style={styles.overlay}>
          <Pressable style={styles.backdrop} onPress={handleClose} />

          <View style={styles.dialogShell}>
            <View className="rounded-[32px] border border-[#3f4760] bg-[#202737] p-4">
              <View className="mb-2 flex-row justify-end">
                <Pressable
                  onPress={handleClose}
                  className="size-8 items-center justify-center rounded-full bg-[#31384a]">
                  <Text className="text-[18px] font-bold leading-[18px] text-[#d2d7e4]">×</Text>
                </Pressable>
              </View>

              <View className="rounded-[28px] border border-[#343d52] bg-[#111827] p-4">
                <View className="flex-row rounded-full bg-[#232b3d] p-1">
                  <Pressable
                    onPress={() => setActiveMode('login')}
                    className={`flex-1 rounded-full px-4 py-3 ${
                      activeMode === 'login' ? 'bg-[#b79249]' : ''
                    }`}>
                    <Text
                      className={`text-center text-[15px] ${
                        activeMode === 'login' ? 'text-white' : 'text-[#97a0b7]'
                      }`}>
                      登录
                    </Text>
                  </Pressable>

                  <Pressable
                    onPress={() => setActiveMode('register')}
                    className={`flex-1 rounded-full px-4 py-3 ${
                      activeMode === 'register' ? 'bg-[#6f1dff]' : ''
                    }`}>
                    <Text
                      className={`text-center text-[15px] ${
                        activeMode === 'register' ? 'text-white' : 'text-[#97a0b7]'
                      }`}>
                      注册
                    </Text>
                  </Pressable>
                </View>

                <ScrollView
                  className="mt-4 max-h-[460px]"
                  contentContainerStyle={{ paddingBottom: 6 }}
                  keyboardShouldPersistTaps="handled"
                  showsVerticalScrollIndicator={false}>
                  <Text className="text-[24px] font-black text-white">
                    {activeMode === 'login' ? '会员登录' : '开户注册'}
                  </Text>
                  <Text className="mt-2 text-[13px] leading-[20px] text-[#9ba5bc]">
                    {activeMode === 'login'
                      ? '请输入账号信息以继续访问会员服务。'
                      : '请填写基础信息，完成账户注册。'}
                  </Text>
                  {activeMode === 'register' && pendingInvite.agentName ? (
                    <Text className="mt-2 text-[12px] font-medium text-[#c9a35b]">
                      所属渠道：{pendingInvite.agentName}
                    </Text>
                  ) : null}
                  {activeMode === 'register' && pendingInvite.inviteCode ? (
                    <Text className="mt-1 text-[12px] text-[#8f9ab2]">
                      邀请码：{pendingInvite.inviteCode}
                    </Text>
                  ) : null}

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

                      <Pressable
                        onPress={handleLogin}
                        disabled={submitting}
                        className={`mt-5 h-12 items-center justify-center rounded-2xl ${
                          submitting ? 'bg-[#7d6a3e]' : 'bg-[#b79249]'
                        }`}>
                        {submitting ? (
                          <ActivityIndicator color="#fff" />
                        ) : (
                          <Text className="text-[15px] text-white">登录账户</Text>
                        )}
                      </Pressable>

                      <Pressable onPress={() => setActiveMode('register')} className="mt-4">
                        <Text className="text-center text-[13px] font-medium text-[#9b5cff]">
                          还没有账户？立即注册
                        </Text>
                      </Pressable>
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

                      <Pressable
                        onPress={handleRegister}
                        disabled={submitting || !inviteHydrated}
                        className={`mt-5 h-12 items-center justify-center rounded-2xl ${
                          submitting || !inviteHydrated ? 'bg-[#4d2f9c]' : 'bg-[#6f1dff]'
                        }`}>
                        {submitting || !inviteHydrated ? (
                          <ActivityIndicator color="#fff" />
                        ) : (
                          <Text className="text-[15px] text-white">提交注册</Text>
                        )}
                      </Pressable>

                      <Pressable onPress={() => setActiveMode('login')} className="mt-4">
                        <Text className="text-center text-[13px] font-medium text-[#b79249]">
                          已有账户？前往登录
                        </Text>
                      </Pressable>
                    </>
                  )}

                  {errorText ? (
                    <View className="mt-4 rounded-2xl border border-[#5a2f3d] bg-[#3a1e28] px-4 py-3">
                      <Text className="text-[13px] leading-[20px] text-[#ffb8c8]">
                        {errorText}
                      </Text>
                    </View>
                  ) : null}
                </ScrollView>
              </View>
            </View>
          </View>
        </View>
      </KeyboardAvoidingView>
    </Modal>
  );
}

const styles = StyleSheet.create({
  keyboardContainer: {
    flex: 1,
  },
  overlay: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: 16,
  },
  backdrop: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: 'rgba(0, 0, 0, 0.7)',
  },
  dialogShell: {
    width: '100%',
    maxWidth: 420,
    zIndex: 1,
    elevation: 1,
  },
});
