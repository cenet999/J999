import { api, ApiResult } from './request';

export async function login(username: string, password: string): Promise<ApiResult<string>> {
  return await api.post<string>(
    `/api/login/@Login?Username=${encodeURIComponent(username)}&Password=${encodeURIComponent(password)}`
  );
}

export type MemberInfo = {
  Id?: string | number;
  id?: string | number;
  Username?: string;
  username?: string;
  Nickname?: string;
  nickname?: string;
  DAgentId?: string | number;
  dAgentId?: string | number;
  Avatar?: string;
  avatar?: string;
  PhoneNumber?: string;
  phoneNumber?: string;
  Telegram?: string;
  telegram?: string;
  USDTAddress?: string;
  usdtAddress?: string;
  WithdrawPassword?: string;
  withdrawPassword?: string;
  RebateAmount?: string | number;
  rebateAmount?: string | number;
  RebateTotalAmount?: string | number;
  rebateTotalAmount?: string | number;
  CreditAmount?: string | number;
  creditAmount?: string | number;
};

export async function getMemberInfo(): Promise<ApiResult<MemberInfo>> {
  return await api.get<MemberInfo>('/api/login/@Check');
}

export type AgentInfo = {
  Id?: string | number;
  id?: string | number;
  UsdtAddress?: string;
  usdtAddress?: string;
};

export async function getAgentInfo(agentId: string | number): Promise<ApiResult<AgentInfo>> {
  return await api.get<AgentInfo>(`/api/login/@GetAgentInfo?agentId=${encodeURIComponent(String(agentId))}`);
}

export async function playerCheckIn(): Promise<
  ApiResult<{ bonusPoints: number; continuousDays: number; newPoints: number }>
> {
  return await api.post<{ bonusPoints: number; continuousDays: number; newPoints: number }>(
    '/api/login/@PlayerCheckIn'
  );
}

export async function register(data: {
  Username: string;
  Password: string;
  BrowserFingerprint: string;
  AgentId: number;
  AgentName?: string;
  InviteCode: string;
}): Promise<ApiResult<unknown>> {
  return await api.post<unknown>('/api/login/@Register', data);
}

export async function changePassword(
  oldPassword: string,
  newPassword: string
): Promise<ApiResult<unknown>> {
  return await api.post<unknown>(
    `/api/login/@ChangePassword?OldPassword=${encodeURIComponent(oldPassword)}&NewPassword=${encodeURIComponent(newPassword)}`
  );
}

export async function changeWithdrawPassword(
  loginPassword: string,
  newWithdrawPassword: string
): Promise<ApiResult<unknown>> {
  return await api.post<unknown>(
    `/api/login/@ChangeWithdrawPassword?LoginPassword=${encodeURIComponent(loginPassword)}&NewWithdrawPassword=${encodeURIComponent(newWithdrawPassword)}`
  );
}

export async function updateMemberInfo(
  telegram: string,
  usdtAddress: string,
  phoneNumber: string,
  withdrawPassword: string
): Promise<ApiResult<unknown>> {
  return await api.post<unknown>(
    `/api/login/@UpdateMemberInfo?Telegram=${encodeURIComponent(telegram)}&USDTAddress=${encodeURIComponent(usdtAddress)}&PhoneNumber=${encodeURIComponent(phoneNumber)}&WithdrawPassword=${encodeURIComponent(withdrawPassword)}`
  );
}

export async function uploadAvatar(
  avatarBase64: string
): Promise<ApiResult<{ avatar?: string; Avatar?: string }>> {
  return await api.post<{ avatar?: string; Avatar?: string }>(
    '/api/login/@UploadAvatar',
    { Avatar: avatarBase64 }
  );
}
