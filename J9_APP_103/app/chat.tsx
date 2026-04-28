import { Pg51PageShell, Pg51TrackedScrollView } from '@/components/pg51-clone/chrome';
import { Pg51LucideIconBadge } from '@/components/pg51-clone/original-icons';
import { Icon } from '@/components/ui/icon';
import { Text } from '@/components/ui/text';
import { Toast } from '@/components/ui/toast';
import {
  getMessages,
  markAsRead,
  MessageSenderRole,
  MessageStatus,
  sendMessage as submitSupportMessage,
  type DMessage,
} from '@/lib/api/message';
import { getToken } from '@/lib/api/request';
import { useFocusEffect } from '@react-navigation/native';
import { Stack, useRouter } from 'expo-router';
import {
  ActivityIndicator,
  Keyboard,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  TextInput,
  View,
} from 'react-native';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ChevronLeft, Headset, Megaphone, MessageCircle, Send } from 'lucide-react-native';

const QUICK_TAGS = ['充值咨询', '提现问题', '账号问题', '活动奖励'];
const MESSAGE_POLL_MS = 10_000;

function formatChatTime(dateStr: string) {
  const date = new Date(dateStr);
  if (Number.isNaN(date.getTime())) return '';
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');
  return `${hours}:${minutes}`;
}

export default function ChatScreen() {
  const router = useRouter();
  const scrollRef = useRef<ScrollView>(null);
  const [messages, setMessages] = useState<DMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [isKeyboardVisible, setIsKeyboardVisible] = useState(false);

  const scrollToBottom = useCallback((animated = true, delay = 0) => {
    const run = () => scrollRef.current?.scrollToEnd({ animated });

    if (delay > 0) {
      const timer = setTimeout(run, delay);
      return () => clearTimeout(timer);
    }

    requestAnimationFrame(run);
    return undefined;
  }, []);

  const welcomeHint = useMemo(
    () => '您好，欢迎进入在线客服通道。请提交您的问题，我们将尽快为您处理。',
    []
  );

  const loadMessages = useCallback(async (options?: { silent?: boolean }) => {
    const silent = options?.silent ?? false;
    const token = await getToken();

    if (!token) {
      setMessages([]);
      if (!silent) setLoading(false);
      return;
    }

    if (!silent) setLoading(true);

    try {
      const result = await getMessages();
      if (result.success && result.data) {
        const rows = [...result.data].sort(
          (a, b) => new Date(a.sentAt).getTime() - new Date(b.sentAt).getTime()
        );
        const unreadAgentIds = rows
          .filter(
            (item) =>
              item.senderRole === MessageSenderRole.Agent && item.status === MessageStatus.未读
          )
          .map((item) => item.id);

        if (unreadAgentIds.length > 0) {
          setMessages(
            rows.map((item) =>
              unreadAgentIds.includes(item.id) ? { ...item, status: MessageStatus.已读 } : item
            )
          );
          void Promise.all(unreadAgentIds.map((id) => markAsRead(id)));
        } else {
          setMessages(rows);
        }
      }
    } catch {
      if (!silent) {
        Toast.show({ type: 'error', text1: '提示', text2: '聊天记录加载失败，请稍后再试。' });
      }
    } finally {
      if (!silent) setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadMessages();
  }, [loadMessages]);

  useFocusEffect(
    useCallback(() => {
      const timer = setInterval(() => {
        void loadMessages({ silent: true });
      }, MESSAGE_POLL_MS);

      return () => clearInterval(timer);
    }, [loadMessages])
  );

  useEffect(() => {
    const showEvent = Platform.OS === 'ios' ? 'keyboardWillShow' : 'keyboardDidShow';
    const hideEvent = Platform.OS === 'ios' ? 'keyboardWillHide' : 'keyboardDidHide';

    const showSubscription = Keyboard.addListener(showEvent, () => {
      setIsKeyboardVisible(true);
      scrollToBottom(true, 120);
    });
    const hideSubscription = Keyboard.addListener(hideEvent, () => {
      setIsKeyboardVisible(false);
    });

    return () => {
      showSubscription.remove();
      hideSubscription.remove();
    };
  }, [scrollToBottom]);

  useEffect(() => {
    if (!messages.length) return;
    return scrollToBottom(true, 120);
  }, [messages.length, scrollToBottom]);

  const handleSend = useCallback(async () => {
    const content = input.trim();
    if (!content || sending) return;

    setSending(true);
    try {
      const result = await submitSupportMessage(content);
      if (result.success) {
        setInput('');
        await loadMessages({ silent: true });
      } else {
        Toast.show({ type: 'error', text1: '发送失败', text2: result.message || '请稍后再试。' });
      }
    } catch {
      Toast.show({
        type: 'error',
        text1: '网络异常',
        text2: '消息没发出去，请检查网络后重试。',
      });
    } finally {
      setSending(false);
    }
  }, [input, loadMessages, sending]);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <Pg51PageShell withBottomNav={false}>
        <KeyboardAvoidingView
          className="flex-1"
          behavior={Platform.OS === 'ios' ? 'padding' : 'height'}>
          <View className="flex-1 px-4 pb-5 pt-5">
            <View className="flex-row items-center justify-between gap-3">
              <Pressable
                onPress={() => (router.canGoBack() ? router.back() : router.replace('/mine'))}
                className="flex-row items-center gap-2 rounded-full border border-[#39435a] bg-[#212838] px-4 py-2.5">
                <Icon as={ChevronLeft} size={16} className="text-white" />
                <Text className="text-[12px] font-bold text-white">返回</Text>
              </Pressable>

              <View className="rounded-full bg-[#172b26] px-3 py-1.5">
                <Text className="text-[11px] font-bold text-[#4ade80]">在线处理中</Text>
              </View>
            </View>

            <View className="mt-4 flex-1 rounded-[28px] border border-[#39435a] bg-[#171d2a] px-4 py-4">
              {loading ? (
                <View className="flex-1 items-center justify-center">
                  <ActivityIndicator color="#9b5cff" />
                  <Text className="mt-3 text-[12px] text-[#9fa8be]">聊天记录加载中...</Text>
                </View>
              ) : (
                <Pg51TrackedScrollView
                  ref={scrollRef}
                  className="flex-1"
                  showsVerticalScrollIndicator={false}
                  keyboardDismissMode="on-drag"
                  keyboardShouldPersistTaps="handled"
                  contentContainerStyle={{ paddingBottom: 12 }}>
                  <View className="mb-4 items-start">
                    <View className="max-w-[92%] rounded-[20px] rounded-bl-md border border-[#32405b] bg-[#212838] px-4 py-3">
                      <View className="mb-2 flex-row items-center gap-2">
                        <Pg51LucideIconBadge icon={Headset} size={28} iconSize={13} radius={14} />
                        <Text className="text-[12px] font-bold text-white">客服</Text>
                      </View>
                      <Text className="text-[13px] leading-[20px] text-[#d7def0]">
                        {welcomeHint}
                      </Text>
                    </View>
                  </View>

                  {messages.length === 0 ? (
                    <Text className="mb-3 text-center text-[12px] text-[#8f9ab2]">
                      当前暂无会话记录，您可直接提交咨询内容。
                    </Text>
                  ) : null}

                  {messages.map((item) => (
                    <ChatBubble key={item.id} item={item} />
                  ))}
                </Pg51TrackedScrollView>
              )}
            </View>

            {!isKeyboardVisible ? (
              <View className="mt-3">
                <ScrollView
                  horizontal
                  showsHorizontalScrollIndicator={false}
                  contentContainerStyle={{ gap: 8 }}>
                  {QUICK_TAGS.map((tag) => (
                    <Pressable
                      key={tag}
                      disabled={sending}
                      onPress={() => setInput(tag)}
                      className="rounded-full border border-[#39435a] bg-[#212838] px-3 py-2"
                      style={{ opacity: sending ? 0.5 : 1 }}>
                      <Text className="text-[11px] font-semibold text-[#d7def0]">{tag}</Text>
                    </Pressable>
                  ))}
                </ScrollView>
              </View>
            ) : null}

            <View className="mt-3 flex-row items-center gap-2 rounded-[24px] border border-[#39435a] bg-[#171d2a] p-3">
              <View className="h-12 flex-1 flex-row items-center rounded-[18px] bg-[#212838] px-3">
                <Icon as={MessageCircle} size={16} color="#8f9ab2" />
                <TextInput
                  className="flex-1 pl-2 text-[14px] text-white"
                  style={{
                    paddingVertical: 0,
                    ...(Platform.OS === 'android' ? { textAlignVertical: 'center' as const } : {}),
                  }}
                  placeholder="请输入您的问题或需求..."
                  placeholderTextColor="#8f9ab2"
                  value={input}
                  onChangeText={setInput}
                  editable={!sending}
                  maxLength={2000}
                  onFocus={() => {
                    scrollToBottom(true, 120);
                  }}
                  returnKeyType="send"
                  blurOnSubmit={false}
                  onSubmitEditing={() => void handleSend()}
                />
              </View>

              <Pressable
                onPress={() => void handleSend()}
                disabled={sending || !input.trim()}
                className="size-12 items-center justify-center rounded-[18px]"
                style={{
                  backgroundColor: '#6f1dff',
                  opacity: sending || !input.trim() ? 0.45 : 1,
                }}>
                {sending ? (
                  <ActivityIndicator color="#ffffff" size="small" />
                ) : (
                  <Icon as={Send} size={18} color="#ffffff" />
                )}
              </Pressable>
            </View>
          </View>
        </KeyboardAvoidingView>
      </Pg51PageShell>
    </>
  );
}

function ChatBubble({ item }: { item: DMessage }) {
  const isCustomer = item.senderRole === MessageSenderRole.Customer;
  const isSystem = item.senderRole === MessageSenderRole.System;

  if (isSystem) {
    return (
      <View className="mb-3 items-center px-2">
        <View className="flex-row items-center gap-1.5 rounded-full bg-[#241d39] px-3 py-1.5">
          <Icon as={Megaphone} size={12} color="#9b5cff" />
          <Text className="text-[10px] font-semibold text-[#d7c8ff]">系统消息</Text>
        </View>
        <Text className="mt-1.5 text-center text-[12px] leading-[18px] text-[#9fa8be]">
          {item.content}
        </Text>
        <Text className="mt-1 text-[10px] text-[#71809e]">{formatChatTime(item.sentAt)}</Text>
      </View>
    );
  }

  return (
    <View className={`mb-3 ${isCustomer ? 'items-end' : 'items-start'}`}>
      <View
        className={`max-w-[88%] rounded-[20px] px-3.5 py-3 ${
          isCustomer ? 'rounded-br-md bg-[#6f1dff]' : 'rounded-bl-md bg-[#212838]'
        }`}
        style={{
          borderWidth: 1,
          borderColor: isCustomer ? '#8250ff' : '#39435a',
        }}>
        {!isCustomer ? (
          <View className="mb-1.5 flex-row items-center gap-1.5">
            <Icon as={Headset} size={12} color="#4ade80" />
            <Text className="text-[10px] font-bold text-[#9fe7bc]">客服</Text>
          </View>
        ) : null}

        <Text
          className="text-[13px] leading-[20px]"
          style={{ color: isCustomer ? '#ffffff' : '#d7def0' }}>
          {item.content}
        </Text>
      </View>

      <Text className="mt-1 px-1 text-[10px] text-[#71809e]">{formatChatTime(item.sentAt)}</Text>
    </View>
  );
}
