import { api, ApiResult, request } from './request';

export interface CheckInStatus {
  activityPoint: number;
  continuousDays: number;
  isTodayChecked: boolean;
  today: string;
  todayWeek: string;
  checkInDays: {
    day: string;
    date: string;
    reward: string;
    checked: boolean;
    isToday: boolean;
  }[];
}

export interface TimeLimitedEvent {
  name: string;
  desc: string;
  image: string;
  progress: number;
  total: number;
  timeLeft: string;
}

export interface DailyTask {
  id: string;
  title: string;
  description: string;
  icon: string;
  jumpPath: string;
  rewardAmount: number;
  activityPoint: number;
  currentValue: number;
  targetValue: number;
  status: number;
}

export interface DailyTaskResponse {
  tasks: DailyTask[];
  totalActivityPoint: number;
  claimedChests: number[];
}

export interface MonthlyTaskActivityDay {
  date: string;
  taskActivityPoint: number;
}

export interface MonthlyTaskActivityResponse {
  year: number;
  month: number;
  days: MonthlyTaskActivityDay[];
  monthTotal: number;
  activeDays: number;
}

export async function getCheckInStatus(): Promise<ApiResult<CheckInStatus>> {
  return await api.get<CheckInStatus>('/api/event/@GetCheckInStatus');
}

export async function getTimeLimitedEvents(): Promise<ApiResult<TimeLimitedEvent[]>> {
  return await api.get<TimeLimitedEvent[]>('/api/event/@GetTimeLimitedEvents');
}

export async function getDailyTasks(): Promise<ApiResult<DailyTaskResponse>> {
  return await api.get<DailyTaskResponse>('/api/event/@GetDailyTasks');
}

export async function claimDailyTask(taskId: string): Promise<ApiResult<{ newBalance: number }>> {
  return await api.post<{ newBalance: number }>('/api/event/@ClaimDailyTask', taskId, {
    headers: { 'Content-Type': 'application/json' },
  });
}

export async function claimActivityChest(
  targetPoint: number
): Promise<ApiResult<{ newBalance: number }>> {
  return await api.post<{ newBalance: number }>('/api/event/@ClaimActivityChest', targetPoint, {
    headers: { 'Content-Type': 'application/json' },
  });
}

type RequestWithParams = RequestInit & { params?: Record<string, string | number> };

export async function getMonthlyTaskActivity(
  year: number,
  month: number
): Promise<ApiResult<MonthlyTaskActivityResponse>> {
  return await request<MonthlyTaskActivityResponse>('/api/event/@GetMonthlyTaskActivity', {
    method: 'GET',
    params: { year, month },
  } as RequestWithParams);
}
