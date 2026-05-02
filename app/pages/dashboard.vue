<template>
  <div>
    <div class="mb-8">
      <h1 class="text-3xl font-bold text-gray-900">Financial Dashboard</h1>
      <p class="text-gray-600 mt-2">Your personal finance overview</p>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mr-4"></div>
      <span>Loading your financial data...</span>
    </div>

    <div v-else>
      <!-- Quick Stats -->
      <div class="flex items-center gap-2 mb-3">
        <span class="text-sm text-gray-500">Year to date:</span>
        <span class="text-sm font-semibold text-gray-700">Jan 1 – {{ formatShortDate(new Date()) }}</span>
      </div>
      <div class="grid grid-cols-1 md:grid-cols-5 gap-6 mb-8">
        <NuxtLink to="/transactions?type=expense&dateRange=thisYear" class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 hover:shadow-md transition-shadow group">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-sm font-medium text-gray-500">Expenses</p>
              <p class="text-2xl font-bold text-gray-900">{{ formatCurrency(stats.monthlyExpenses) }}</p>
            </div>
            <div class="w-10 h-10 bg-red-100 rounded-lg flex items-center justify-center group-hover:scale-110 transition-transform">
              <span class="text-red-600">📉</span>
            </div>
          </div>
        </NuxtLink>

        <NuxtLink to="/transactions?type=income&dateRange=thisYear" class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 hover:shadow-md transition-shadow group">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-sm font-medium text-gray-500">Income</p>
              <p class="text-2xl font-bold text-green-600">{{ formatCurrency(stats.monthlyIncome) }}</p>
            </div>
            <div class="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center group-hover:scale-110 transition-transform">
              <span class="text-green-600">📈</span>
            </div>
          </div>
        </NuxtLink>

        <NuxtLink to="/transactions?dateRange=thisYear" class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 hover:shadow-md transition-shadow group">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-sm font-medium text-gray-500">Net Savings</p>
              <p class="text-2xl font-bold" :class="stats.netSavings >= 0 ? 'text-blue-600' : 'text-red-600'">
                {{ formatCurrency(Math.abs(stats.netSavings)) }}
              </p>
            </div>
            <div class="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center group-hover:scale-110 transition-transform">
              <span class="text-blue-600">💰</span>
            </div>
          </div>
        </NuxtLink>

        <NuxtLink to="/transactions?dateRange=thisYear" class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 hover:shadow-md transition-shadow group">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-sm font-medium text-gray-500">Transactions</p>
              <p class="text-2xl font-bold text-gray-900">{{ stats.transactionCount }}</p>
            </div>
            <div class="w-10 h-10 bg-purple-100 rounded-lg flex items-center justify-center group-hover:scale-110 transition-transform">
              <span class="text-purple-600">📋</span>
            </div>
          </div>
        </NuxtLink>

        <NuxtLink to="/transactions?uncategorizedOnly=true&dateRange=thisYear" class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 hover:shadow-md transition-shadow cursor-pointer group">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-sm font-medium text-gray-500">Uncategorized</p>
              <p class="text-2xl font-bold text-gray-900">{{ stats.uncategorizedCount }}</p>
              <p class="text-xs text-amber-600 mt-1">Need attention</p>
            </div>
            <div class="w-10 h-10 bg-amber-100 rounded-lg flex items-center justify-center group-hover:scale-110 transition-transform">
              <span class="text-amber-600">🔍</span>
            </div>
          </div>
        </NuxtLink>
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
                <div class="flex items-center space-x-3 flex-1 min-w-0">
                  <div class="w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0" :class="getTransactionIconBg(transaction.amount)">
                    <span class="text-xs font-bold" :class="getTransactionIconText(transaction.amount)">
                      {{ getTransactionIcon(transaction.categoryName) }}
                    </span>
                  </div>
                  <div class="flex-1 min-w-0">
                    <p class="font-medium text-gray-900 truncate">{{ transaction.normalizedMerchant || transaction.merchant }}</p>
                    <p class="text-sm text-gray-500 truncate">{{ transaction.categoryName || 'Uncategorized' }}</p>
                  </div>
                </div>
                <div class="text-right flex-shrink-0 ml-3">
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
            <select v-model="categoryChartPeriod" @change="handleChartPeriodChange" class="text-sm border rounded-md px-2 py-1">
              <option value="thisMonth">This month</option>
              <option value="lastMonth">Last month</option>
              <option value="last3Months">Last 3 months</option>
              <option value="last6Months">Last 6 months</option>
              <option value="thisYear">This year</option>
              <option value="custom">Custom date</option>
            </select>
          </div>
          <div v-if="categoryChartPeriod === 'custom'" class="flex gap-2 mb-4">
            <div class="flex-1">
              <label class="block text-xs text-gray-500 mb-1">From</label>
              <input v-model="chartCustomStart" type="date" @change="loadCategoryChartData" class="w-full text-sm border-gray-300 rounded-lg" />
            </div>
            <div class="flex-1">
              <label class="block text-xs text-gray-500 mb-1">To</label>
              <input v-model="chartCustomEnd" type="date" @change="loadCategoryChartData" class="w-full text-sm border-gray-300 rounded-lg" />
            </div>
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
            <p class="text-green-800">Great job! You have {{ formatCurrency(stats.netSavings) }} in net savings this year.</p>
          </div>
          <div v-else class="bg-yellow-50 p-4 rounded-lg border-l-4 border-yellow-400">
            <h4 class="font-semibold text-yellow-900 mb-2">Budget Alert</h4>
            <p class="text-yellow-800">Your expenses exceed income this year. Consider reviewing your spending.</p>
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

  </div>
</template>

<script setup lang="ts">
import type { Transaction } from '~/types'

definePageMeta({
  middleware: 'auth'
})

// State
const loading = ref(true)
const categoryChartPeriod = ref('thisMonth')
const chartCustomStart = ref('')
const chartCustomEnd = ref('')
const stats = reactive({
  monthlyExpenses: 0,
  monthlyIncome: 0,
  netSavings: 0,
  transactionCount: 0,
  uncategorizedCount: 0
})
const recentTransactions = ref<Transaction[]>([])
const monthlyTransactions = ref<Transaction[]>([])

// Modal states
const showEditModal = ref(false)
const selectedTransaction = ref<Transaction | null>(null)

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

// Load category chart data based on selected period
const loadCategoryChartData = async () => {
  try {
    const now = new Date()
    const y = now.getFullYear()
    const m = now.getMonth()
    let startDate: string
    let endDate: string

    if (categoryChartPeriod.value === 'thisMonth') {
      startDate = new Date(y, m, 1).toISOString().split('T')[0]
      endDate = new Date(y, m + 1, 0).toISOString().split('T')[0]
    } else if (categoryChartPeriod.value === 'lastMonth') {
      startDate = new Date(y, m - 1, 1).toISOString().split('T')[0]
      endDate = new Date(y, m, 0).toISOString().split('T')[0]
    } else if (categoryChartPeriod.value === 'last3Months') {
      startDate = new Date(y, m - 2, 1).toISOString().split('T')[0]
      endDate = new Date(y, m + 1, 0).toISOString().split('T')[0]
    } else if (categoryChartPeriod.value === 'last6Months') {
      startDate = new Date(y, m - 5, 1).toISOString().split('T')[0]
      endDate = new Date(y, m + 1, 0).toISOString().split('T')[0]
    } else if (categoryChartPeriod.value === 'thisYear') {
      startDate = new Date(y, 0, 1).toISOString().split('T')[0]
      endDate = new Date(y, 11, 31).toISOString().split('T')[0]
    } else if (categoryChartPeriod.value === 'custom') {
      if (!chartCustomStart.value || !chartCustomEnd.value) return
      startDate = chartCustomStart.value
      endDate = chartCustomEnd.value
    } else {
      return
    }

    const response = await api.getTransactions({
      startDate,
      endDate,
      pageSize: 1000
    })
    monthlyTransactions.value = response.items
  } catch (error) {
    console.error('Error loading category chart data:', error)
  }
}

const handleChartPeriodChange = () => {
  if (categoryChartPeriod.value !== 'custom') {
    loadCategoryChartData()
  }
}

// Load dashboard data
const loadDashboardData = async () => {
  try {
    console.log('Loading real dashboard data...')
    loading.value = true
    
    // Load recent transactions (last 5)
    const recentTransactionsResponse = await api.getTransactions({
      pageSize: 5,
      page: 1
    })
    recentTransactions.value = recentTransactionsResponse.items
    
    // Get year-to-date range (Jan 1 to today)
    const now = new Date()
    const startOfYear = new Date(now.getFullYear(), 0, 1)
    const ytdStart = startOfYear.toISOString()
    const ytdEnd = now.toISOString()

    // Fetch YTD transactions (large page to cover full year for financial totals)
    const ytdTransactionsResponse = await api.getTransactions({
      startDate: ytdStart,
      endDate: ytdEnd,
      pageSize: 5000
    })

    monthlyTransactions.value = ytdTransactionsResponse.items

    // Load category chart data (will use the selected period)
    await loadCategoryChartData()

    // Calculate stats — uncategorized counted client-side matching what the UI shows (no categoryName)
    const ytdItems = ytdTransactionsResponse.items
    const expenses = ytdItems.filter(t => t.amount < 0).reduce((sum, t) => sum + Math.abs(t.amount), 0)
    const income = ytdItems.filter(t => t.amount > 0).reduce((sum, t) => sum + t.amount, 0)

    stats.monthlyExpenses = expenses
    stats.monthlyIncome = income
    stats.netSavings = income - expenses
    stats.transactionCount = ytdTransactionsResponse.totalCount
    stats.uncategorizedCount = ytdItems.filter(t => !t.categoryName).length
    
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

const formatShortDate = (date: Date) => {
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
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

// Load data on mount
onMounted(() => {
  loadDashboardData()
})
</script>