import { api, type ApiResult } from './request';

export enum MessageSenderRole {
  Customer = 'Customer',
  Agent = 'Agent',
  System = 'System',
}

export enum MessageStatus {
  未读 = '未读',
  已读 = '已读',
  已回复 = '已回复',
  已撤回 = '已撤回',
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

export async function getMessages(): Promise<ApiResult<DMessage[]>> {
  return await api.get<DMessage[]>('/api/message/@GetMessages');
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
