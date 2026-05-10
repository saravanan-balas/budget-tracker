<template>
  <div>
    <div class="mb-8 flex justify-between items-start">
      <div>
        <h1 class="text-3xl font-bold text-gray-900">Ask AI: Monthly Insights</h1>
        <p class="text-gray-600 mt-2">See where you’re spending the most, and get AI recommendations.</p>
      </div>

      <div class="flex items-end gap-3">
        <div>
          <label class="block text-xs font-medium text-gray-600 mb-1">Year</label>
          <select v-model.number="selectedYear" class="border rounded-md px-3 py-2 text-sm bg-white">
            <option v-for="y in yearOptions" :key="y" :value="y">{{ y }}</option>
          </select>
        </div>
        <div>
          <label class="block text-xs font-medium text-gray-600 mb-1">Month</label>
          <select v-model.number="selectedMonth" class="border rounded-md px-3 py-2 text-sm bg-white">
            <option v-for="m in monthOptions" :key="m.value" :value="m.value">{{ m.label }}</option>
          </select>
        </div>
        <button
          @click="loadInsights"
          :disabled="loading"
          class="bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 disabled:opacity-50"
        >
          <span v-if="loading">Generating…</span>
          <span v-else>Generate Insights</span>
        </button>
      </div>
    </div>

    <div v-if="error" class="mb-6 bg-red-50 border border-red-200 text-red-800 rounded-md p-4">
      {{ error }}
    </div>

    <div v-if="loading && !insights" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mr-4"></div>
      <span>Analyzing your monthly transactions…</span>
    </div>

    <div v-else-if="insights">
      <!-- Summary cards -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-6 mb-8">
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <p class="text-sm font-medium text-gray-500">Income</p>
          <p class="text-2xl font-bold text-green-600">{{ formatCurrency(insights.totalIncome) }}</p>
        </div>
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <p class="text-sm font-medium text-gray-500">Expenses</p>
          <p class="text-2xl font-bold text-gray-900">{{ formatCurrency(insights.totalExpenses) }}</p>
        </div>
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <p class="text-sm font-medium text-gray-500">Net</p>
          <p class="text-2xl font-bold" :class="insights.net >= 0 ? 'text-blue-600' : 'text-red-600'">
            {{ formatCurrency(insights.net) }}
          </p>
        </div>
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <p class="text-sm font-medium text-gray-500">Transactions</p>
          <p class="text-2xl font-bold text-gray-900">{{ insights.transactionCount }}</p>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- Category chart -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <div class="flex items-center justify-between mb-4">
            <div class="flex items-center gap-2">
              <h2 class="text-lg font-semibold">Spending by Category</h2>
              <button
                @click="chartFullscreen = true"
                class="p-1 rounded text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-colors"
                title="Full screen"
              >
                <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M4 8V4h4M20 8V4h-4M4 16v4h4M20 16v4h-4" />
                </svg>
              </button>
            </div>
            <div class="text-sm text-gray-500">
              {{ formatMonthLabel(insights.periodStartUtc) }}
            </div>
          </div>
          <div class="h-72">
            <CategoryChart
              v-if="categoryChartData.length > 0"
              :data="categoryChartData"
            />
            <div v-else class="flex items-center justify-center h-full text-gray-500">
              No expense transactions found for this month.
            </div>
          </div>
        </div>

        <!-- Chart Fullscreen Modal -->
        <ChartFullscreenModal
          v-if="chartFullscreen"
          title="Spending by Category"
          @close="chartFullscreen = false"
        >
          <CategoryChart v-if="categoryChartData.length > 0" :data="categoryChartData" />
          <div v-else class="flex items-center justify-center h-full text-gray-500">No expense transactions found for this month.</div>
        </ChartFullscreenModal>

        <!-- AI insights -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold">AI Insights</h2>
            <span
              class="text-xs px-2 py-1 rounded-full"
              :class="insights.ai.usedAi ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-700'"
            >
              {{ insights.ai.usedAi ? 'AI' : 'Basic' }}
            </span>
          </div>

          <p class="text-gray-800 mb-4">{{ insights.ai.summary }}</p>

          <div v-if="insights.ai.highlights?.length" class="mb-4">
            <h3 class="text-sm font-semibold text-gray-900 mb-2">Highlights</h3>
            <ul class="list-disc pl-5 text-sm text-gray-700 space-y-1">
              <li v-for="(h, i) in insights.ai.highlights" :key="i">{{ h }}</li>
            </ul>
          </div>

          <div v-if="insights.ai.suggestions?.length" class="mb-4">
            <h3 class="text-sm font-semibold text-gray-900 mb-2">Suggestions</h3>
            <ul class="list-disc pl-5 text-sm text-gray-700 space-y-1">
              <li v-for="(s, i) in insights.ai.suggestions" :key="i">{{ s }}</li>
            </ul>
          </div>

          <div v-if="insights.ai.watchouts?.length">
            <h3 class="text-sm font-semibold text-gray-900 mb-2">Watchouts</h3>
            <ul class="list-disc pl-5 text-sm text-gray-700 space-y-1">
              <li v-for="(w, i) in insights.ai.watchouts" :key="i">{{ w }}</li>
            </ul>
          </div>
        </div>
      </div>

      <!-- Top merchants -->
      <div class="mt-8 bg-white rounded-lg shadow-sm border border-gray-200 p-6">
        <h2 class="text-lg font-semibold mb-4">Top Merchants</h2>
        <div v-if="insights.topMerchants.length === 0" class="text-gray-500">
          No merchant spending found for this month.
        </div>
        <div v-else class="overflow-x-auto">
          <table class="min-w-full divide-y divide-gray-200">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Merchant</th>
                <th class="px-4 py-2 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Spent</th>
                <th class="px-4 py-2 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Count</th>
              </tr>
            </thead>
            <tbody class="bg-white divide-y divide-gray-200">
              <tr v-for="m in insights.topMerchants" :key="m.merchant">
                <td class="px-4 py-2 text-sm text-gray-900">{{ m.merchant }}</td>
                <td class="px-4 py-2 text-sm text-right text-gray-900">{{ formatCurrency(m.amount) }}</td>
                <td class="px-4 py-2 text-sm text-right text-gray-700">{{ m.count }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Sample transactions -->
      <div class="mt-8 bg-white rounded-lg shadow-sm border border-gray-200 p-6">
        <h2 class="text-lg font-semibold mb-4">Sample Transactions (for insights)</h2>
        <div v-if="insights.sampleTransactions.length === 0" class="text-gray-500">
          No transactions found for this month.
        </div>
        <div v-else class="overflow-x-auto">
          <table class="min-w-full divide-y divide-gray-200">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Date</th>
                <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Merchant</th>
                <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Category</th>
                <th class="px-4 py-2 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
              </tr>
            </thead>
            <tbody class="bg-white divide-y divide-gray-200">
              <tr v-for="t in insights.sampleTransactions" :key="t.transactionDateUtc + t.merchant + t.amount">
                <td class="px-4 py-2 text-sm text-gray-900">{{ formatDate(t.transactionDateUtc) }}</td>
                <td class="px-4 py-2 text-sm text-gray-900">{{ t.merchant }}</td>
                <td class="px-4 py-2 text-sm text-gray-700">{{ t.category }}</td>
                <td class="px-4 py-2 text-sm text-right font-medium" :class="t.amount >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ formatSignedCurrency(t.amount) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import CategoryChart from '~/components/CategoryChart.vue'
import type { MonthlyInsightsResponse } from '~/types'

definePageMeta({
  middleware: 'auth'
})

const api = useApi()

const loading = ref(false)
const error = ref<string | null>(null)
const insights = ref<MonthlyInsightsResponse | null>(null)
const chartFullscreen = ref(false)

const now = new Date()
const selectedYear = ref(now.getFullYear())
const selectedMonth = ref(now.getMonth() + 1) // 1-12

const yearOptions = computed(() => {
  // Provide a reasonable window that includes 2025 (and a few years back/forward)
  const current = now.getFullYear()
  const years: number[] = []
  for (let y = current + 1; y >= current - 6; y--) years.push(y)
  return years
})

const monthOptions = [
  { value: 1, label: 'January' },
  { value: 2, label: 'February' },
  { value: 3, label: 'March' },
  { value: 4, label: 'April' },
  { value: 5, label: 'May' },
  { value: 6, label: 'June' },
  { value: 7, label: 'July' },
  { value: 8, label: 'August' },
  { value: 9, label: 'September' },
  { value: 10, label: 'October' },
  { value: 11, label: 'November' },
  { value: 12, label: 'December' }
]

const categoryChartData = computed(() => {
  if (!insights.value) return []
  return insights.value.spendingByCategory.map(c => ({
    name: c.label,
    amount: c.value
  }))
})

const loadInsights = async () => {
  error.value = null
  loading.value = true
  try {
    insights.value = await api.getMonthlyInsights({
      year: selectedYear.value,
      month: selectedMonth.value,
      sampleSize: 10
    })
  } catch (e: any) {
    console.error(e)
    error.value = e?.data?.error || e?.message || 'Failed to load insights'
  } finally {
    loading.value = false
  }
}

const formatCurrency = (amount: number) =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount)

const formatSignedCurrency = (amount: number) => {
  const formatted = formatCurrency(Math.abs(amount))
  return amount >= 0 ? `+${formatted}` : `-${formatted}`
}

const formatDate = (iso: string) =>
  new Date(iso).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC' })

const formatMonthLabel = (iso: string) =>
  new Date(iso).toLocaleDateString('en-US', { month: 'long', year: 'numeric', timeZone: 'UTC' })

onMounted(() => {
  loadInsights()
})
</script>

