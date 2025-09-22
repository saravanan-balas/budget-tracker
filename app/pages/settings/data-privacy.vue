<template>
  <div class="max-w-4xl mx-auto py-8 px-4 sm:px-6 lg:px-8">
    <div class="mb-8">
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-3xl font-bold text-gray-900">Data & Privacy</h1>
          <p class="mt-2 text-gray-600">Manage your data and privacy settings.</p>
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

    <div class="space-y-6">
      <!-- Data Export -->
      <div class="bg-white shadow rounded-lg">
        <div class="px-6 py-5">
          <div class="flex items-center justify-between">
            <div>
              <h3 class="text-lg leading-6 font-medium text-gray-900">Export Your Data</h3>
              <p class="mt-2 text-sm text-gray-500">
                Download a complete copy of your budget data including accounts, transactions, categories, goals, and rules.
              </p>
              <p class="mt-1 text-xs text-gray-400">
                Data will be exported as a JSON file containing all your personal information.
              </p>
            </div>
            <button
              @click="exportData"
              :disabled="exportLoading"
              class="inline-flex items-center px-4 py-2 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <svg v-if="exportLoading" class="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              <svg v-else class="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
              {{ exportLoading ? 'Exporting...' : 'Export Data' }}
            </button>
          </div>
          <div v-if="exportSuccess" class="mt-4 rounded-md bg-green-50 p-4">
            <div class="flex">
              <div class="flex-shrink-0">
                <svg class="h-5 w-5 text-green-400" viewBox="0 0 20 20" fill="currentColor">
                  <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
                </svg>
              </div>
              <div class="ml-3">
                <h3 class="text-sm font-medium text-green-800">Export Complete</h3>
                <div class="mt-2 text-sm text-green-700">Your data has been downloaded successfully.</div>
              </div>
            </div>
          </div>
          <div v-if="exportError" class="mt-4 rounded-md bg-red-50 p-4">
            <div class="flex">
              <div class="flex-shrink-0">
                <svg class="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
                  <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd" />
                </svg>
              </div>
              <div class="ml-3">
                <h3 class="text-sm font-medium text-red-800">Export Failed</h3>
                <div class="mt-2 text-sm text-red-700">{{ exportError }}</div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Privacy Information -->
      <div class="bg-white shadow rounded-lg">
        <div class="px-6 py-5">
          <h3 class="text-lg leading-6 font-medium text-gray-900 mb-4">Privacy Information</h3>
          <div class="prose prose-sm text-gray-500 max-w-none">
            <p class="mb-4">
              We take your privacy seriously. Here's how we handle your data:
            </p>
            <ul class="list-disc list-inside space-y-2 mb-4">
              <li>Your financial data is encrypted and stored securely</li>
              <li>We never sell or share your personal information with third parties</li>
              <li>All data transmission is encrypted using industry-standard protocols</li>
              <li>You can export your data at any time</li>
              <li>You can delete your account and all data permanently</li>
              <li>We use your data only to provide and improve our service</li>
            </ul>
            <div class="bg-blue-50 border border-blue-200 rounded-md p-4">
              <h4 class="text-sm font-medium text-blue-800 mb-2">Your Rights</h4>
              <ul class="text-sm text-blue-700 list-disc list-inside space-y-1">
                <li>Access: You can view all your personal data</li>
                <li>Portability: You can export your data in a machine-readable format</li>
                <li>Deletion: You can request complete deletion of your account and data</li>
                <li>Correction: You can update or correct your personal information</li>
              </ul>
            </div>
          </div>
        </div>
      </div>

      <!-- Account Deletion -->
      <div class="bg-white shadow rounded-lg border-l-4 border-red-400">
        <div class="px-6 py-5">
          <div class="flex items-start">
            <div class="flex-shrink-0">
              <svg class="h-6 w-6 text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.732-.833-2.5 0L4.268 19.5c-.77.833.192 2.5 1.732 2.5z" />
              </svg>
            </div>
            <div class="ml-3 flex-1">
              <h3 class="text-lg leading-6 font-medium text-gray-900">Delete Account</h3>
              <p class="mt-2 text-sm text-gray-500">
                Permanently delete your account and all associated data. This action cannot be undone.
              </p>
              <div class="mt-4">
                <button
                  @click="showDeleteModal = true"
                  class="inline-flex items-center px-4 py-2 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-red-600 hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500"
                >
                  <svg class="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                  Delete Account
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Delete Account Modal -->
    <div v-if="showDeleteModal" class="fixed inset-0 z-50 overflow-y-auto">
      <div class="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
        <div class="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" @click="showDeleteModal = false"></div>
        
        <span class="hidden sm:inline-block sm:align-middle sm:h-screen">&#8203;</span>
        
        <div class="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full">
          <div class="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <div class="sm:flex sm:items-start">
              <div class="mx-auto flex-shrink-0 flex items-center justify-center h-12 w-12 rounded-full bg-red-100 sm:mx-0 sm:h-10 sm:w-10">
                <svg class="h-6 w-6 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.732-.833-2.5 0L4.268 19.5c-.77.833.192 2.5 1.732 2.5z" />
                </svg>
              </div>
              <div class="mt-3 text-center sm:mt-0 sm:ml-4 sm:text-left">
                <h3 class="text-lg leading-6 font-medium text-gray-900">Delete Account</h3>
                <div class="mt-2">
                  <p class="text-sm text-gray-500">
                    Are you sure you want to delete your account? This action will permanently delete all your data including:
                  </p>
                  <ul class="mt-2 text-sm text-gray-500 list-disc list-inside">
                    <li>All accounts and transactions</li>
                    <li>Categories and rules</li>
                    <li>Goals and budgets</li>
                    <li>All other personal data</li>
                  </ul>
                  <p class="mt-2 text-sm text-gray-500">
                    This action cannot be undone.
                  </p>
                </div>
              </div>
            </div>
          </div>
          
          <div class="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <form @submit.prevent="deleteAccount" class="w-full">
              <div class="mb-4">
                <label for="confirmation" class="block text-sm font-medium text-gray-700 mb-2">
                  Type "DELETE MY ACCOUNT" to confirm
                </label>
                <input
                  id="confirmation"
                  v-model="deleteForm.confirmationPhrase"
                  type="text"
                  required
                  class="w-full border-gray-300 rounded-md shadow-sm focus:ring-red-500 focus:border-red-500 sm:text-sm"
                  placeholder="DELETE MY ACCOUNT"
                />
              </div>
              <div class="flex justify-end space-x-3">
                <button
                  type="button"
                  @click="showDeleteModal = false"
                  class="inline-flex justify-center w-full rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:mt-0 sm:w-auto sm:text-sm"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  :disabled="deleteLoading || !isDeleteFormValid"
                  class="inline-flex justify-center w-full rounded-md border border-transparent shadow-sm px-4 py-2 bg-red-600 text-base font-medium text-white hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500 disabled:opacity-50 disabled:cursor-not-allowed sm:mt-0 sm:w-auto sm:text-sm"
                >
                  <svg v-if="deleteLoading" class="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                  </svg>
                  {{ deleteLoading ? 'Deleting...' : 'Delete Account' }}
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({
  layout: 'default',
  middleware: 'auth'
})

const authStore = useAuthStore()

const showDeleteModal = ref(false)
const exportLoading = ref(false)
const exportSuccess = ref(false)
const exportError = ref('')
const deleteLoading = ref(false)

const deleteForm = ref({
  confirmationPhrase: ''
})

const isDeleteFormValid = computed(() => {
  return deleteForm.value.confirmationPhrase === 'DELETE MY ACCOUNT'
})

const exportData = async () => {
  exportLoading.value = true
  exportError.value = ''
  exportSuccess.value = false

  try {
    // Simulate data collection
    await new Promise(resolve => setTimeout(resolve, 1500))

    // Create mock data for export
    const exportData = {
      user: {
        email: authStore.user?.email || 'user@example.com',
        firstName: authStore.user?.firstName || '',
        lastName: authStore.user?.lastName || '',
        currency: authStore.user?.currency || 'USD',
        country: authStore.user?.country || 'US',
        timeZone: authStore.user?.timeZone || 'UTC',
        subscriptionTier: authStore.user?.subscriptionTier || 'Free'
      },
      accounts: [
        { id: '1', name: 'Checking Account', type: 'Checking', balance: 2500.00, currency: 'USD' },
        { id: '2', name: 'Savings Account', type: 'Savings', balance: 15000.00, currency: 'USD' },
        { id: '3', name: 'Credit Card', type: 'CreditCard', balance: -1250.50, currency: 'USD' }
      ],
      transactions: [
        { id: '1', date: '2024-01-15', description: 'Grocery Store', amount: -85.50, category: 'Food & Dining' },
        { id: '2', date: '2024-01-14', description: 'Salary', amount: 3000.00, category: 'Income' },
        { id: '3', date: '2024-01-13', description: 'Gas Station', amount: -45.00, category: 'Transportation' }
      ],
      categories: [
        { id: '1', name: 'Food & Dining', type: 'Expense', color: '#EF4444' },
        { id: '2', name: 'Income', type: 'Income', color: '#10B981' },
        { id: '3', name: 'Transportation', type: 'Expense', color: '#3B82F6' }
      ],
      goals: [
        { id: '1', name: 'Emergency Fund', targetAmount: 10000, currentAmount: 7500, targetDate: '2024-12-31' },
        { id: '2', name: 'Vacation Fund', targetAmount: 5000, currentAmount: 2500, targetDate: '2024-06-30' }
      ],
      exportedAt: new Date().toISOString(),
      exportedBy: authStore.user?.email || 'user@example.com'
    }

    // Create and download file
    const json = JSON.stringify(exportData, null, 2)
    const blob = new Blob([json], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `budget-tracker-export-${new Date().toISOString().split('T')[0]}.json`
    document.body.appendChild(a)
    a.click()
    URL.revokeObjectURL(url)
    document.body.removeChild(a)

    exportSuccess.value = true
    setTimeout(() => {
      exportSuccess.value = false
    }, 5000)
  } catch (err: any) {
    console.error('Error exporting data:', err)
    exportError.value = err.message || 'Failed to export data. Please try again.'
  } finally {
    exportLoading.value = false
  }
}

const deleteAccount = async () => {
  deleteLoading.value = true

  try {
    // Simulate account deletion
    await new Promise(resolve => setTimeout(resolve, 2000))
    
    // Clear all local data
    localStorage.clear()
    sessionStorage.clear()
    
    // Clear auth and redirect to login
    authStore.clearAuth()
    await navigateTo('/auth/login')
  } catch (err: any) {
    console.error('Error deleting account:', err)
    alert(err.message || 'Failed to delete account. Please try again.')
  } finally {
    deleteLoading.value = false
    showDeleteModal.value = false
    deleteForm.value = {
      confirmationPhrase: ''
    }
  }
}
</script>
