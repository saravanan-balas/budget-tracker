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
            <div class="flex items-center space-x-1">
              <NuxtLink to="/auth/forgot-password" class="text-sm text-blue-600 hover:underline">
                Forgot password?
              </NuxtLink>
            </div>
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

          <div class="mt-6 flex justify-center">
            <div id="g_id_signin" class="w-full"></div>
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
  email: '',
  password: '',
  rememberMe: false
})

const error = ref('')
const loading = ref(false)

const authStore = useAuthStore()
const runtimeConfig = useRuntimeConfig()

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

// Handle Google credential response
const handleGoogleCallback = async (response: any) => {
  loading.value = true
  error.value = ''
  
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

// Initialize Google Sign-In with new GIS SDK
onMounted(() => {
  // Make callback globally available for Google to call
  window.handleCredentialResponse = handleGoogleCallback

  // Load Google Identity Services script
  const script = document.createElement('script')
  script.src = 'https://accounts.google.com/gsi/client'
  script.async = true
  script.defer = true
  script.onload = () => {
    // Initialize and render the button
    window.google.accounts.id.initialize({
      client_id: runtimeConfig.public.googleClientId,
      callback: handleGoogleCallback,
      auto_select: false,
      cancel_on_tap_outside: true
    })
    
    // Render the button
    const container = document.getElementById('g_id_signin')
    window.google.accounts.id.renderButton(
      container,
      {
        theme: 'outline',
        size: 'large',
        width: container.offsetWidth || 400,
        text: 'continue_with',
        shape: 'rectangular'
      }
    )
    
    console.log('Google Sign-In button rendered successfully')
  }
  document.head.appendChild(script)
})
</script>