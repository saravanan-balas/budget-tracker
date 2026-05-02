<template>
  <div>
    <!-- Header with filters -->
    <div class="mb-6">
      <div class="flex flex-col sm:flex-row sm:justify-between sm:items-start gap-3 mb-4">
        <div>
          <h1 class="text-3xl font-bold text-gray-900">Transactions</h1>
          <p class="text-gray-600 mt-1">Manage and analyze your financial transactions</p>
        </div>

        <!-- Quick Actions -->
        <div class="flex gap-2 flex-shrink-0">
          <button @click="exportTransactions('csv')" class="bg-white border border-gray-300 text-gray-700 px-3 py-2 rounded-lg hover:bg-gray-50 flex items-center text-sm">
            <svg class="w-4 h-4 mr-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"></path>
            </svg>
            Export CSV
          </button>
          <button @click="openAddModal" class="bg-blue-600 text-white px-3 py-2 rounded-lg hover:bg-blue-700 flex items-center text-sm">
            <svg class="w-4 h-4 mr-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"></path>
            </svg>
            Add Transaction
          </button>
        </div>
      </div>

      <!-- Filter Bar -->
      <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-4">
        <div class="grid grid-cols-1 md:grid-cols-5 gap-4">
          <!-- Date Range -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Date Range</label>
            <select v-model="filters.dateRange" @change="handleDateRangeChange" class="w-full border-gray-300 rounded-lg">
              <option value="thisMonth">This Month</option>
              <option value="lastMonth">Last Month</option>
              <option value="last3Months">Last 3 Months</option>
              <option value="last6Months">Last 6 Months</option>
              <option value="thisYear">This Year</option>
              <option value="custom">Custom Range</option>
            </select>
          </div>

          <!-- Account Filter -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Account</label>
            <select v-model="filters.accountId" class="w-full border-gray-300 rounded-lg">
              <option value="">All Accounts</option>
              <option v-for="account in accounts" :key="account.id" :value="account.id">
                {{ account.name }}
              </option>
            </select>
          </div>

          <!-- Category Filter -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Category</label>
            <select v-model="filters.categoryId" class="w-full border-gray-300 rounded-lg">
              <option value="">All Categories</option>
              <option value="uncategorized">🔍 Uncategorized</option>
              <option disabled>──────────</option>
              <option v-for="category in categories" :key="category.id" :value="category.id">
                {{ category.name }}
              </option>
            </select>
          </div>

          <!-- Search -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Search</label>
            <input 
              v-model="filters.searchTerm" 
              type="text" 
              placeholder="Search merchants..."
              class="w-full border-gray-300 rounded-lg"
            />
          </div>

          <!-- Transaction Type -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Type</label>
            <select v-model="filters.transactionType" class="w-full border-gray-300 rounded-lg">
              <option value="">All Types</option>
              <option value="income">Income</option>
              <option value="expense">Expense</option>
              <option value="transfer">Transfer</option>
            </select>
          </div>
        </div>

        <!-- Custom Date Range (shown when custom is selected) -->
        <div v-if="filters.dateRange === 'custom'" class="mt-4 grid grid-cols-2 gap-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Start Date</label>
            <input 
              v-model="filters.startDate" 
              type="date" 
              class="w-full border-gray-300 rounded-lg"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">End Date</label>
            <input 
              v-model="filters.endDate" 
              type="date" 
              class="w-full border-gray-300 rounded-lg"
            />
          </div>
        </div>

        <!-- Filter Actions -->
        <div class="mt-4 flex flex-col sm:flex-row sm:justify-between sm:items-center gap-2">
          <div class="flex flex-wrap items-center gap-x-4 gap-y-1">
            <div class="text-sm text-gray-600">
              Showing {{ transactions.length }} of {{ totalTransactions }} (Page {{ currentPage }} of {{ totalPages }})
            </div>
            <div class="flex items-center gap-2">
              <label class="text-sm text-gray-600">Per page:</label>
              <select v-model.number="pageSize" @change="currentPage = 1" class="text-sm border-gray-300 rounded py-1 px-2">
                <option :value="20">20</option>
                <option :value="50">50</option>
                <option :value="100">100</option>
                <option :value="200">200</option>
              </select>
            </div>
          </div>
          <button @click="resetFilters" class="text-sm text-blue-600 hover:text-blue-800 self-start sm:self-auto">
            Reset Filters
          </button>
        </div>
      </div>
    </div>

    <!-- Main Content Grid -->
    <div class="flex flex-col lg:flex-row gap-6">
      <!-- Transactions Table -->
      <div class="flex-1 min-w-0 bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
        <div class="overflow-x-auto">
            <table class="min-w-full divide-y divide-gray-200 table-fixed transactions-table" style="min-width: 800px;">
            <thead class="bg-gray-50">
            <tr>              <th @click="sortBy('transactionDate')" class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100" style="width: 10%;">
                <div class="flex items-center">
                  Date
                  <svg v-if="sortField === 'transactionDate'" class="ml-1 w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path v-if="sortOrder === 'asc'" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 15l7-7 7 7"></path>
                    <path v-else stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path>
                  </svg>
                </div>
              </th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider" style="width: 35%;">
                Merchant & Description
              </th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider" style="width: 15%;">
                Category
              </th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider" style="width: 15%;">
                Account
              </th>
              <th @click="sortBy('amount')" class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100" style="width: 12%;">
                <div class="flex items-center justify-end">
                  Amount
                  <svg v-if="sortField === 'amount'" class="ml-1 w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path v-if="sortOrder === 'asc'" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 15l7-7 7 7"></path>
                    <path v-else stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path>
                  </svg>
                </div>
              </th>
              <th class="px-4 py-3 text-center text-xs font-medium text-gray-500 uppercase tracking-wider" style="width: 13%;">
                Actions
              </th>
            </tr>
          </thead>
          <tbody class="bg-white divide-y divide-gray-200">
            <tr v-for="transaction in paginatedTransactions" :key="transaction.id" class="hover:bg-gray-50">
              <td class="px-4 py-4 whitespace-nowrap text-sm text-gray-900" style="width: 10%;">
                {{ formatDate(transaction.transactionDate) }}
              </td>
              <td class="px-4 py-4 text-sm" style="width: 35%;">
                <div class="overflow-hidden">
                  <div class="font-medium text-gray-900 truncate" :title="transaction.merchant">{{ transaction.merchant }}</div>
                  <div v-if="transaction.description" class="text-gray-500 truncate text-xs mt-1" :title="transaction.description">
                    {{ transaction.description }}
                  </div>
                </div>
              </td>
              <td class="px-4 py-4 whitespace-nowrap" style="width: 15%;">
                <span v-if="transaction.categoryName" class="px-2 inline-flex text-xs leading-5 font-semibold rounded-full bg-green-100 text-green-800">
                  <span class="truncate max-w-20">{{ transaction.categoryName }}</span>
                </span>
                <span v-else class="text-sm text-gray-400">Uncategorized</span>
              </td>
              <td class="px-4 py-4 text-sm text-gray-500" style="width: 15%;">
                <span class="truncate block" :title="transaction.accountName">{{ transaction.accountName }}</span>
              </td>
              <td class="px-4 py-4 whitespace-nowrap text-sm text-right" style="width: 12%;">
                <span :class="transaction.amount > 0 ? 'text-green-600' : 'text-red-600'" class="font-medium">
                  {{ formatCurrency(Math.abs(transaction.amount)) }}
                </span>
              </td>
              <td class="px-4 py-4 whitespace-nowrap text-center text-sm font-medium" style="width: 13%;">
                <div class="flex justify-center space-x-2">
                  <button @click="editTransaction(transaction)" class="text-indigo-600 hover:text-indigo-900 text-sm px-2 py-1 rounded hover:bg-indigo-50">
                    Edit
                  </button>
                  <button @click="confirmDelete(transaction)" class="text-red-600 hover:text-red-900 text-sm px-2 py-1 rounded hover:bg-red-50">
                    Delete
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div class="bg-gray-50 px-4 py-3 flex items-center justify-between border-t border-gray-200 sm:px-6">
        <div class="flex-1 flex justify-between sm:hidden">
          <button 
            @click="currentPage--" 
            :disabled="currentPage === 1"
            class="relative inline-flex items-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 disabled:opacity-50"
          >
            Previous
          </button>
          <button 
            @click="currentPage++" 
            :disabled="currentPage === totalPages"
            class="ml-3 relative inline-flex items-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 disabled:opacity-50"
          >
            Next
          </button>
        </div>
        <div class="hidden sm:flex-1 sm:flex sm:items-center sm:justify-between">
          <div>
            <p class="text-sm text-gray-700">
              Showing
              <span class="font-medium">{{ Math.max(1, (currentPage - 1) * pageSize + 1) }}</span>
              to
              <span class="font-medium">{{ Math.min(currentPage * pageSize, totalTransactions) }}</span>
              of
              <span class="font-medium">{{ totalTransactions }}</span>
              results
            </p>
          </div>
          <div>
            <nav class="relative z-0 inline-flex rounded-md shadow-sm -space-x-px" aria-label="Pagination">
              <button 
                @click="currentPage--" 
                :disabled="currentPage === 1"
                class="relative inline-flex items-center px-2 py-2 rounded-l-md border border-gray-300 bg-white text-sm font-medium text-gray-500 hover:bg-gray-50 disabled:opacity-50"
              >
                <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"></path>
                </svg>
              </button>
              
              <button 
                v-for="page in displayedPages" 
                :key="page"
                @click="currentPage = page"
                :class="{'bg-indigo-50 border-indigo-500 text-indigo-600': page === currentPage}"
                class="relative inline-flex items-center px-4 py-2 border text-sm font-medium"
              >
                {{ page }}
              </button>
              
              <button 
                @click="currentPage++" 
                :disabled="currentPage === totalPages"
                class="relative inline-flex items-center px-2 py-2 rounded-r-md border border-gray-300 bg-white text-sm font-medium text-gray-500 hover:bg-gray-50 disabled:opacity-50"
              >
                <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"></path>
                </svg>
              </button>
            </nav>
          </div>
        </div>
      </div>
      </div>

      <!-- Charts Section (hidden on mobile) -->
      <div class="hidden lg:block w-80 space-y-4">
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-3">
          <h3 class="text-sm font-semibold mb-3">Spending by Category</h3>
          <div class="h-48">
            <canvas ref="categoryChart"></canvas>
          </div>
        </div>

        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-3">
          <h3 class="text-sm font-semibold mb-3">Daily Spending Trend</h3>
          <div class="h-48">
            <canvas ref="trendChart"></canvas>
          </div>
        </div>
      </div>
    </div>

    <!-- Add Transaction Modal -->
    <QuickAddModal 
      v-if="showAddModal" 
      @close="showAddModal = false"
      @success="handleAddSuccess"
    />

    <!-- Edit Transaction Modal -->
    <EditTransactionModal 
      v-if="showEditModal && selectedTransaction" 
      :transaction="selectedTransaction"
      @close="showEditModal = false"
      @success="handleEditSuccess"
    />

    <!-- Delete Confirmation Modal -->
    <div v-if="showDeleteModal" class="fixed inset-0 bg-gray-500 bg-opacity-75 flex items-center justify-center z-50">
      <div class="bg-white rounded-lg p-6 max-w-md w-full">
        <h3 class="text-lg font-medium text-gray-900 mb-4">Delete Transaction</h3>
        <p class="text-sm text-gray-500 mb-6">
          Are you sure you want to delete this transaction? This action cannot be undone.
        </p>
        <div class="flex justify-end space-x-3">
          <button 
            @click="showDeleteModal = false" 
            class="px-4 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            Cancel
          </button>
          <button 
            @click="deleteTransaction" 
            class="px-4 py-2 bg-red-600 text-white rounded-md text-sm font-medium hover:bg-red-700"
          >
            Delete
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from 'vue'
import { Chart, registerables } from 'chart.js'
import type { Transaction, Account, Category, TransactionFilter, PaginatedResponse } from '~/types'
import QuickAddModal from '~/components/QuickAddModal.vue'
import EditTransactionModal from '~/components/EditTransactionModal.vue'

Chart.register(...registerables)

// Reactive state
const transactions = ref<Transaction[]>([])
const chartTransactions = ref<Transaction[]>([]) // all data for charts (unpaginated)
const accounts = ref<Account[]>([])
const categories = ref<Category[]>([])
const loading = ref(false)
const showAddModal = ref(false)
const showEditModal = ref(false)
const showDeleteModal = ref(false)
const selectedTransaction = ref<Transaction | null>(null)
const totalTransactions = ref(0)

// Filters
const filters = ref({
  dateRange: 'thisMonth',
  startDate: '',
  endDate: '',
  accountId: '',
  categoryId: '',
  searchTerm: '',
  transactionType: ''
})

// Sorting
const sortField = ref<'transactionDate' | 'amount'>('transactionDate')
const sortOrder = ref<'asc' | 'desc'>('desc')

// Pagination
const currentPage = ref(1)
const pageSize = ref(50)

// Chart refs
const categoryChart = ref<HTMLCanvasElement | null>(null)
const trendChart = ref<HTMLCanvasElement | null>(null)
let categoryChartInstance: Chart | null = null
let trendChartInstance: Chart | null = null

// API
const api = useApi()

// Computed properties for client-side display
const filteredTransactions = computed(() => {
  let result = [...transactions.value]

  // Apply transaction type filter (client-side)
  if (filters.value.transactionType) {
    if (filters.value.transactionType === 'income') {
      result = result.filter(t => t.amount > 0)
    } else if (filters.value.transactionType === 'expense') {
      result = result.filter(t => t.amount < 0)
    } else if (filters.value.transactionType === 'transfer') {
      result = result.filter(t => t.isTransfer)
    }
  }

  // Sort
  result.sort((a, b) => {
    const aVal = sortField.value === 'amount' ? a.amount : new Date(a.transactionDate).getTime()
    const bVal = sortField.value === 'amount' ? b.amount : new Date(b.transactionDate).getTime()
    
    if (sortOrder.value === 'asc') {
      return aVal < bVal ? -1 : aVal > bVal ? 1 : 0
    } else {
      return aVal > bVal ? -1 : aVal < bVal ? 1 : 0
    }
  })

  return result
})

const paginatedTransactions = computed(() => {
  // Since we're using server-side pagination, just return filtered transactions
  return filteredTransactions.value
})

const totalPages = computed(() => {
  return Math.max(1, Math.ceil(totalTransactions.value / pageSize.value))
})

const displayedPages = computed(() => {
  const pages = []
  const maxPages = 5
  let start = Math.max(1, currentPage.value - Math.floor(maxPages / 2))
  let end = Math.min(totalPages.value, start + maxPages - 1)
  
  if (end - start < maxPages - 1) {
    start = Math.max(1, end - maxPages + 1)
  }
  
  for (let i = start; i <= end; i++) {
    pages.push(i)
  }
  
  return pages
})

// Methods
const loadTransactions = async (resetPage = false) => {
  loading.value = true
  try {
    if (resetPage) {
      currentPage.value = 1
    }
    
    const filter: TransactionFilter = {}
    
    // Set date filters based on selected range
    const now = new Date()
    const currentMonth = now.getMonth()
    const currentYear = now.getFullYear()
    
    if (filters.value.dateRange === 'thisMonth') {
      filter.startDate = new Date(currentYear, currentMonth, 1).toISOString().split('T')[0]
      filter.endDate = new Date(currentYear, currentMonth + 1, 0).toISOString().split('T')[0]
    } else if (filters.value.dateRange === 'lastMonth') {
      filter.startDate = new Date(currentYear, currentMonth - 1, 1).toISOString().split('T')[0]
      filter.endDate = new Date(currentYear, currentMonth, 0).toISOString().split('T')[0]
    } else if (filters.value.dateRange === 'last3Months') {
      filter.startDate = new Date(currentYear, currentMonth - 2, 1).toISOString().split('T')[0]
      filter.endDate = new Date(currentYear, currentMonth + 1, 0).toISOString().split('T')[0]
    } else if (filters.value.dateRange === 'last6Months') {
      filter.startDate = new Date(currentYear, currentMonth - 5, 1).toISOString().split('T')[0]
      filter.endDate = new Date(currentYear, currentMonth + 1, 0).toISOString().split('T')[0]
    } else if (filters.value.dateRange === 'thisYear') {
      filter.startDate = new Date(currentYear, 0, 1).toISOString().split('T')[0]
      filter.endDate = new Date(currentYear, 11, 31).toISOString().split('T')[0]
    } else if (filters.value.dateRange === 'custom') {
      filter.startDate = filters.value.startDate
      filter.endDate = filters.value.endDate
    }
    
    // Add filters from UI
    if (filters.value.accountId) {
      filter.accountId = filters.value.accountId
    }
    
    if (filters.value.categoryId === 'uncategorized') {
      filter.categoryIdString = 'uncategorized'
    } else if (filters.value.categoryId) {
      filter.categoryId = filters.value.categoryId
    }
    
    if (filters.value.searchTerm) {
      filter.searchTerm = filters.value.searchTerm
    }
    
    // Add pagination
    filter.page = currentPage.value
    filter.pageSize = pageSize.value
    
    // Fetch paginated data from API
    const paginatedResponse = await api.getTransactions(filter)
    
    // Update state with paginated response
    transactions.value = paginatedResponse.items
    totalTransactions.value = paginatedResponse.totalCount
    
    // Update pagination if page is out of bounds
    if (paginatedResponse.totalPages > 0 && currentPage.value > paginatedResponse.totalPages) {
      currentPage.value = paginatedResponse.totalPages
      await loadTransactions(false)
      return
    }
    
    // Update date filters for display
    if (filter.startDate) filters.value.startDate = filter.startDate
    if (filter.endDate) filters.value.endDate = filter.endDate
  } catch (error) {
    console.error('Error loading transactions:', error)
  } finally {
    loading.value = false
  }
}

const loadChartTransactions = async () => {
  try {
    const filter: TransactionFilter = {}
    const now = new Date()
    const currentMonth = now.getMonth()
    const currentYear = now.getFullYear()

    if (filters.value.dateRange === 'thisMonth') {
      filter.startDate = new Date(currentYear, currentMonth, 1).toISOString().split('T')[0]
      filter.endDate = new Date(currentYear, currentMonth + 1, 0).toISOString().split('T')[0]
    } else if (filters.value.dateRange === 'lastMonth') {
      filter.startDate = new Date(currentYear, currentMonth - 1, 1).toISOString().split('T')[0]
      filter.endDate = new Date(currentYear, currentMonth, 0).toISOString().split('T')[0]
    } else if (filters.value.dateRange === 'last3Months') {
      filter.startDate = new Date(currentYear, currentMonth - 2, 1).toISOString().split('T')[0]
      filter.endDate = new Date(currentYear, currentMonth + 1, 0).toISOString().split('T')[0]
    } else if (filters.value.dateRange === 'last6Months') {
      filter.startDate = new Date(currentYear, currentMonth - 5, 1).toISOString().split('T')[0]
      filter.endDate = new Date(currentYear, currentMonth + 1, 0).toISOString().split('T')[0]
    } else if (filters.value.dateRange === 'thisYear') {
      filter.startDate = new Date(currentYear, 0, 1).toISOString().split('T')[0]
      filter.endDate = new Date(currentYear, 11, 31).toISOString().split('T')[0]
    } else if (filters.value.dateRange === 'custom') {
      filter.startDate = filters.value.startDate
      filter.endDate = filters.value.endDate
    }

    if (filters.value.accountId) filter.accountId = filters.value.accountId
    if (filters.value.categoryId === 'uncategorized') {
      filter.categoryIdString = 'uncategorized'
    } else if (filters.value.categoryId) {
      filter.categoryId = filters.value.categoryId
    }
    if (filters.value.searchTerm) filter.searchTerm = filters.value.searchTerm

    filter.page = 1
    filter.pageSize = 5000 // fetch all for charts

    const response = await api.getTransactions(filter)
    chartTransactions.value = response.items
  } catch (error) {
    console.error('Error loading chart transactions:', error)
  }
}

const loadAccounts = async () => {
  try {
    accounts.value = await api.getAccounts()
  } catch (error) {
    console.error('Error loading accounts:', error)
  }
}

const loadCategories = async () => {
  try {
    categories.value = await api.getCategories()
  } catch (error) {
    console.error('Error loading categories:', error)
  }
}

const handleDateRangeChange = () => {
  if (filters.value.dateRange !== 'custom') {
    loadTransactions(true)
    loadChartTransactions()
  }
}

const sortBy = (field: 'transactionDate' | 'amount') => {
  if (sortField.value === field) {
    sortOrder.value = sortOrder.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortField.value = field
    sortOrder.value = 'desc'
  }
}

const resetFilters = () => {
  filters.value = {
    dateRange: 'thisMonth',
    startDate: '',
    endDate: '',
    accountId: '',
    categoryId: '',
    searchTerm: '',
    transactionType: ''
  }
  currentPage.value = 1
  loadTransactions(true)
}

const formatDate = (date: string) => {
  return new Date(date).toLocaleDateString('en-US', { 
    year: 'numeric', 
    month: 'short', 
    day: 'numeric' 
  })
}

const formatCurrency = (amount: number) => {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD'
  }).format(amount)
}

const openAddModal = () => {
  showAddModal.value = true
}

const editTransaction = (transaction: Transaction) => {
  selectedTransaction.value = transaction
  showEditModal.value = true
}

const handleAddSuccess = () => {
  showAddModal.value = false
  loadTransactions()
}

const handleEditSuccess = () => {
  showEditModal.value = false
  selectedTransaction.value = null
  loadTransactions()
}

const confirmDelete = (transaction: Transaction) => {
  selectedTransaction.value = transaction
  showDeleteModal.value = true
}

const deleteTransaction = async () => {
  if (!selectedTransaction.value) return
  
  try {
    await api.deleteTransaction(selectedTransaction.value.id)
    showDeleteModal.value = false
    loadTransactions()
  } catch (error) {
    console.error('Error deleting transaction:', error)
  }
}

const exportTransactions = (format: 'csv' | 'pdf') => {
  // Generate CSV
  if (format === 'csv') {
    const headers = ['Date', 'Merchant', 'Category', 'Account', 'Amount', 'Description']
    const rows = filteredTransactions.value.map(t => [
      formatDate(t.transactionDate),
      t.merchant,
      t.categoryName || 'Uncategorized',
      t.accountName,
      t.amount.toString(),
      t.description || ''
    ])
    
    const csv = [
      headers.join(','),
      ...rows.map(row => row.map(cell => `"${cell}"`).join(','))
    ].join('\n')
    
    const blob = new Blob([csv], { type: 'text/csv' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `transactions-${new Date().toISOString().split('T')[0]}.csv`
    a.click()
    URL.revokeObjectURL(url)
  }
}

const updateCharts = () => {
  nextTick(() => {
    // Category spending chart
    if (categoryChart.value) {
      const categoryData = chartTransactions.value
        .filter(t => t.amount < 0) // Only expenses
        .reduce((acc, t) => {
          const category = t.categoryName || 'Uncategorized'
          acc[category] = (acc[category] || 0) + Math.abs(t.amount)
          return acc
        }, {} as Record<string, number>)
      
      const sortedCategories = Object.entries(categoryData)
        .sort((a, b) => b[1] - a[1])
        .slice(0, 8) // Top 8 categories for smaller chart
      
      if (categoryChartInstance) {
        categoryChartInstance.destroy()
      }
      
      categoryChartInstance = new Chart(categoryChart.value, {
        type: 'doughnut',
        data: {
          labels: sortedCategories.map(([name]) => name),
          datasets: [{
            data: sortedCategories.map(([, amount]) => amount),
            backgroundColor: [
              '#3B82F6', '#10B981', '#F59E0B', '#EF4444', 
              '#8B5CF6', '#EC4899', '#06B6D4', '#84CC16'
            ]
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: {
              position: 'bottom',
              labels: {
                boxWidth: 12,
                font: {
                  size: 10
                }
              }
            },
            tooltip: {
              callbacks: {
                label: (context) => {
                  const label = context.label || ''
                  const value = formatCurrency(context.parsed)
                  return `${label}: ${value}`
                }
              }
            }
          }
        }
      })
    }
    
    // Daily trend chart - always show full month
    if (trendChart.value) {
      // Get current month or filtered month range
      const now = new Date()
      let startDate: Date
      let endDate: Date
      
      if (filters.value.dateRange === 'thisMonth') {
        startDate = new Date(now.getFullYear(), now.getMonth(), 1)
        endDate = new Date(now.getFullYear(), now.getMonth() + 1, 0)
      } else if (filters.value.startDate && filters.value.endDate) {
        startDate = new Date(filters.value.startDate)
        endDate = new Date(filters.value.endDate)
      } else {
        // Default to current month
        startDate = new Date(now.getFullYear(), now.getMonth(), 1)
        endDate = new Date(now.getFullYear(), now.getMonth() + 1, 0)
      }
      
      // Create array of all days in the range
      const allDays: string[] = []
      const currentDate = new Date(startDate)
      while (currentDate <= endDate) {
        allDays.push(currentDate.toISOString().split('T')[0])
        currentDate.setDate(currentDate.getDate() + 1)
      }
      
      // Aggregate transaction data by date
      const dailyData = chartTransactions.value.reduce((acc, t) => {
        const date = t.transactionDate.split('T')[0]
        if (!acc[date]) {
          acc[date] = { income: 0, expense: 0 }
        }
        if (t.amount > 0) {
          acc[date].income += t.amount
        } else {
          acc[date].expense += Math.abs(t.amount)
        }
        return acc
      }, {} as Record<string, { income: number; expense: number }>)
      
      // Fill in missing days with 0 values
      const completeData = allDays.map(date => ({
        date,
        income: dailyData[date]?.income || 0,
        expense: dailyData[date]?.expense || 0
      }))
      
      if (trendChartInstance) {
        trendChartInstance.destroy()
      }
      
      trendChartInstance = new Chart(trendChart.value, {
        type: 'line',
        data: {
          labels: completeData.map(d => new Date(d.date).getDate().toString()),
          datasets: [
            {
              label: 'Income',
              data: completeData.map(d => d.income),
              borderColor: '#10B981',
              backgroundColor: 'rgba(16, 185, 129, 0.1)',
              tension: 0.1,
              pointRadius: 2
            },
            {
              label: 'Expenses',
              data: completeData.map(d => d.expense),
              borderColor: '#EF4444',
              backgroundColor: 'rgba(239, 68, 68, 0.1)',
              tension: 0.1,
              pointRadius: 2
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          interaction: {
            intersect: false,
            mode: 'index'
          },
          plugins: {
            legend: {
              display: true,
              labels: {
                boxWidth: 12,
                font: {
                  size: 10
                }
              }
            },
            tooltip: {
              callbacks: {
                title: (context) => {
                  const dayIndex = context[0].dataIndex
                  const date = allDays[dayIndex]
                  return formatDate(date)
                },
                label: (context) => {
                  const label = context.dataset.label || ''
                  const value = formatCurrency(context.parsed.y)
                  return `${label}: ${value}`
                }
              }
            }
          },
          scales: {
            x: {
              ticks: {
                maxTicksLimit: 10,
                font: {
                  size: 10
                }
              }
            },
            y: {
              beginAtZero: true,
              ticks: {
                callback: (value) => {
                  const num = value as number
                  return num >= 1000 ? `$${(num/1000).toFixed(0)}k` : `$${num}`
                },
                font: {
                  size: 10
                }
              }
            }
          }
        }
      })
    }
  })
}

// Watch for transactions changes to update charts
watch(chartTransactions, () => {
  updateCharts()
})

// Watch for page changes
watch(currentPage, () => {
  loadTransactions()
})

// Watch for filter changes that require reloading
watch([() => filters.value.accountId, () => filters.value.categoryId, () => filters.value.searchTerm], () => {
  loadTransactions(true)
  loadChartTransactions()
})

// Watch for transaction type changes (client-side filter)
watch(() => filters.value.transactionType, () => {
  // Transaction type is handled client-side, just update display
  updateCharts()
})

// Watch for custom date changes
watch([() => filters.value.startDate, () => filters.value.endDate], () => {
  if (filters.value.dateRange === 'custom' && filters.value.startDate && filters.value.endDate) {
    loadTransactions(true)
    loadChartTransactions()
  }
})

// Watch for pageSize changes
watch(pageSize, () => {
  loadTransactions(true)
})

// Initialize
onMounted(() => {
  // Check for URL parameters to preset filters
  const route = useRoute()
  if (route.query.filter === 'uncategorized') {
    filters.value.categoryId = 'uncategorized'
  }
  if (route.query.type === 'expense') {
    filters.value.transactionType = 'expense'
  } else if (route.query.type === 'income') {
    filters.value.transactionType = 'income'
  }
  const validDateRanges = ['thisMonth', 'lastMonth', 'last3Months', 'last6Months', 'thisYear', 'custom']
  if (route.query.dateRange && validDateRanges.includes(route.query.dateRange as string)) {
    filters.value.dateRange = route.query.dateRange as string
  }
  
  loadTransactions()
  loadChartTransactions()
  loadAccounts()
  loadCategories()
})

// Set page meta
useHead({
  title: 'Transactions - BudgetVu'
})
</script>

<style scoped>
input[type="date"]::-webkit-calendar-picker-indicator {
  cursor: pointer;
}

/* Ensure table cells don't expand beyond their assigned widths */
.transactions-table {
  table-layout: fixed;
  width: 100%;
}

.transactions-table th,
.transactions-table td {
  overflow: hidden;
  text-overflow: ellipsis;
}

/* Handle long text in merchant description */
.merchant-description {
  word-wrap: break-word;
  overflow-wrap: break-word;
}

/* Ensure category badges don't overflow */
.category-badge {
  max-width: 100%;
}
</style>