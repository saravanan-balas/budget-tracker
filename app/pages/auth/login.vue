<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
    <div class="max-w-md w-full space-y-8">
      <div class="card">
        <div class="text-center mb-6">
          <h1 class="text-2xl font-bold text-gray-900 mb-2">Welcome back</h1>
          <p class="text-gray-600">Sign in to your account to continue</p>
        </div>

        <div v-if="error" class="bg-red-50 border border-red-200 text-red-600 px-4 py-3 rounded-md mb-4">
          {{ error }}
        </div>

        <form @submit.prevent="handleLogin" class="space-y-4">
          <div>
            <label class="form-label">Email Address</label>
            <input 
              v-model="form.email" 
              type="email" 
              required 
              class="form-input" 
              placeholder="Enter your email"
              autocomplete="email"
            >
          </div>

          <div>
            <label class="form-label">Password</label>
            <input 
              v-model="form.password" 
              type="password" 
              required 
              class="form-input" 
              placeholder="Enter your password"
              autocomplete="current-password"
            >
          </div>

          <div class="flex items-center justify-between">
            <div class="flex items-center">
              <input 
                v-model="form.rememberMe" 
                type="checkbox" 
                class="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
              >
              <label class="ml-2 text-sm text-gray-700">Remember me</label>
            </div>
            <NuxtLink to="/auth/forgot-password" class="text-sm text-blue-600 hover:underline">
              Forgot password?
            </NuxtLink>
          </div>

          <button 
            type="submit" 
            :disabled="loading"
            class="w-full btn-primary py-3 text-lg flex items-center justify-center"
          >
            <div v-if="loading" class="animate-spin rounded-full h-5 w-5 border-b-2 border-white mr-2"></div>
            Sign In
          </button>
        </form>

        <div class="mt-6">
          <div class="relative">
            <div class="absolute inset-0 flex items-center">
              <div class="w-full border-t border-gray-300" />
            </div>
            <div class="relative flex justify-center text-sm">
              <span class="px-2 bg-white text-gray-500">Or continue with</span>
            </div>
          </div>

          <div class="mt-6">
            <button 
              @click="handleGoogleSignIn"
              :disabled="loading"
              class="w-full flex justify-center items-center px-4 py-3 border border-gray-300 rounded-md shadow-sm bg-white text-sm font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50"
            >
              <svg class="w-5 h-5 mr-2" viewBox="0 0 24 24">
                <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
                <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
                <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/>
                <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/>
              </svg>
              <div v-if="loading" class="animate-spin rounded-full h-4 w-4 border-b-2 border-gray-600 mr-2"></div>
              Continue with Google
            </button>
          </div>
        </div>

        <div class="mt-6 text-center">
          <p class="text-gray-600">
            Don't have an account? 
            <NuxtLink to="/auth/register" class="text-blue-600 font-medium hover:underline">
              Sign up
            </NuxtLink>
          </p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({
  layout: false
})

import { onMounted } from 'vue'

const form = reactive({
  email: 'test@example.com', // Pre-fill for testing
  password: 'Test123**', // Pre-fill for testing
  rememberMe: false
})

const error = ref('')
const loading = ref(false)

const authStore = useAuthStore()

const handleLogin = async () => {
  error.value = ''
  loading.value = true

  try {
    console.log('Attempting real login...')
    
    // Use the auth store for consistent state management
    await authStore.login({
      email: form.email,
      password: form.password
    })
    
    console.log('Login successful, redirecting to dashboard...')
    
    // Redirect to dashboard
    await navigateTo('/dashboard')
  } catch (err: any) {
    console.error('Login error:', err)
    error.value = err.data?.error || err.message || 'Invalid email or password. Please try again.'
  } finally {
    loading.value = false
  }
}

const handleGoogleSignIn = async () => {
  error.value = ''
  loading.value = true

  try {
    // Load Google OAuth script if not already loaded
    if (!window.gapi) {
      await loadGoogleScript()
    }

    // Use the older, more reliable gapi.auth2 method
    const authInstance = window.gapi.auth2.getAuthInstance()
    const user = await authInstance.signIn()
    const idToken = user.getAuthResponse().id_token
    
    // Call our callback with the token
    await handleGoogleCallback({ credential: idToken })
  } catch (err: any) {
    console.error('Google sign-in error:', err)
    error.value = 'Failed to sign in with Google. Please try again.'
    loading.value = false
  }
}

const handleGoogleCallback = async (response: any) => {
  try {
    console.log('Google callback received, authenticating...')
    
    // Use the auth store for Google authentication
    await authStore.googleAuth({
      idToken: response.credential
    })
    
    console.log('Google authentication successful, redirecting to dashboard...')
    
    // Redirect to dashboard
    await navigateTo('/dashboard')
  } catch (err: any) {
    console.error('Google authentication error:', err)
    error.value = err.data?.error || err.message || 'Google authentication failed. Please try again.'
  } finally {
    loading.value = false
  }
}

const loadGoogleScript = () => {
  return new Promise((resolve, reject) => {
    if (window.gapi) {
      resolve(true)
      return
    }

    const script = document.createElement('script')
    script.src = 'https://apis.google.com/js/api.js'
    script.async = true
    script.defer = true
    script.onload = () => {
      window.gapi.load('auth2', () => {
        window.gapi.auth2.init({
          client_id: 'YOUR_GOOGLE_CLIENT_ID'
        }).then(() => resolve(true))
      })
    }
    script.onerror = () => reject(new Error('Failed to load Google script'))
    document.head.appendChild(script)
  })
}

// Initialize Google Sign-In when component mounts
onMounted(async () => {
  try {
    await loadGoogleScript()
    console.log('Google Sign-In initialized successfully')
  } catch (err) {
    console.error('Failed to initialize Google Sign-In:', err)
  }
})
</script>