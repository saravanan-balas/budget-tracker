import { defineStore } from 'pinia'
import type { User, LoginRequest, RegisterRequest } from '~/types'

interface AuthState {
  user: User | null
  token: string | null
  isAuthenticated: boolean
  loading: boolean
}

export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    user: null,
    token: null,
    isAuthenticated: false,
    loading: false
  }),

  actions: {
    async login(credentials: LoginRequest) {
      this.loading = true
      try {
        const api = useApi()
        const response = await api.login(credentials)
        
        // Store token and user data
        this.token = response.token
        this.user = response.user
        this.isAuthenticated = true
        
        // Store token in cookie for persistence
        const tokenCookie = useCookie('auth-token', {
          default: () => null,
          maxAge: 60 * 60 * 24 * 7 // 7 days
        })
        tokenCookie.value = response.token
        
        // Store user in cookie
        const userCookie = useCookie('auth-user', {
          default: () => null,
          maxAge: 60 * 60 * 24 * 7,
          serialize: JSON.stringify,
          deserialize: JSON.parse
        })
        userCookie.value = response.user
        
        return response
      } catch (error) {
        this.clearAuth()
        throw error
      } finally {
        this.loading = false
      }
    },

    async register(userData: RegisterRequest) {
      this.loading = true
      try {
        const api = useApi()
        const response = await api.register(userData)
        
        // Store token and user data
        this.token = response.token
        this.user = response.user
        this.isAuthenticated = true
        
        // Store token in cookie for persistence
        const tokenCookie = useCookie('auth-token', {
          default: () => null,
          maxAge: 60 * 60 * 24 * 7 // 7 days
        })
        tokenCookie.value = response.token
        
        // Store user in cookie
        const userCookie = useCookie('auth-user', {
          default: () => null,
          maxAge: 60 * 60 * 24 * 7,
          serialize: JSON.stringify,
          deserialize: JSON.parse
        })
        userCookie.value = response.user
        
        return response
      } catch (error) {
        this.clearAuth()
        throw error
      } finally {
        this.loading = false
      }
    },

    async logout() {
      try {
        const api = useApi()
        await api.logout()
      } catch (error) {
        console.error('Logout error:', error)
      } finally {
        this.clearAuth()
        await navigateTo('/auth/login')
      }
    },

    clearAuth() {
      this.user = null
      this.token = null
      this.isAuthenticated = false
      
      // Clear cookies
      const tokenCookie = useCookie('auth-token')
      const userCookie = useCookie('auth-user', {
        default: () => null,
        serialize: JSON.stringify,
        deserialize: JSON.parse
      })
      tokenCookie.value = null
      userCookie.value = null
    },

    initializeAuth() {
      // Check if user is already authenticated from cookies
      const tokenCookie = useCookie('auth-token')
      const userCookie = useCookie('auth-user', {
        default: () => null,
        serialize: JSON.stringify,
        deserialize: JSON.parse
      })
      
      if (tokenCookie.value && userCookie.value) {
        try {
          this.token = tokenCookie.value
          this.user = userCookie.value
          this.isAuthenticated = true
        } catch (error) {
          console.error('Error parsing user data:', error)
          this.clearAuth()
        }
      }
    }
  }
})