export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  googleId?: string;
  subscriptionTier: string;
  isAdmin?: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  confirmPassword: string;
  firstName: string;
  lastName: string;
  currency?: string;
  country?: string;
}

export interface LoginResponse {
  token: string;
  refreshToken: string;
  user: User;
}

export interface GoogleAuthRequest {
  idToken: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}

export interface Transaction {
  id: string;
  accountId: string;
  accountName: string;
  transactionDate: string;
  postedDate: string;
  amount: number;
  type: string;
  merchant: string;
  normalizedMerchant?: string;
  categoryId?: string;
  categoryName?: string;
  description?: string;
  notes?: string;
  isPending: boolean;
  isRecurring: boolean;
  isTransfer: boolean;
  isSplit: boolean;
  tags?: string;
}

export interface CreateTransaction {
  accountId: string;
  transactionDate: string;
  amount: number;
  merchant: string;
  categoryId?: string;
  description?: string;
  notes?: string;
  tags?: string;
}

export interface UpdateTransaction {
  categoryId?: string;
  notes?: string;
  tags?: string;
  merchant?: string;
}

export interface Account {
  id: string;
  name: string;
  type: string;
  balance: number;
  currency: string;
  institution?: string;
  accountNumber?: string;
}

export interface CreateAccount {
  name: string;
  type: string;
  initialBalance?: number;
  currency: string;
  institution?: string;
  accountNumber?: string;
}

export interface Category {
  id: string;
  name: string;
  type: string;
  icon?: string;
  color?: string;
  parentCategoryId?: string;
  budgetAmount?: number;
  isSystem: boolean;
}

export interface CreateCategory {
  name: string;
  type: string;
  icon?: string;
  color?: string;
  parentCategoryId?: string;
  budgetAmount?: number;
  displayOrder?: number;
}

export interface TransactionFilter {
  accountId?: string;
  categoryId?: string;
  uncategorizedOnly?: boolean;
  startDate?: string;
  endDate?: string;
  minAmount?: number;
  maxAmount?: number;
  searchTerm?: string;
  isRecurring?: boolean;
  isPending?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ApiResponse<T> {
  data: T;
  success: boolean;
  message?: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface DashboardStats {
  monthlyExpenses: number;
  monthlyIncome: number;
  netSavings: number;
  transactionCount: number;
}

export interface MonthlyInsightsRequest {
  month: number; // 1-12
  year: number;
  accountIds?: string[];
  sampleSize?: number;
}

export interface MonthlyInsightsChartData {
  label: string;
  value: number;
  color?: string;
  metadata?: Record<string, any>;
}

export interface MonthlyInsightsTopMerchant {
  merchant: string;
  amount: number;
  count: number;
}

export interface MonthlyInsightsSampleTransaction {
  transactionDateUtc: string;
  merchant: string;
  amount: number;
  category: string;
}

export interface MonthlyInsightsAi {
  usedAi: boolean;
  summary: string;
  highlights: string[];
  suggestions: string[];
  watchouts: string[];
}

export interface MonthlyInsightsResponse {
  periodStartUtc: string;
  periodEndUtc: string;
  totalIncome: number;
  totalExpenses: number;
  net: number;
  transactionCount: number;
  spendingByCategory: MonthlyInsightsChartData[];
  topMerchants: MonthlyInsightsTopMerchant[];
  sampleTransactions: MonthlyInsightsSampleTransaction[];
  ai: MonthlyInsightsAi;
}