<template>
  <div>
    <div class="mb-8 flex justify-between items-start">
      <div>
        <h1 class="text-3xl font-bold text-gray-900">Accounts</h1>
        <p class="text-gray-600 mt-2">Manage your financial accounts</p>
      </div>
      
      <button @click="openAddAccountModal" class="bg-blue-600 text-white font-semibold px-4 py-2 rounded-lg hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 transition-colors flex items-center space-x-2">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6"></path>
        </svg>
        <span>Add Account</span>
      </button>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mr-4"></div>
      <span>Loading accounts...</span>
    </div>

    <!-- Accounts List -->
    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
      <!-- Empty State -->
      <div v-if="accounts.length === 0" class="col-span-full text-center py-12">
        <svg class="w-24 h-24 mx-auto mb-4 text-gray-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z"></path>
        </svg>
        <h3 class="text-lg font-medium text-gray-900 mb-2">No accounts yet</h3>
        <p class="text-gray-500 mb-4">Get started by adding your first account</p>
        <button @click="openAddAccountModal" class="bg-blue-600 text-white font-semibold px-4 py-2 rounded-lg hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 transition-colors flex items-center space-x-2 mx-auto">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6"></path>
          </svg>
          <span>Add Your First Account</span>
        </button>
      </div>

      <!-- Credit Card Style Account Cards -->
      <div v-else v-for="account in accounts" :key="account.id" class="relative">
        <!-- Credit Card -->
        <div 
          class="w-full h-32 rounded-xl shadow-lg hover:shadow-xl transition-all duration-300 transform hover:-translate-y-1 cursor-pointer relative overflow-hidden"
          :class="getAccountCardStyle(account.type)"
          @click="openEditAccountModal(account)"
        >
          <!-- Card Background Pattern -->
          <div class="absolute inset-0 opacity-10">
            <div class="absolute top-2 right-2 w-8 h-8 rounded-full bg-white"></div>
            <div class="absolute top-4 right-6 w-4 h-4 rounded-full bg-white"></div>
            <div class="absolute bottom-2 left-2 w-6 h-6 rounded-full bg-white"></div>
          </div>
          
          <!-- Card Content -->
          <div class="relative h-full p-4 flex flex-col justify-between text-white">
            <!-- Top Row -->
            <div class="flex items-start justify-between">
              <div class="flex items-center space-x-2">
                <span class="text-lg">{{ getAccountIcon(account.type) }}</span>
                <span class="text-sm font-medium opacity-90">{{ account.type }}</span>
              </div>
              <!-- Menu Button -->
              <button @click.stop="toggleMenu(account.id)" class="text-white/80 hover:text-white p-1 rounded">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 5v.01M12 12v.01M12 19v.01M12 6a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2z"></path>
                </svg>
              </button>
            </div>
            
            <!-- Account Name -->
            <div>
              <h3 class="font-semibold text-base leading-tight truncate mb-1">{{ account.name }}</h3>
              <!-- Account Number -->
              <div v-if="account.accountNumber" class="text-xs opacity-80 font-mono tracking-wider">
                •••• {{ account.accountNumber.slice(-4) }}
              </div>
              <div v-else class="text-xs opacity-80">
                {{ account.institution || 'No institution' }}
              </div>
            </div>
            
            <!-- Balance -->
            <div class="text-right">
              <div class="text-lg font-bold leading-none">
                {{ account.currency }} {{ formatAmount(account.balance) }}
              </div>
              <div class="text-xs opacity-80">Available Balance</div>
            </div>
          </div>
        </div>
        
        <!-- Dropdown Menu -->
        <div v-show="openMenuId === account.id" class="absolute right-0 top-8 mt-2 w-48 bg-white rounded-md shadow-lg z-20 border">
          <button @click="openEditAccountModal(account)" class="block w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100">
            Edit Account
          </button>
          <button @click="handleDeleteAccount(account.id)" class="block w-full text-left px-4 py-2 text-sm text-red-600 hover:bg-red-50">
            Delete Account
          </button>
        </div>
      </div>
    </div>

    <!-- Add Account Modal -->
    <AddAccountModal 
      v-if="showAddModal"
      @close="closeAddAccountModal"
      @success="handleAccountSuccess"
    />

    <!-- Edit Account Modal -->
    <EditAccountModal 
      v-if="showEditModal && selectedAccount"
      :account="selectedAccount"
      @close="closeEditAccountModal"
      @success="handleAccountSuccess"
    />
  </div>
</template>

<script setup lang="ts">
import type { Account } from '~/types'

definePageMeta({
  middleware: 'auth'
})

// State
const loading = ref(true)
const accounts = ref<Account[]>([])
const showAddModal = ref(false)
const showEditModal = ref(false)
const selectedAccount = ref<Account | null>(null)
const openMenuId = ref<string | null>(null)

// API
const api = useApi()

// Load accounts
const loadAccounts = async () => {
  try {
    loading.value = true
    const accountsData = await api.getAccounts()
    accounts.value = accountsData
  } catch (error) {
    console.error('Error loading accounts:', error)
  } finally {
    loading.value = false
  }
}

// Account icon mapping
const getAccountIcon = (type: string) => {
  const iconMap: { [key: string]: string } = {
    'Checking': '💳',
    'Savings': '🏦',
    'CreditCard': '💳',
    'Investment': '📈',
    'Loan': '💸',
    'Cash': '💵'
  }
  return iconMap[type] || '💰'
}

// Account card style mapping (credit card colors)
const getAccountCardStyle = (type: string) => {
  const styleMap: { [key: string]: string } = {
    'Checking': 'bg-gradient-to-br from-blue-500 to-blue-600',
    'Savings': 'bg-gradient-to-br from-green-500 to-green-600',
    'CreditCard': 'bg-gradient-to-br from-purple-500 to-purple-600',
    'Investment': 'bg-gradient-to-br from-indigo-500 to-indigo-600',
    'Loan': 'bg-gradient-to-br from-red-500 to-red-600',
    'Cash': 'bg-gradient-to-br from-yellow-500 to-orange-500'
  }
  return styleMap[type] || 'bg-gradient-to-br from-gray-500 to-gray-600'
}

// Format amount
const formatAmount = (amount: number) => {
  return Math.abs(amount).toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  })
}

// Menu handling
const toggleMenu = (accountId: string) => {
  openMenuId.value = openMenuId.value === accountId ? null : accountId
}

// Close menus when clicking outside
onMounted(() => {
  loadAccounts()
  
  document.addEventListener('click', (event) => {
    const target = event.target as HTMLElement
    if (!target.closest('button')) {
      openMenuId.value = null
    }
  })
})

// Modal functions
const openAddAccountModal = () => {
  showAddModal.value = true
  openMenuId.value = null
}

const closeAddAccountModal = () => {
  showAddModal.value = false
}

const openEditAccountModal = (account: Account) => {
  selectedAccount.value = account
  showEditModal.value = true
  openMenuId.value = null
}

const closeEditAccountModal = () => {
  showEditModal.value = false
  selectedAccount.value = null
}

const handleAccountSuccess = () => {
  closeAddAccountModal()
  closeEditAccountModal()
  loadAccounts()
}

// Delete account
const handleDeleteAccount = async (accountId: string) => {
  if (!confirm('Are you sure you want to delete this account? This action cannot be undone.')) {
    return
  }
  
  try {
    await api.deleteAccount(accountId)
    await loadAccounts()
    openMenuId.value = null
  } catch (error) {
    console.error('Error deleting account:', error)
    alert('Failed to delete account. It may have associated transactions.')
  }
}
</script>