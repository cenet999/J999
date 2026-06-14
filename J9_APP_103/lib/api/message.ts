import { stripHtml } from '@/lib/utils';
import { api, apiOk, type ApiResult } from './request';

/** 与后端 DMessage.MessageSenderRole 数值一致 */
export enum MessageSenderRole {
  Customer = 0,
  Agent = 1,
  System = 2,
}

/** 与后端 DMessage.MessageStatus 数值一致 */
export enum MessageStatus {
  未读 = 0,
  已读 = 1,
  已回复 = 2,
  已撤回 = 3,
}

export interface DMessage {
  id: number;
  dMemberId: number;
  senderRole: MessageSenderRole;
  content: string;
  sentAt: string;
  status: MessageStatus;
  senderIp: string;
}

const SENDER_ROLE_MAP: Record<string, MessageSenderRole> = {
  Customer: MessageSenderRole.Customer,
  Agent: MessageSenderRole.Agent,
  System: MessageSenderRole.System,
  '0': MessageSenderRole.Customer,
  '1': MessageSenderRole.Agent,
  '2': MessageSenderRole.System,
};

const MESSAGE_STATUS_MAP: Record<string, MessageStatus> = {
  未读: MessageStatus.未读,
  已读: MessageStatus.已读,
  已回复: MessageStatus.已回复,
  已撤回: MessageStatus.已撤回,
  '0': MessageStatus.未读,
  '1': MessageStatus.已读,
  '2': MessageStatus.已回复,
  '3': MessageStatus.已撤回,
};

export function parseSenderRole(value: unknown): MessageSenderRole {
  if (typeof value === 'number' && value in MessageSenderRole) {
    return value as MessageSenderRole;
  }
  const key = String(value ?? '');
  return SENDER_ROLE_MAP[key] ?? MessageSenderRole.System;
}

export function parseMessageStatus(value: unknown): MessageStatus {
  if (typeof value === 'number' && value in MessageStatus) {
    return value as MessageStatus;
  }
  const key = String(value ?? '');
  return MESSAGE_STATUS_MAP[key] ?? MessageStatus.已读;
}

function normalizeMessage(raw: Record<string, unknown>): DMessage {
  return {
    id: Number(raw.id ?? raw.Id ?? 0),
    dMemberId: Number(raw.dMemberId ?? raw.DMemberId ?? 0),
    senderRole: parseSenderRole(raw.senderRole ?? raw.SenderRole),
    content: String(raw.content ?? raw.Content ?? ''),
    sentAt: String(raw.sentAt ?? raw.SentAt ?? ''),
    status: parseMessageStatus(raw.status ?? raw.Status),
    senderIp: String(raw.senderIp ?? raw.SenderIp ?? ''),
  };
}

/** 客服回复可能带 HTML，展示前过滤为纯文本 */
export function getDisplayContent(message: DMessage): string {
  const content = message.content ?? '';
  if (message.senderRole === MessageSenderRole.Agent) {
    return stripHtml(content);
  }
  return content;
}

export async function getMessages(): Promise<ApiResult<DMessage[]>> {
  const result = await api.get<Record<string, unknown>[]>('/api/message/@GetMessages');
  if (!apiOk(result) || !Array.isArray(result.data)) {
    return result as unknown as ApiResult<DMessage[]>;
  }

  return {
    ...result,
    data: result.data.map((item) => normalizeMessage(item)),
  };
}

export async function sendMessage(content: string): Promise<ApiResult<unknown>> {
  return await api.post<unknown>(
    `/api/message/@SendMessage?content=${encodeURIComponent(content)}`
  );
}

export async function markAsRead(id: number): Promise<ApiResult<unknown>> {
  return await api.post<unknown>(`/api/message/@MarkAsRead?id=${id}`);
}

export async function markAllAsRead(): Promise<ApiResult<unknown>> {
  return await api.post<unknown>('/api/message/@MarkAllAsRead');
}

export async function deleteMessage(id: number): Promise<ApiResult<unknown>> {
  return await api.post<unknown>(`/api/message/@DeleteMessage?id=${id}`);
}
