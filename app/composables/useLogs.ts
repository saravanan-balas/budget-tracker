interface LogFilter {
  level?: string
  source?: string
  startDate?: string
  endDate?: string
  searchText?: string
  page?: number
  pageSize?: number
}

interface ApplicationLog {
  id: string
  timestamp: string
  level: string
  message: string
  exception?: string
  source?: string
  userId?: string
  properties?: string
}

interface LogResponse {
  items: ApplicationLog[]
  totalCount: number
  page: number
  pageSize: number
}

export const useLogs = () => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBaseUrl

  const apiCall = async <T>(endpoint: string, options: any = {}): Promise<T> => {
    const token = useCookie('auth-token')
    
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...options.headers
    }

    if (token.value) {
      headers['Authorization'] = `Bearer ${token.value}`
    }

    try {
      const response = await $fetch<T>(`${baseURL}/api${endpoint}`, {
        method: options.method || 'GET',
        headers,
        body: options.body ? JSON.stringify(options.body) : undefined,
      })

      return response
    } catch (error: any) {
      console.error('Logs API Error:', error)
      
      if (error.status === 401 || error.status === 403) {
        await navigateTo('/auth/login')
      }
      
      throw error
    }
  }

  const getLogs = (filter?: LogFilter): Promise<LogResponse> => {
    const queryParams = new URLSearchParams()
    if (filter) {
      Object.entries(filter).forEach(([key, value]) => {
        if (value !== undefined && value !== null) {
          queryParams.append(key, value.toString())
        }
      })
    }
    const query = queryParams.toString()
    return apiCall(`/logs${query ? `?${query}` : ''}`)
  }

  const getLogById = (id: string): Promise<ApplicationLog> =>
    apiCall(`/logs/${id}`)

  const getLogLevels = (): Promise<string[]> =>
    apiCall('/logs/levels')

  const getSources = (): Promise<string[]> =>
    apiCall('/logs/sources')

  return {
    getLogs,
    getLogById,
    getLogLevels,
    getSources
  }
}

