<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
    <div class="max-w-md w-full space-y-8">
      <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
        <div class="text-center mb-6">
          <h1 class="text-2xl font-bold text-gray-900 mb-2">Create your account</h1>
          <p class="text-gray-600">Start managing your finances with AI-powered insights</p>
        </div>

        <div v-if="error" class="bg-red-50 border border-red-200 text-red-600 px-4 py-3 rounded-md mb-4">
          {{ error }}
        </div>

        <form @submit.prevent="handleRegister" class="space-y-4">
          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="form-label">First Name</label>
              <input 
                v-model="form.firstName" 
                type="text" 
                required 
                class="form-input" 
                placeholder="John"
                autocomplete="given-name"
              >
            </div>
            <div>
              <label class="form-label">Last Name</label>
              <input 
                v-model="form.lastName" 
                type="text" 
                required 
                class="form-input" 
                placeholder="Doe"
                autocomplete="family-name"
              >
            </div>
          </div>

          <div>
            <label class="form-label">Email Address</label>
            <input 
              v-model="form.email" 
              type="email" 
              required 
              class="form-input" 
              placeholder="john@example.com"
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
              placeholder="Enter a strong password"
              autocomplete="new-password"
            >
            <p class="text-xs text-gray-500 mt-1">
              Password must be at least 8 characters with uppercase, lowercase, number, and special character
            </p>
          </div>

          <div>
            <label class="form-label">Confirm Password</label>
            <input 
              v-model="form.confirmPassword" 
              type="password" 
              required 
              class="form-input" 
              placeholder="Confirm your password"
              autocomplete="new-password"
            >
          </div>

          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="form-label">Currency</label>
              <select v-model="form.currency" class="form-input">
                <option value="USD">USD ($)</option>
                <option value="EUR">EUR (€)</option>
                <option value="GBP">GBP (£)</option>
                <option value="CAD">CAD (C$)</option>
                <option value="AUD">AUD (A$)</option>
                <option value="INR">INR (₹)</option>
              </select>
            </div>
            <div>
              <label class="form-label">Country</label>
              <select v-model="form.country" class="form-input">
                <option value="US">United States</option>
                <option value="CA">Canada</option>
                <option value="GB">United Kingdom</option>
                <option value="AU">Australia</option>
                <option value="IN">India</option>
                <option value="DE">Germany</option>
                <option value="FR">France</option>
              </select>
            </div>
          </div>

          <div class="flex items-center">
            <input 
              v-model="form.acceptTerms" 
              type="checkbox" 
              required 
              class="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
            >
            <label class="ml-2 text-sm text-gray-700">
              I agree to the <a href="/terms" class="text-blue-600 hover:underline" target="_blank">Terms of Service</a> 
              and <a href="/privacy" class="text-blue-600 hover:underline" target="_blank">Privacy Policy</a>
            </label>
          </div>

          <button 
            type="submit" 
            :disabled="loading"
            class="w-full btn-primary py-3 text-lg flex items-center justify-center"
          >
            <div v-if="loading" class="animate-spin rounded-full h-5 w-5 border-b-2 border-white mr-2"></div>
            Create Account
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
            Already have an account? 
            <NuxtLink to="/auth/login" class="text-blue-600 font-medium hover:underline">Sign in</NuxtLink>
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
  firstName: '',
  lastName: '',
  email: '',
  password: '',
  confirmPassword: '',
  currency: 'USD',
  country: 'US',
  acceptTerms: false
})

const error = ref('')
const loading = ref(false)

const authStore = useAuthStore()

const handleRegister = async () => {
  error.value = ''
  
  // Basic validation
  if (form.password !== form.confirmPassword) {
    error.value = 'Passwords do not match'
    return
  }
  
  if (!form.acceptTerms) {
    error.value = 'You must accept the Terms of Service and Privacy Policy'
    return
  }
  
  loading.value = true

  try {
    console.log('Attempting registration...')
    
    // Use the auth store for consistent state management
    await authStore.register({
      email: form.email,
      password: form.password,
      confirmPassword: form.confirmPassword,
      firstName: form.firstName,
      lastName: form.lastName,
      currency: form.currency,
      country: form.country
    })
    
    console.log('Registration successful, redirecting to dashboard...')
    
    // Redirect to dashboard
    await navigateTo('/dashboard')
  } catch (err: any) {
    console.error('Registration error:', err)
    error.value = err.data?.error || err.message || 'Registration failed. Please try again.'
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

<style scoped>
.form-label {
  @apply block text-sm font-medium text-gray-700 mb-1;
}

.form-input {
  @apply w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm placeholder-gray-400 focus:outline-none focus:ring-blue-500 focus:border-blue-500;
}

.btn-primary {
  @apply bg-blue-600 text-white font-semibold rounded-lg hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 transition-colors disabled:opacity-50 disabled:cursor-not-allowed;
}
</style>