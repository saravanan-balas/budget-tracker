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