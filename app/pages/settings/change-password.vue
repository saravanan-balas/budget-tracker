<template>
  <div class="max-w-2xl mx-auto py-8 px-4 sm:px-6 lg:px-8">
    <div class="bg-white shadow rounded-lg">
      <div class="px-4 py-5 sm:p-6">
        <h3 class="text-lg leading-6 font-medium text-gray-900 mb-6">
          Change Password
        </h3>
        
        <form @submit.prevent="handleChangePassword" class="space-y-6">
          <div>
            <label for="currentPassword" class="block text-sm font-medium text-gray-700">
              Current Password
            </label>
            <div class="mt-1">
              <input
                id="currentPassword"
                v-model="form.currentPassword"
                name="currentPassword"
                type="password"
                autocomplete="current-password"
                required
                class="shadow-sm focus:ring-indigo-500 focus:border-indigo-500 block w-full sm:text-sm border-gray-300 rounded-md"
                placeholder="Enter your current password"
              />
            </div>
          </div>

          <div>
            <label for="newPassword" class="block text-sm font-medium text-gray-700">
              New Password
            </label>
            <div class="mt-1">
              <input
                id="newPassword"
                v-model="form.newPassword"
                name="newPassword"
                type="password"
                autocomplete="new-password"
                required
                class="shadow-sm focus:ring-indigo-500 focus:border-indigo-500 block w-full sm:text-sm border-gray-300 rounded-md"
                placeholder="Enter your new password"
              />
            </div>
            <p class="mt-2 text-sm text-gray-500">
              Password must be at least 8 characters long and contain uppercase, lowercase, number, and special character.
            </p>
          </div>

          <div>
            <label for="confirmNewPassword" class="block text-sm font-medium text-gray-700">
              Confirm New Password
            </label>
            <div class="mt-1">
              <input
                id="confirmNewPassword"
                v-model="form.confirmNewPassword"
                name="confirmNewPassword"
                type="password"
                autocomplete="new-password"
                required
                class="shadow-sm focus:ring-indigo-500 focus:border-indigo-500 block w-full sm:text-sm border-gray-300 rounded-md"
                placeholder="Confirm your new password"
              />
            </div>
          </div>

          <div v-if="error" class="rounded-md bg-red-50 p-4">
            <div class="flex">
              <div class="flex-shrink-0">
                <svg class="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
                  <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd" />
                </svg>
              </div>
              <div class="ml-3">
                <h3 class="text-sm font-medium text-red-800">
                  {{ error }}
                </h3>
              </div>
            </div>
          </div>

          <div v-if="success" class="rounded-md bg-green-50 p-4">
            <div class="flex">
              <div class="flex-shrink-0">
                <svg class="h-5 w-5 text-green-400" viewBox="0 0 20 20" fill="currentColor">
                  <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
                </svg>
              </div>
              <div class="ml-3">
                <h3 class="text-sm font-medium text-green-800">
                  {{ success }}
                </h3>
              </div>
            </div>
          </div>

          <div class="flex justify-end space-x-3">
            <NuxtLink
              to="/settings"
              class="bg-white py-2 px-4 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500"
            >
              Cancel
            </NuxtLink>
            <button
              type="submit"
              :disabled="loading"
              class="inline-flex justify-center py-2 px-4 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <span v-if="loading" class="flex items-center">
                <svg class="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                  <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                  <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
                {{ loading ? 'Changing...' : 'Change Password' }}
              </span>
              <span v-else>Change Password</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { ChangePasswordRequest } from '~/types'

definePageMeta({
  layout: 'dashboard',
  middleware: 'auth'
})

const form = ref<ChangePasswordRequest>({
  currentPassword: '',
  newPassword: '',
  confirmNewPassword: ''
})

const loading = ref(false)
const error = ref('')
const success = ref('')

const handleChangePassword = async () => {
  error.value = ''
  success.value = ''
  
  if (!form.value.currentPassword || !form.value.newPassword || !form.value.confirmNewPassword) {
    error.value = 'Please fill in all fields'
    return
  }
  
  if (form.value.newPassword !== form.value.confirmNewPassword) {
    error.value = 'New passwords do not match'
    return
  }
  
  if (form.value.newPassword.length < 8) {
    error.value = 'New password must be at least 8 characters long'
    return
  }
  
  // Check password strength
  const passwordRegex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$/
  if (!passwordRegex.test(form.value.newPassword)) {
    error.value = 'Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character'
    return
  }
  
  loading.value = true

  try {
    const api = useApi()
    const response = await api.changePassword(form.value)
    
    success.value = response.message
    
    // Clear form after successful change
    form.value = {
      currentPassword: '',
      newPassword: '',
      confirmNewPassword: ''
    }
  } catch (err: any) {
    console.error('Change password error:', err)
    error.value = err.data?.error || err.message || 'Failed to change password. Please try again.'
  } finally {
    loading.value = false
  }
}
</script>
