<template>
  <div>
    <div class="mb-8 flex justify-between items-start">
      <div>
        <h1 class="text-3xl font-bold text-gray-900">Application Logs</h1>
        <p class="text-gray-600 mt-2">View and filter application logs</p>
      </div>
      <button
        @click="loadLogs"
        class="bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 transition-colors"
        :disabled="loading"
      >
        {{ loading ? 'Loading...' : 'Refresh' }}
      </button>
    </div>

    <!-- Filters -->
    <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 mb-6">
      <h2 class="text-lg font-semibold mb-4">Filters</h2>
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-2">Level</label>
          <select
            v-model="filters.level"
            @change="loadLogs"
            class="w-full border rounded-md px-3 py-2"
          >
            <option value="">All Levels</option>
            <option v-for="level in logLevels" :key="level" :value="level">
              {{ level }}
            </option>
          </select>
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-2">Source</label>
          <select
            v-model="filters.source"
            @change="loadLogs"
            class="w-full border rounded-md px-3 py-2"
          >
            <option value="">All Sources</option>
            <option v-for="source in sources" :key="source" :value="source">
              {{ source }}
            </option>
          </select>
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-2">Start Date</label>
          <input
            v-model="filters.startDate"
            type="datetime-local"
            @change="loadLogs"
            class="w-full border rounded-md px-3 py-2"
          />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-2">End Date</label>
          <input
            v-model="filters.endDate"
            type="datetime-local"
            @change="loadLogs"
            class="w-full border rounded-md px-3 py-2"
          />
        </div>
      </div>
      <div class="mt-4">
        <label class="block text-sm font-medium text-gray-700 mb-2">Search</label>
        <input
          v-model="filters.searchText"
          type="text"
          placeholder="Search in message, exception, or source..."
          @input="debouncedSearch"
          class="w-full border rounded-md px-3 py-2"
        />
      </div>
    </div>

    <!-- Logs Table -->
    <div class="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
      <div class="overflow-x-auto">
        <table class="min-w-full divide-y divide-gray-200">
          <thead class="bg-gray-50">
            <tr>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                Timestamp
              </th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                Level
              </th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                Source
              </th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                Message
              </th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                Exception
              </th>
            </tr>
          </thead>
          <tbody class="bg-white divide-y divide-gray-200">
            <tr v-if="loading" class="text-center">
              <td colspan="5" class="px-6 py-4">
                <div class="flex items-center justify-center">
                  <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
                  <span class="ml-2">Loading logs...</span>
                </div>
              </td>
            </tr>
            <tr v-else-if="logs.length === 0" class="text-center">
              <td colspan="5" class="px-6 py-4 text-gray-500">
                No logs found
              </td>
            </tr>
            <tr
              v-else
              v-for="log in logs"
              :key="log.id"
              class="hover:bg-gray-50"
            >
              <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                {{ formatDate(log.timestamp) }}
              </td>
              <td class="px-6 py-4 whitespace-nowrap">
                <span
                  class="px-2 py-1 text-xs font-semibold rounded-full"
                  :class="getLevelColor(log.level)"
                >
                  {{ log.level }}
                </span>
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                {{ log.source || '-' }}
              </td>
              <td class="px-6 py-4 text-sm text-gray-900 max-w-md truncate">
                {{ log.message }}
              </td>
              <td class="px-6 py-4 text-sm text-gray-500">
                <span v-if="log.exception" class="text-red-600 cursor-pointer" @click="showException(log)">
                  View Exception
                </span>
                <span v-else>-</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div v-if="!loading && logs.length > 0" class="bg-gray-50 px-6 py-4 flex items-center justify-between">
        <div class="text-sm text-gray-700">
          Showing {{ (currentPage - 1) * pageSize + 1 }} to
          {{ Math.min(currentPage * pageSize, totalCount) }} of {{ totalCount }} logs
        </div>
        <div class="flex gap-2">
          <button
            @click="previousPage"
            :disabled="currentPage === 1"
            class="px-4 py-2 border rounded-md disabled:opacity-50 disabled:cursor-not-allowed hover:bg-gray-100"
          >
            Previous
          </button>
          <button
            @click="nextPage"
            :disabled="currentPage * pageSize >= totalCount"
            class="px-4 py-2 border rounded-md disabled:opacity-50 disabled:cursor-not-allowed hover:bg-gray-100"
          >
            Next
          </button>
        </div>
      </div>
    </div>

    <!-- Exception Modal -->
    <div
      v-if="selectedException"
      class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50"
      @click="selectedException = null"
    >
      <div
        class="bg-white rounded-lg p-6 max-w-3xl max-h-96 overflow-auto"
        @click.stop
      >
        <div class="flex justify-between items-center mb-4">
          <h3 class="text-lg font-semibold">Exception Details</h3>
          <button
            @click="selectedException = null"
            class="text-gray-500 hover:text-gray-700"
          >
            ✕
          </button>
        </div>
        <pre class="text-sm text-gray-800 whitespace-pre-wrap">{{ selectedException }}</pre>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({
  middleware: 'admin'
})

const logs = ref<any[]>([])
const loading = ref(false)
const logLevels = ref<string[]>([])
const sources = ref<string[]>([])
const selectedException = ref<string | null>(null)

const currentPage = ref(1)
const pageSize = ref(50)
const totalCount = ref(0)

const filters = reactive({
  level: '',
  source: '',
  startDate: '',
  endDate: '',
  searchText: ''
})

const logsApi = useLogs()

const loadLogs = async () => {
  loading.value = true
  try {
    const filter: any = {
      page: currentPage.value,
      pageSize: pageSize.value
    }

    if (filters.level) filter.level = filters.level
    if (filters.source) filter.source = filters.source
    if (filters.startDate) filter.startDate = new Date(filters.startDate).toISOString()
    if (filters.endDate) filter.endDate = new Date(filters.endDate).toISOString()
    if (filters.searchText) filter.searchText = filters.searchText

    const response = await logsApi.getLogs(filter)
    logs.value = response.items
    totalCount.value = response.totalCount
  } catch (error) {
    console.error('Error loading logs:', error)
  } finally {
    loading.value = false
  }
}

const loadMetadata = async () => {
  try {
    logLevels.value = await logsApi.getLogLevels()
    sources.value = await logsApi.getSources()
  } catch (error) {
    console.error('Error loading metadata:', error)
  }
}

let searchTimeout: NodeJS.Timeout | null = null
const debouncedSearch = () => {
  if (searchTimeout) {
    clearTimeout(searchTimeout)
  }
  searchTimeout = setTimeout(() => {
    currentPage.value = 1
    loadLogs()
  }, 500)
}

const previousPage = () => {
  if (currentPage.value > 1) {
    currentPage.value--
    loadLogs()
  }
}

const nextPage = () => {
  if (currentPage.value * pageSize.value < totalCount.value) {
    currentPage.value++
    loadLogs()
  }
}

const formatDate = (dateString: string) => {
  return new Date(dateString).toLocaleString()
}

const getLevelColor = (level: string) => {
  switch (level.toUpperCase()) {
    case 'ERROR':
    case 'FATAL':
      return 'bg-red-100 text-red-800'
    case 'WARNING':
    case 'WARN':
      return 'bg-yellow-100 text-yellow-800'
    case 'INFORMATION':
    case 'INFO':
      return 'bg-blue-100 text-blue-800'
    default:
      return 'bg-gray-100 text-gray-800'
  }
}

const showException = (log: any) => {
  selectedException.value = log.exception
}

onMounted(() => {
  loadMetadata()
  loadLogs()
})
</script>

