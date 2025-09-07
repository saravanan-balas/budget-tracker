<template>
  <div>
    <div class="mb-8 flex justify-between items-start">
      <div>
        <h1 class="text-3xl font-bold text-gray-900">Financial Dashboard</h1>
        <p class="text-gray-600 mt-2">Your personal finance overview</p>
      </div>
      
      <!-- Quick Actions (moved to top right) -->
      <div class="flex gap-3">
        <NuxtLink to="/import" class="bg-white rounded-lg shadow-sm border border-gray-200 px-4 py-2 hover:shadow-md transition-shadow cursor-pointer flex items-center space-x-2">
          <div class="w-8 h-8 bg-blue-100 rounded-lg flex items-center justify-center">
            <svg class="w-4 h-4 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"></path>
            </svg>
          </div>
          <div class="text-left">
            <h3 class="text-xs font-medium">Import</h3>
            <p class="text-xs text-gray-600">CSV/PDF</p>
          </div>
        </NuxtLink>

        <NuxtLink to="/chat" class="bg-white rounded-lg shadow-sm border border-gray-200 px-4 py-2 hover:shadow-md transition-shadow cursor-pointer flex items-center space-x-2">
          <div class="w-8 h-8 bg-green-100 rounded-lg flex items-center justify-center">
            <svg class="w-4 h-4 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z"></path>
            </svg>
          </div>
          <div class="text-left">
            <h3 class="text-xs font-medium">Ask AI</h3>
            <p class="text-xs text-gray-600">Insights</p>
          </div>
        </NuxtLink>

        <div @click="openQuickAddModal" class="bg-white rounded-lg shadow-sm border border-gray-200 px-4 py-2 hover:shadow-md transition-shadow cursor-pointer flex items-center space-x-2">
          <div class="w-8 h-8 bg-purple-100 rounded-lg flex items-center justify-center">
            <svg class="w-4 h-4 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6"></path>
            </svg>
          </div>
          <div class="text-left">
            <h3 class="text-xs font-medium">Quick Add</h3>
            <p class="text-xs text-gray-600">Transaction</p>
          </div>
        </div>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mr-4"></div>
      <span>Loading your financial data...</span>
    </div>

    <div v-else>
      <!-- Quick Stats -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-6 mb-8">
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-sm font-medium text-gray-500">This Month</p>
              <p class="text-2xl font-bold text-gray-900">{{ formatCurrency(stats.monthlyExpenses) }}</p>
            </div>
            <div class="w-10 h-10 bg-red-100 rounded-lg flex items-center justify-center">
              <span class="text-red-600">📉</span>
            </div>
          </div>
        </div>

        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-sm font-medium text-gray-500">Income</p>
              <p class="text-2xl font-bold text-green-600">{{ formatCurrency(stats.monthlyIncome) }}</p>
            </div>
            <div class="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
              <span class="text-green-600">📈</span>
            </div>
          </div>
        </div>

        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-sm font-medium text-gray-500">Net Savings</p>
              <p class="text-2xl font-bold" :class="stats.netSavings >= 0 ? 'text-blue-600' : 'text-red-600'">
                {{ formatCurrency(Math.abs(stats.netSavings)) }}
              </p>
            </div>
            <div class="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
              <span class="text-blue-600">💰</span>
            </div>
          </div>
        </div>

        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-sm font-medium text-gray-500">Transactions</p>
              <p class="text-2xl font-bold text-gray-900">{{ stats.transactionCount }}</p>
            </div>
            <div class="w-10 h-10 bg-purple-100 rounded-lg flex items-center justify-center">
              <span class="text-purple-600">📋</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Main Content Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- Recent Transactions -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold">Recent Transactions</h2>
            <NuxtLink to="/transactions" class="text-blue-600 hover:underline text-sm">View all</NuxtLink>
          </div>
          <div class="space-y-3">
            <div v-if="recentTransactions.length === 0" class="text-center py-8 text-gray-500">
              <p>No transactions found</p>
              <NuxtLink to="/import" class="text-blue-600 hover:underline text-sm mt-2 inline-block">
                Import transactions
              </NuxtLink>
            </div>
            <div v-else>
              <div 
                v-for="transaction in recentTransactions" 
                :key="transaction.id"
                class="flex items-center justify-between py-3 border-b border-gray-100 hover:bg-gray-50 transition-colors duration-200 cursor-pointer"
                @click="openEditModal(transaction)"
              >
                <div class="flex items-center space-x-3 flex-1">
                  <div class="w-8 h-8 rounded-full flex items-center justify-center" :class="getTransactionIconBg(transaction.amount)">
                    <span class="text-xs font-bold" :class="getTransactionIconText(transaction.amount)">
                      {{ getTransactionIcon(transaction.categoryName) }}
                    </span>
                  </div>
                  <div class="flex-1 min-w-0">
                    <p class="font-medium text-gray-900 truncate">{{ transaction.normalizedMerchant || transaction.merchant }}</p>
                    <p class="text-sm text-gray-500 truncate">{{ transaction.categoryName || 'Uncategorized' }}</p>
                  </div>
                </div>
                <div class="text-right">
                  <p class="font-medium" :class="transaction.amount > 0 ? 'text-green-600' : 'text-red-600'">
                    {{ formatTransactionAmount(transaction.amount) }}
                  </p>
                  <p class="text-sm text-gray-500">{{ formatRelativeDate(transaction.transactionDate) }}</p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Spending by Category Chart -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold">Spending by Category</h2>
            <select class="text-sm border rounded-md px-2 py-1">
              <option>Last 30 days</option>
              <option>Last 3 months</option>
              <option>Last year</option>
            </select>
          </div>
          <div class="h-64">
            <CategoryChart v-if="categoryChartData.length > 0" :data="categoryChartData" />
            <div v-else class="flex items-center justify-center h-full text-gray-500">
              <div class="text-center">
                <svg class="w-12 h-12 mx-auto mb-2 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"></path>
                </svg>
                <p>No transaction data available</p>
                <NuxtLink to="/import" class="text-blue-600 hover:underline text-sm mt-2 inline-block">Import transactions</NuxtLink>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- AI Insights -->
      <div class="mt-8 bg-white rounded-lg shadow-sm border border-gray-200 p-6">
        <h2 class="text-lg font-semibold mb-4">💡 AI Insights</h2>
        <div class="space-y-4">
          <div class="bg-blue-50 p-4 rounded-lg border-l-4 border-blue-400">
            <h4 class="font-semibold text-blue-900 mb-2">Real Data Connected!</h4>
            <p class="text-blue-800">Your dashboard is now showing real transaction data from your database. {{ stats.transactionCount }} transactions loaded.</p>
          </div>
          <div v-if="stats.netSavings > 0" class="bg-green-50 p-4 rounded-lg border-l-4 border-green-400">
            <h4 class="font-semibold text-green-900 mb-2">Positive Net Savings</h4>
            <p class="text-green-800">Great job! You have {{ formatCurrency(stats.netSavings) }} in net savings this month.</p>
          </div>
          <div v-else class="bg-yellow-50 p-4 rounded-lg border-l-4 border-yellow-400">
            <h4 class="font-semibold text-yellow-900 mb-2">Budget Alert</h4>
            <p class="text-yellow-800">Your expenses exceed income this month. Consider reviewing your spending.</p>
          </div>
        </div>
      </div>

    </div>

    <!-- Edit Transaction Modal -->
    <EditTransactionModal 
      v-if="showEditModal && selectedTransaction"
      :transaction="selectedTransaction"
      @close="closeEditModal"
      @success="handleEditSuccess"
    />

    <!-- Quick Add Modal -->
    <QuickAddModal 
      v-if="showQuickAddModal"
      @close="closeQuickAddModal"
      @success="handleQuickAddSuccess"
    />
  </div>
</template>

<script setup lang="ts">
import type { Transaction } from '~/types'

definePageMeta({
  middleware: 'auth'
})

// State
const loading = ref(true)
const stats = reactive({
  monthlyExpenses: 0,
  monthlyIncome: 0,
  netSavings: 0,
  transactionCount: 0
})
const recentTransactions = ref<Transaction[]>([])
const monthlyTransactions = ref<Transaction[]>([])

// Modal states
const showEditModal = ref(false)
const selectedTransaction = ref<Transaction | null>(null)
const showQuickAddModal = ref(false)

// API
const api = useApi()

// Computed properties
const categoryChartData = computed(() => {
  const expenses = monthlyTransactions.value.filter(t => t.amount < 0)
  
  if (expenses.length === 0) return []
  
  const grouped = expenses.reduce((acc, transaction) => {
    const category = transaction.categoryName || 'Uncategorized'
    if (!acc[category]) {
      acc[category] = 0
    }
    acc[category] += Math.abs(transaction.amount)
    return acc
  }, {} as Record<string, number>)
  
  // Convert to array and sort by amount, take top 8
  return Object.entries(grouped)
    .map(([name, amount]) => ({ name, amount }))
    .sort((a, b) => b.amount - a.amount)
    .slice(0, 8)
})

// Load dashboard data
const loadDashboardData = async () => {
  try {
    console.log('Loading real dashboard data...')
    loading.value = true
    
    // Get current month's date range
    const now = new Date()
    const startOfMonth = new Date(now.getFullYear(), now.getMonth(), 1)
    const endOfMonth = new Date(now.getFullYear(), now.getMonth() + 1, 0)
    
    // Load recent transactions (last 5)
    const recentTransactionsData = await api.getTransactions({
      pageSize: 5,
      page: 1
    })
    recentTransactions.value = recentTransactionsData
    
    // Load monthly transactions for stats
    const monthlyTransactionsData = await api.getTransactions({
      startDate: startOfMonth.toISOString(),
      endDate: endOfMonth.toISOString(),
      pageSize: 1000
    })
    monthlyTransactions.value = monthlyTransactionsData
    
    // Calculate stats
    const expenses = monthlyTransactionsData.filter(t => t.amount < 0).reduce((sum, t) => sum + Math.abs(t.amount), 0)
    const income = monthlyTransactionsData.filter(t => t.amount > 0).reduce((sum, t) => sum + t.amount, 0)
    
    stats.monthlyExpenses = expenses
    stats.monthlyIncome = income
    stats.netSavings = income - expenses
    stats.transactionCount = monthlyTransactionsData.length
    
    console.log('Dashboard data loaded:', {
      expenses,
      income,
      netSavings: income - expenses,
      transactionCount: monthlyTransactionsData.length,
      recentTransactions: recentTransactionsData.length
    })
    
  } catch (error) {
    console.error('Error loading dashboard data:', error)
  } finally {
    loading.value = false
  }
}

// Utility functions
const formatCurrency = (amount: number) => {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD'
  }).format(amount)
}

const formatTransactionAmount = (amount: number) => {
  const formatted = Math.abs(amount).toLocaleString('en-US', {
    style: 'currency',
    currency: 'USD'
  })
  return amount > 0 ? `+${formatted}` : `-${formatted}`
}

const formatRelativeDate = (dateString: string) => {
  const date = new Date(dateString)
  const now = new Date()
  const diff = now.getTime() - date.getTime()
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))

  if (days === 0) return 'Today'
  if (days === 1) return 'Yesterday'
  if (days < 7) return `${days} days ago`
  if (days < 30) return `${Math.floor(days / 7)} weeks ago`
  
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

const getTransactionIcon = (categoryName?: string) => {
  const category = categoryName?.toLowerCase()
  const iconMap: { [key: string]: string } = {
    'food & dining': '🍔',
    'dining': '🍔',
    'restaurants': '🍔',
    'groceries': '🛒',
    'transportation': '⛽',
    'gas': '⛽',
    'fuel': '⛽',
    'shopping': '🛍️',
    'entertainment': '🎬',
    'bills & utilities': '📱',
    'utilities': '📱',
    'healthcare': '🏥',
    'medical': '🏥',
    'income': '💰',
    'salary': '💰',
    'deposit': '💰',
    'transfer': '↔️'
  }
  return iconMap[category || ''] || '📝'
}

const getTransactionIconBg = (amount: number) => {
  return amount > 0 ? 'bg-green-100' : 'bg-red-100'
}

const getTransactionIconText = (amount: number) => {
  return amount > 0 ? 'text-green-600' : 'text-red-600'
}

// Modal functions
const openEditModal = (transaction: Transaction) => {
  selectedTransaction.value = transaction
  showEditModal.value = true
}

const closeEditModal = () => {
  showEditModal.value = false
  selectedTransaction.value = null
}

const handleEditSuccess = () => {
  closeEditModal()
  // Reload dashboard data to reflect changes
  loadDashboardData()
}

// Quick Add modal functions
const openQuickAddModal = () => {
  showQuickAddModal.value = true
}

const closeQuickAddModal = () => {
  showQuickAddModal.value = false
}

const handleQuickAddSuccess = () => {
  closeQuickAddModal()
  // Reload dashboard data to reflect changes
  loadDashboardData()
}

// Load data on mount
onMounted(() => {
  loadDashboardData()
})
</script>