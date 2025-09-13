import type { 
  LoginRequest, 
  LoginResponse,
  RegisterRequest,
  GoogleAuthRequest,
  Transaction, 
  CreateTransaction, 
  UpdateTransaction,
  Account, 
  CreateAccount,
  Category, 
  CreateCategory,
  TransactionFilter 
} from '~/types'

interface ApiOptions {
  method?: string;
  body?: any;
  headers?: Record<string, string>;
}

export const useApi = () => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBaseUrl

  const apiCall = async <T>(endpoint: string, options: ApiOptions = {}): Promise<T> => {
    const token = useCookie('auth-token')
    
    const headers: Record<string, string> = {
      ...options.headers
    }

    // Only set Content-Type for JSON, let browser set it for FormData
    if (!(options.body instanceof FormData)) {
      headers['Content-Type'] = 'application/json'
    }

    if (token.value) {
      headers['Authorization'] = `Bearer ${token.value}`
    }

    try {
      const response = await $fetch<T>(`${baseURL}/api${endpoint}`, {
        method: options.method || 'GET',
        headers,
        body: options.body instanceof FormData ? options.body : 
              options.body ? JSON.stringify(options.body) : undefined,
      })

      return response
    } catch (error: any) {
      console.error('API Error:', error)
      
      // Handle authentication errors
      if (error.status === 401) {
        // Clear token and redirect to login
        token.value = null
        await navigateTo('/auth/login')
      }
      
      throw error
    }
  }

  // Auth endpoints
  const login = (credentials: LoginRequest): Promise<LoginResponse> =>
    apiCall('/auth/login', { method: 'POST', body: credentials })

  const register = (userData: RegisterRequest): Promise<LoginResponse> =>
    apiCall('/auth/register', { method: 'POST', body: userData })

  const logout = (): Promise<void> =>
    apiCall('/auth/logout', { method: 'POST' })

  const googleAuth = (googleAuthData: GoogleAuthRequest): Promise<LoginResponse> =>
    apiCall('/auth/google', { method: 'POST', body: googleAuthData })

  // Transaction endpoints
  const getTransactions = (filter?: TransactionFilter): Promise<Transaction[]> => {
    const queryParams = new URLSearchParams()
    if (filter) {
      Object.entries(filter).forEach(([key, value]) => {
        if (value !== undefined && value !== null) {
          queryParams.append(key, value.toString())
        }
      })
    }
    const query = queryParams.toString()
    return apiCall(`/transactions${query ? `?${query}` : ''}`)
  }

  const getTransaction = (id: string): Promise<Transaction> =>
    apiCall(`/transactions/${id}`)

  const createTransaction = (transaction: CreateTransaction): Promise<Transaction> =>
    apiCall('/transactions', { method: 'POST', body: transaction })

  const updateTransaction = (id: string, update: UpdateTransaction): Promise<Transaction> =>
    apiCall(`/transactions/${id}`, { method: 'PUT', body: update })

  const deleteTransaction = (id: string): Promise<void> =>
    apiCall(`/transactions/${id}`, { method: 'DELETE' })

  // Account endpoints
  const getAccounts = (): Promise<Account[]> =>
    apiCall('/accounts')

  const getAccount = (id: string): Promise<Account> =>
    apiCall(`/accounts/${id}`)

  const createAccount = (account: CreateAccount): Promise<Account> =>
    apiCall('/accounts', { method: 'POST', body: account })

  const updateAccount = (id: string, account: Partial<Account>): Promise<Account> =>
    apiCall(`/accounts/${id}`, { method: 'PUT', body: account })

  const deleteAccount = (id: string): Promise<void> =>
    apiCall(`/accounts/${id}`, { method: 'DELETE' })

  // Category endpoints
  const getCategories = (): Promise<Category[]> =>
    apiCall('/categories')

  const getCategory = (id: string): Promise<Category> =>
    apiCall(`/categories/${id}`)

  const createCategory = (category: CreateCategory): Promise<Category> =>
    apiCall('/categories', { method: 'POST', body: category })

  const seedDefaultCategories = (): Promise<{ message: string }> =>
    apiCall('/categories/seed-defaults', { method: 'POST' })

  // Import endpoints
  const analyzeImportFile = (formData: FormData): Promise<any> =>
    apiCall('/import/analyze', { 
      method: 'POST', 
      body: formData,
      headers: {} // Let browser set content-type for FormData
    })

  const smartImport = (formData: FormData): Promise<any> =>
    apiCall('/import/smart', { 
      method: 'POST', 
      body: formData,
      headers: {} // Let browser set content-type for FormData
    })

  const getImportStatus = (importId: string): Promise<any> =>
    apiCall(`/import/status/${importId}`)

  return {
    // Auth
    login,
    register,
    logout,
    googleAuth,
    
    // Transactions
    getTransactions,
    getTransaction,
    createTransaction,
    updateTransaction,
    deleteTransaction,
    
    // Accounts
    getAccounts,
    getAccount,
    createAccount,
    updateAccount,
    deleteAccount,
    
    // Categories
    getCategories,
    getCategory,
    createCategory,
    seedDefaultCategories,
    
    // Import
    analyzeImportFile,
    smartImport,
    getImportStatus
  }
}