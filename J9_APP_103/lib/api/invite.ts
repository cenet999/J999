import { api, type ApiResult } from './request';

export interface InviteRecordItem {
  displayName?: string;
  DisplayName?: string;
  registeredAt?: string;
  RegisteredAt?: string;
}

export interface InviteLeaderboardItem {
  rank?: number;
  Rank?: number;
  displayName?: string;
  DisplayName?: string;
  inviteCount?: number;
  InviteCount?: number;
  isCurrentUser?: boolean;
  IsCurrentUser?: boolean;
}

export interface InviteCenterData {
  agentId?: number;
  AgentId?: number;
  agentName?: string;
  AgentName?: string;
  inviteCode?: string;
  InviteCode?: string;
  totalInvites?: number;
  TotalInvites?: number;
  todayInvites?: number;
  TodayInvites?: number;
  totalInviteTaskReward?: number;
  TotalInviteTaskReward?: number;
  myRank?: number;
  MyRank?: number;
  myInviteCount?: number;
  MyInviteCount?: number;
  records?: InviteRecordItem[];
  Records?: InviteRecordItem[];
  leaderboard?: InviteLeaderboardItem[];
  Leaderboard?: InviteLeaderboardItem[];
}

export async function getInviteCenter(): Promise<ApiResult<InviteCenterData>> {
  return await api.get<InviteCenterData>('/api/login/@GetInviteCenter');
}
