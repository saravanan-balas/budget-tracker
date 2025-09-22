<template>
  <div class="max-w-2xl mx-auto py-8 px-4 sm:px-6 lg:px-8">
    <div class="mb-8">
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-3xl font-bold text-gray-900">Profile Settings</h1>
          <p class="mt-2 text-gray-600">Update your personal information and preferences.</p>
        </div>
        <NuxtLink
          to="/settings"
          class="inline-flex items-center px-4 py-2 border border-gray-300 shadow-sm text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500"
        >
          <svg class="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
          </svg>
          Back to Settings
        </NuxtLink>
      </div>
    </div>

    <div class="bg-white shadow rounded-lg">
      <form @submit.prevent="handleSubmit" class="space-y-6 p-6">
        <!-- Personal Information -->
        <div>
          <h3 class="text-lg leading-6 font-medium text-gray-900 mb-4">Personal Information</h3>
          <div class="grid grid-cols-1 gap-6 sm:grid-cols-2">
            <div>
              <label for="firstName" class="block text-sm font-medium text-gray-700">
                First Name
              </label>
              <input
                id="firstName"
                v-model="form.firstName"
                type="text"
                required
                class="mt-1 block w-full border-gray-300 rounded-md shadow-sm focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
              />
            </div>

            <div>
              <label for="lastName" class="block text-sm font-medium text-gray-700">
                Last Name
              </label>
              <input
                id="lastName"
                v-model="form.lastName"
                type="text"
                required
                class="mt-1 block w-full border-gray-300 rounded-md shadow-sm focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
              />
            </div>
          </div>
        </div>

        <!-- Email (Read-only) -->
        <div>
          <label for="email" class="block text-sm font-medium text-gray-700">
            Email Address
          </label>
          <input
            id="email"
            :value="user?.email || 'user@example.com'"
            type="email"
            disabled
            class="mt-1 block w-full border-gray-300 rounded-md shadow-sm bg-gray-50 text-gray-500 sm:text-sm"
          />
          <p class="mt-2 text-sm text-gray-500">Email cannot be changed. Contact support if needed.</p>
        </div>

        <!-- Preferences -->
        <div>
          <h3 class="text-lg leading-6 font-medium text-gray-900 mb-4">Preferences</h3>
          <div class="grid grid-cols-1 gap-6 sm:grid-cols-3">
            <div>
              <label for="currency" class="block text-sm font-medium text-gray-700">
                Default Currency
              </label>
              <select
                id="currency"
                v-model="form.currency"
                class="mt-1 block w-full border-gray-300 rounded-md shadow-sm focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
              >
                <option value="USD">USD - US Dollar</option>
                <option value="EUR">EUR - Euro</option>
                <option value="GBP">GBP - British Pound</option>
                <option value="CAD">CAD - Canadian Dollar</option>
                <option value="AUD">AUD - Australian Dollar</option>
              </select>
            </div>

            <div>
              <label for="country" class="block text-sm font-medium text-gray-700">
                Country
              </label>
              <select
                id="country"
                v-model="form.country"
                class="mt-1 block w-full border-gray-300 rounded-md shadow-sm focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
              >
                <option value="US">United States</option>
                <option value="CA">Canada</option>
                <option value="GB">United Kingdom</option>
                <option value="AU">Australia</option>
                <option value="DE">Germany</option>
                <option value="FR">France</option>
              </select>
            </div>

            <div>
              <label for="timezone" class="block text-sm font-medium text-gray-700">
                Time Zone
              </label>
              <select
                id="timezone"
                v-model="form.timeZone"
                class="mt-1 block w-full border-gray-300 rounded-md shadow-sm focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
              >
                <option value="UTC">UTC</option>
                <option value="America/New_York">Eastern Time (ET)</option>
                <option value="America/Chicago">Central Time (CT)</option>
                <option value="America/Denver">Mountain Time (MT)</option>
                <option value="America/Los_Angeles">Pacific Time (PT)</option>
                <option value="Europe/London">London (GMT)</option>
              </select>
            </div>
          </div>
        </div>

        <!-- Success Message -->
        <div v-if="success" class="rounded-md bg-green-50 p-4">
          <div class="flex">
            <div class="flex-shrink-0">
              <svg class="h-5 w-5 text-green-400" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
              </svg>
            </div>
            <div class="ml-3">
              <h3 class="text-sm font-medium text-green-800">Success</h3>
              <div class="mt-2 text-sm text-green-700">{{ success }}</div>
            </div>
          </div>
        </div>

        <!-- Submit Button -->
        <div class="flex justify-end space-x-3">
          <NuxtLink
            to="/settings"
            class="inline-flex items-center px-4 py-2 border border-gray-300 shadow-sm text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500"
          >
            Cancel
          </NuxtLink>
          <button
            type="submit"
            :disabled="loading"
            class="inline-flex items-center px-4 py-2 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <svg v-if="loading" class="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
            </svg>
            {{ loading ? 'Saving...' : 'Save Changes' }}
          </button>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({
  layout: 'default',
  middleware: 'auth'
})

const authStore = useAuthStore()

const loading = ref(false)
const success = ref('')

const form = ref({
  firstName: '',
  lastName: '',
  currency: 'USD',
  country: 'US',
  timeZone: 'UTC'
})

const user = computed(() => authStore.user)

// Load user data
onMounted(() => {
  // First try to load from localStorage
  const savedProfile = localStorage.getItem('userProfile')
  if (savedProfile) {
    try {
      const profileData = JSON.parse(savedProfile)
      form.value = {
        firstName: profileData.firstName || '',
        lastName: profileData.lastName || '',
        currency: profileData.currency || 'USD',
        country: profileData.country || 'US',
        timeZone: profileData.timeZone || 'UTC'
      }
    } catch (error) {
      console.error('Error parsing saved profile:', error)
    }
  }
  
  // Fallback to user data from auth store
  if (user.value && !savedProfile) {
    form.value = {
      firstName: user.value.firstName || '',
      lastName: user.value.lastName || '',
      currency: user.value.currency || 'USD',
      country: user.value.country || 'US',
      timeZone: user.value.timeZone || 'UTC'
    }
  }
})

const handleSubmit = async () => {
  loading.value = true
  success.value = ''

  try {
    // Update the user data in the auth store
    if (authStore.user) {
      const updatedUser = {
        ...authStore.user,
        firstName: form.value.firstName,
        lastName: form.value.lastName,
        currency: form.value.currency,
        country: form.value.country,
        timeZone: form.value.timeZone
      }
      
      // Update auth store
      authStore.updateUser(updatedUser)
      
      // Store in localStorage for persistence
      localStorage.setItem('userProfile', JSON.stringify({
        firstName: form.value.firstName,
        lastName: form.value.lastName,
        currency: form.value.currency,
        country: form.value.country,
        timeZone: form.value.timeZone,
        updatedAt: new Date().toISOString()
      }))
    }
    
    success.value = 'Profile updated successfully!'
    
    // Clear success message after 3 seconds
    setTimeout(() => {
      success.value = ''
    }, 3000)
  } catch (err) {
    console.error('Error updating profile:', err)
  } finally {
    loading.value = false
  }
}
</script>
