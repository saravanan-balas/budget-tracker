<template>
  <div class="min-h-screen bg-gray-50">
    <div class="max-w-4xl mx-auto py-8 px-4 sm:px-6 lg:px-8">
      <!-- Header -->
      <div class="mb-8">
        <h1 class="text-3xl font-bold text-gray-900">Import Transactions</h1>
        <p class="mt-2 text-gray-600">Upload bank statements from any bank globally using CSV files.</p>
      </div>

      <!-- Import Steps -->
      <div class="mb-8">
        <div class="flex items-center justify-between">
          <div v-for="(step, index) in steps" :key="index" 
               :class="[
                 'flex items-center',
                 index < steps.length - 1 ? 'flex-1' : ''
               ]">
            <div :class="[
              'flex items-center justify-center w-8 h-8 rounded-full text-sm font-medium',
              currentStep > index ? 'bg-green-500 text-white' :
              currentStep === index ? 'bg-blue-500 text-white' :
              'bg-gray-200 text-gray-500'
            ]">
              {{ index + 1 }}
            </div>
            <span :class="[
              'ml-2 text-sm font-medium',
              currentStep >= index ? 'text-gray-900' : 'text-gray-500'
            ]">{{ step }}</span>
            <div v-if="index < steps.length - 1" class="flex-1 h-px bg-gray-200 mx-4"></div>
          </div>
        </div>
      </div>

      <!-- File Upload Section -->
      <div v-if="currentStep === 0" class="bg-white rounded-lg shadow p-6 mb-6">
        <h2 class="text-xl font-semibold mb-4">Choose File Type</h2>
        
        <!-- File Type Selection -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
          <div @click="selectFileType('csv')" 
               :class="[
                 'border-2 rounded-lg p-4 cursor-pointer transition-colors',
                 selectedFileType === 'csv' ? 'border-blue-500 bg-blue-50' : 'border-gray-200 hover:border-gray-300'
               ]">
            <div class="text-center">
              <svg class="w-12 h-12 mx-auto mb-2 text-green-500" fill="currentColor" viewBox="0 0 20 20">
                <path d="M4 4a2 2 0 00-2 2v8a2 2 0 002 2h12a2 2 0 002-2V6a2 2 0 00-2-2H4zm2 2h8a1 1 0 110 2H6a1 1 0 110-2zm0 3h8a1 1 0 110 2H6a1 1 0 110-2zm0 3h4a1 1 0 110 2H6a1 1 0 110-2z"/>
              </svg>
              <h3 class="font-medium">CSV File</h3>
              <p class="text-sm text-gray-500">Comma-separated values from your bank</p>
            </div>
          </div>
        </div>

        <!-- File Upload Area -->
        <div v-if="selectedFileType" class="border-2 border-dashed border-gray-300 rounded-lg p-6">
          <div class="text-center">
            <svg class="w-12 h-12 mx-auto mb-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 48 48">
              <path d="M28 8H12a4 4 0 00-4 4v20m32-12v8m0 0v8a4 4 0 01-4 4H12a4 4 0 01-4-4v-4m32-4l-3.172-3.172a4 4 0 00-5.656 0L28 28M8 32l9.172-9.172a4 4 0 015.656 0L28 28m0 0l4 4m4-24h8m-4-4v8m-12 4h.02" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            <div class="mb-4">
              <label for="file-upload" class="cursor-pointer">
                <span class="text-blue-600 hover:text-blue-500 font-medium">Click to upload</span>
                <span class="text-gray-500"> or drag and drop</span>
              </label>
              <input 
                id="file-upload" 
                name="file-upload" 
                type="file" 
                class="sr-only" 
                :accept="fileAcceptTypes[selectedFileType]"
                @change="handleFileSelect"
              >
            </div>
            <p class="text-sm text-gray-500">
              {{ fileTypeDescriptions[selectedFileType] }}
            </p>
          </div>
        </div>

        <!-- Account Selection -->
        <div v-if="selectedFile" class="mt-6">
          <label class="block text-sm font-medium text-gray-700 mb-2">Select Account</label>
          <select v-model="selectedAccountId" class="w-full p-2 border border-gray-300 rounded-md">
            <option value="">Choose an account...</option>
            <option v-for="account in accountOptions" :key="account.id" :value="account.id">
              {{ account.name }} ({{ account.type || 'N/A' }})
            </option>
          </select>
        </div>

        <!-- Analyze File Button -->
        <div v-if="selectedFile && selectedAccountId" class="mt-6">
          <button 
            @click="analyzeFile"
            :disabled="isAnalyzing"
            class="w-full bg-blue-600 text-white py-2 px-4 rounded-md hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <span v-if="isAnalyzing">Analyzing File...</span>
            <span v-else>Analyze File</span>
          </button>
        </div>
      </div>

      <!-- File Analysis Results -->
      <div v-if="currentStep === 1 && fileAnalysis" class="bg-white rounded-lg shadow p-6 mb-6">
        <h2 class="text-xl font-semibold mb-4">File Analysis</h2>
        
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <h3 class="font-medium text-gray-900 mb-2">Detected Information</h3>
            <ul class="space-y-1 text-sm">
              <li><span class="text-gray-600">Format:</span> {{ fileAnalysis.fileFormat }}</li>
              <li><span class="text-gray-600">Size:</span> {{ formatFileSize(fileAnalysis.fileSize) }}</li>
              <li><span class="text-gray-600">Processing:</span> 
                <span :class="fileAnalysis.canProcessSynchronously ? 'text-green-600' : 'text-orange-600'">
                  {{ fileAnalysis.canProcessSynchronously ? 'Instant' : 'Background' }}
                </span>
              </li>
              <li v-if="fileAnalysis.estimatedRowCount"><span class="text-gray-600">Est. Transactions:</span> {{ fileAnalysis.estimatedRowCount }}</li>
            </ul>
          </div>
          
          <div>
            <h3 class="font-medium text-gray-900 mb-2">Processing Details</h3>
            <ul class="space-y-1 text-sm">
              <li><span class="text-gray-600">Template Available:</span> 
                <span :class="fileAnalysis.hasKnownTemplate ? 'text-green-600' : 'text-gray-500'">
                  {{ fileAnalysis.hasKnownTemplate ? 'Yes' : 'No' }}
                </span>
              </li>
              <li><span class="text-gray-600">Estimated Time:</span> {{ fileAnalysis.estimatedSeconds }}s</li>
              <li><span class="text-gray-600">AI Cost:</span> ${{ fileAnalysis.estimatedCost.toFixed(4) }}</li>
              <li v-if="!fileAnalysis.canProcessSynchronously"><span class="text-gray-600">Reason:</span> {{ fileAnalysis.asyncReason }}</li>
            </ul>
          </div>
        </div>

        <div class="mt-6 flex space-x-4">
          <button 
            @click="startImport"
            :disabled="isImporting"
            class="bg-green-600 text-white py-2 px-4 rounded-md hover:bg-green-700 disabled:opacity-50"
          >
            <span v-if="isImporting">Starting Import...</span>
            <span v-else>Start Import</span>
          </button>
          <button 
            @click="goBack"
            class="bg-gray-300 text-gray-700 py-2 px-4 rounded-md hover:bg-gray-400"
          >
            Choose Different File
          </button>
        </div>
      </div>

      <!-- Import Progress -->
      <div v-if="currentStep === 2 && importStatus" class="bg-white rounded-lg shadow p-6">
        <h2 class="text-xl font-semibold mb-4">Import Progress</h2>
        
        <div class="mb-4">
          <div class="flex justify-between text-sm text-gray-600 mb-1">
            <span>{{ importStatus.status }}</span>
            <span v-if="importStatus.estimatedSecondsRemaining">
              {{ importStatus.estimatedSecondsRemaining }}s remaining
            </span>
          </div>
          <div class="w-full bg-gray-200 rounded-full h-2">
            <div 
              class="bg-blue-600 h-2 rounded-full transition-all duration-300"
              :style="{ width: progressPercentage + '%' }"
            ></div>
          </div>
          
          <!-- Processing Details -->
          <div class="mt-3 text-sm text-gray-600">
            <div v-if="importStatus.totalRows > 0" class="flex justify-between">
              <span>Processed: {{ importStatus.processedRows }} / {{ importStatus.totalRows }} rows</span>
              <span>{{ progressPercentage }}%</span>
            </div>
            <div v-if="importStatus.importedTransactions > 0" class="mt-1">
              <span class="text-green-600">✓ {{ importStatus.importedTransactions }} transactions imported</span>
            </div>
            <div v-if="importStatus.duplicateTransactions > 0" class="mt-1">
              <span class="text-yellow-600">⚠ {{ importStatus.duplicateTransactions }} duplicates skipped</span>
            </div>
            <div v-if="importStatus.failedRows > 0" class="mt-1">
              <span class="text-red-600">✗ {{ importStatus.failedRows }} rows failed</span>
            </div>
          </div>
        </div>

        <div v-if="importStatus.status === 'Completed'" class="bg-green-50 border border-green-200 rounded-md p-4">
          <h3 class="text-green-800 font-medium mb-2">Import Completed Successfully!</h3>
          <ul class="text-sm text-green-700 space-y-1">
            <li>✓ {{ importStatus.importedTransactions }} transactions imported</li>
            <li v-if="importStatus.duplicateTransactions">⚠ {{ importStatus.duplicateTransactions }} duplicates skipped</li>
            <li v-if="importStatus.detectedBankName">🏦 Detected bank: {{ importStatus.detectedBankName }}</li>
            <li v-if="importStatus.aiCost">💰 AI processing cost: ${{ importStatus.aiCost.toFixed(4) }}</li>
          </ul>
          <div class="mt-4 flex space-x-4">
            <NuxtLink to="/transactions" class="bg-green-600 text-white py-2 px-4 rounded-md hover:bg-green-700">
              View All Transactions
            </NuxtLink>
            <button 
              @click="showImportedTransactions = true; loadImportedTransactions()"
              class="bg-blue-600 text-white py-2 px-4 rounded-md hover:bg-blue-700"
            >
              Review & Edit Imported Transactions ({{ importStatus.importedTransactions }})
            </button>
          </div>
        </div>

        <div v-if="importStatus.status === 'Failed'" class="bg-red-50 border border-red-200 rounded-md p-4">
          <h3 class="text-red-800 font-medium mb-2">Import Failed</h3>
          <p class="text-sm text-red-700">{{ importStatus.errorDetails }}</p>
          <div class="mt-4">
            <button 
              @click="goBack"
              class="bg-red-600 text-white py-2 px-4 rounded-md hover:bg-red-700"
            >
              Try Again
            </button>
          </div>
        </div>
      </div>

      <!-- Imported Transactions Display -->
      <div v-if="showImportedTransactions && importedTransactions.length > 0" class="bg-white rounded-lg shadow p-6 mt-6">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-xl font-semibold">Imported Transactions</h2>
          <button 
            @click="showImportedTransactions = false"
            class="text-gray-500 hover:text-gray-700"
          >
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
            </svg>
          </button>
        </div>

        <!-- Category Summary -->
        <div v-if="categorySummary.length > 0" class="mb-6 p-4 bg-gray-50 rounded-md">
          <h3 class="text-sm font-medium text-gray-700 mb-3">Category Summary</h3>
          <div class="grid grid-cols-2 md:grid-cols-3 gap-3">
            <div v-for="cat in categorySummary" :key="cat.category" class="text-sm">
              <span class="font-medium">{{ cat.category }}:</span>
              <span class="text-gray-600"> ${{ cat.total.toFixed(2) }} ({{ cat.count }})</span>
            </div>
          </div>
          <div class="mt-3 pt-3 border-t border-gray-200">
            <span class="font-medium">Total:</span>
            <span class="text-gray-900 font-semibold"> ${{ totalAmount.toFixed(2) }}</span>
          </div>
        </div>

        <!-- Transactions Table -->
        <div class="overflow-x-auto">
          <div class="flex justify-between items-center mb-4">
            <div class="text-sm text-gray-600">
              {{ importedTransactions.length }} transactions loaded
            </div>
            <div class="flex space-x-2">
              <button 
                @click="enableBulkEdit = !enableBulkEdit"
                :class="[
                  'px-3 py-1 text-xs rounded',
                  enableBulkEdit ? 'bg-blue-100 text-blue-800' : 'bg-gray-100 text-gray-600'
                ]"
              >
                {{ enableBulkEdit ? 'Exit Bulk Edit' : 'Bulk Edit' }}
              </button>
              <button 
                v-if="selectedTransactions.length > 0"
                @click="bulkDeleteSelected"
                class="px-3 py-1 text-xs bg-red-100 text-red-800 rounded hover:bg-red-200"
              >
                Delete Selected ({{ selectedTransactions.length }})
              </button>
            </div>
          </div>
          
          <table class="min-w-full divide-y divide-gray-200">
            <thead class="bg-gray-50">
              <tr>
                <th v-if="enableBulkEdit" class="px-4 py-2 text-left">
                  <input 
                    type="checkbox" 
                    @change="toggleSelectAll"
                    :checked="allSelected"
                    class="rounded border-gray-300"
                  >
                </th>
                <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Date</th>
                <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Description</th>
                <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Category</th>
                <th class="px-4 py-2 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
                <th class="px-4 py-2 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
              </tr>
            </thead>
            <tbody class="bg-white divide-y divide-gray-200">
              <tr v-for="transaction in importedTransactions" :key="transaction.id" class="hover:bg-gray-50">
                <td v-if="enableBulkEdit" class="px-4 py-2">
                  <input 
                    type="checkbox" 
                    :value="transaction.id"
                    v-model="selectedTransactions"
                    class="rounded border-gray-300"
                  >
                </td>
                <td class="px-4 py-2 whitespace-nowrap text-sm text-gray-900">
                  {{ formatDate(transaction.transactionDate) }}
                </td>
                <td class="px-4 py-2 text-sm text-gray-900">
                  <div v-if="editingTransaction === transaction.id">
                    <input 
                      v-model="editForm.description"
                      type="text"
                      class="w-full px-2 py-1 text-sm border border-gray-300 rounded"
                      @blur="saveTransactionEdit(transaction.id)"
                      @keyup.enter="saveTransactionEdit(transaction.id)"
                      @keyup.escape="cancelEdit"
                    >
                  </div>
                  <div v-else @click="startEdit(transaction)" class="cursor-pointer hover:bg-gray-100 px-1 py-1 rounded">
                    {{ transaction.description || transaction.originalMerchant }}
                  </div>
                </td>
                <td class="px-4 py-2 whitespace-nowrap text-sm">
                  <div v-if="editingTransaction === transaction.id">
                    <select 
                      v-model="editForm.categoryId"
                      class="text-xs border border-gray-300 rounded px-1 py-1"
                      @change="saveTransactionEdit(transaction.id)"
                    >
                      <option value="">Uncategorized</option>
                      <option 
                        v-for="category in categories" 
                        :key="category.id" 
                        :value="category.id"
                      >
                        {{ category.name }}
                      </option>
                    </select>
                  </div>
                  <div v-else @click="startEdit(transaction)" class="cursor-pointer hover:bg-gray-100 px-1 py-1 rounded">
                    <span :class="[
                      'inline-flex px-2 py-1 text-xs font-medium rounded-full',
                      getCategoryColor(transaction.categoryName)
                    ]">
                      {{ transaction.categoryName || 'Uncategorized' }}
                    </span>
                  </div>
                </td>
                <td class="px-4 py-2 whitespace-nowrap text-sm text-right">
                  <span :class="transaction.amount < 0 ? 'text-red-600' : 'text-green-600'">
                    ${{ Math.abs(transaction.amount).toFixed(2) }}
                  </span>
                </td>
                <td class="px-4 py-2 whitespace-nowrap text-right text-sm">
                  <div class="flex space-x-1 justify-end">
                    <button 
                      @click="startEdit(transaction)"
                      class="text-blue-600 hover:text-blue-900 text-xs px-2 py-1 rounded hover:bg-blue-50"
                      title="Edit"
                    >
                      ✏️
                    </button>
                    <button 
                      @click="deleteTransaction(transaction.id)"
                      class="text-red-600 hover:text-red-900 text-xs px-2 py-1 rounded hover:bg-red-50"
                      title="Delete"
                    >
                      🗑️
                    </button>
                  </div>
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
import { ref, computed, onMounted, onUnmounted } from 'vue'

// Page meta
definePageMeta({
  middleware: 'auth'
})

// Types
interface FileAnalysis {
  fileFormat: string
  fileSize: number
  canProcessSynchronously: boolean
  asyncReason: string
  estimatedSeconds: number
  hasKnownTemplate: boolean
  estimatedCost: number
  estimatedRowCount?: number
}

interface ImportStatus {
  importId: string
  status: string
  totalRows: number
  processedRows: number
  importedTransactions: number
  duplicateTransactions: number
  failedRows: number
  errorDetails?: string
  detectedBankName?: string
  detectedFormat?: string
  aiCost?: number
  isProcessedSynchronously: boolean
  estimatedSecondsRemaining?: number
}

interface Account {
  id: string
  name: string
  type: string
}

// State
const currentStep = ref(0)
const steps = ['Upload File', 'Review Analysis', 'Import Progress']

const selectedFileType = ref('csv')
const selectedFile = ref<File | null>(null)
const selectedAccountId = ref('')
const accounts = ref<Account[]>([])

const isAnalyzing = ref(false)
const isImporting = ref(false)
const fileAnalysis = ref<FileAnalysis | null>(null)
const importStatus = ref<ImportStatus | null>(null)
const importId = ref('')
const jobId = ref('')
const statusPollingInterval = ref<NodeJS.Timeout | null>(null)

const showImportedTransactions = ref(false)
const importedTransactions = ref<any[]>([])
const enableBulkEdit = ref(false)
const selectedTransactions = ref<string[]>([])
const editingTransaction = ref<string | null>(null)
const editForm = reactive({
  description: '',
  categoryId: '',
  notes: '',
  tags: ''
})
const categories = ref<any[]>([])

// Constants
const fileAcceptTypes: Record<string, string> = {
  csv: '.csv,.txt'
}

const fileTypeDescriptions: Record<string, string> = {
  csv: 'CSV files up to 10MB. Supports comma, semicolon, and tab delimited formats.'
}

// Computed
const progressPercentage = computed(() => {
  if (!importStatus.value) return 0
  
  if (importStatus.value.status === 'Completed') return 100
  if (importStatus.value.status === 'Failed') return 100
  
  if (importStatus.value.totalRows > 0) {
    return Math.round((importStatus.value.processedRows / importStatus.value.totalRows) * 100)
  }
  
  return 25 // Default progress for processing
})

const categorySummary = computed(() => {
  const summary: Record<string, { count: number; total: number }> = {}
  
  importedTransactions.value.forEach(txn => {
    const category = txn.categoryName || 'Uncategorized'
    if (!summary[category]) {
      summary[category] = { count: 0, total: 0 }
    }
    summary[category].count++
    summary[category].total += Math.abs(txn.amount)
  })
  
  return Object.entries(summary).map(([category, data]) => ({
    category,
    count: data.count,
    total: data.total
  })).sort((a, b) => b.total - a.total)
})

const totalAmount = computed(() => {
  return importedTransactions.value.reduce((sum, txn) => sum + Math.abs(txn.amount), 0)
})

// Computed for accounts display
const accountOptions = computed(() => {
  return accounts.value
})

// Methods
const selectFileType = (type: string) => {
  if (type !== 'csv') return
  selectedFileType.value = type
  selectedFile.value = null
}

const handleFileSelect = (event: Event) => {
  const target = event.target as HTMLInputElement
  if (target.files && target.files.length > 0) {
    selectedFile.value = target.files[0]
  }
}

const formatFileSize = (bytes: number) => {
  if (bytes === 0) return '0 Bytes'
  const k = 1024
  const sizes = ['Bytes', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

const analyzeFile = async () => {
  if (!selectedFile.value) return
  
  isAnalyzing.value = true
  
  try {
    const formData = new FormData()
    formData.append('file', selectedFile.value)
    
    const api = useApi()
    const response = await api.analyzeImportFile(formData)
    
    fileAnalysis.value = response
    currentStep.value = 1
  } catch (error) {
    console.error('Error analyzing file:', error)
    // Show error message
  } finally {
    isAnalyzing.value = false
  }
}

const startImport = async () => {
  if (!selectedFile.value || !selectedAccountId.value) return
  
  isImporting.value = true
  importedTransactions.value = []
  showImportedTransactions.value = false
  
  try {
    const formData = new FormData()
    formData.append('file', selectedFile.value)
    formData.append('accountId', selectedAccountId.value)
    
    const api = useApi()
    const response = await api.smartImport(formData)

    if (response.importId) {
      importId.value = response.importId
    }
    
    if (response.jobId) {
      // Async processing
      jobId.value = response.jobId
      currentStep.value = 2
      startStatusPolling()
    } else {
      // Sync processing completed
      if (importId.value) {
        try {
          importStatus.value = await api.getImportStatus(importId.value)
          await loadImportedTransactions()
          showImportedTransactions.value = true
        } catch (error) {
          console.error('Error loading import status after sync import:', error)
          importStatus.value = {
            importId: importId.value,
            status: 'Completed',
            totalRows: response.transactions?.length || 0,
            processedRows: response.transactions?.length || 0,
            importedTransactions: response.transactions?.length || 0,
            duplicateTransactions: 0,
            failedRows: 0,
            isProcessedSynchronously: true
          }
        }
      } else {
        importStatus.value = {
          importId: '',
          status: 'Completed',
          totalRows: response.transactions?.length || 0,
          processedRows: response.transactions?.length || 0,
          importedTransactions: response.transactions?.length || 0,
          duplicateTransactions: 0,
          failedRows: 0,
          isProcessedSynchronously: true
        }
      }
      currentStep.value = 2
    }
  } catch (error) {
    console.error('Error starting import:', error)
  } finally {
    isImporting.value = false
  }
}

const startStatusPolling = () => {
  if (statusPollingInterval.value) {
    clearInterval(statusPollingInterval.value)
  }
  
  // Poll more frequently for better user experience
  statusPollingInterval.value = setInterval(async () => {
    if (!importId.value) return
    
    try {
      const api = useApi()
      const status = await api.getImportStatus(importId.value)
      importStatus.value = status
      
      if (status.status === 'Completed' || status.status === 'Failed') {
        clearInterval(statusPollingInterval.value!)
        statusPollingInterval.value = null
        
        // Auto-load transactions when completed
        if (status.status === 'Completed' && status.importedTransactions > 0) {
          await loadImportedTransactions()
          showImportedTransactions.value = true
        }
      }
    } catch (error) {
      console.error('Error fetching status:', error)
    }
  }, 5000) // Poll every 5 seconds to reduce server load
}

const goBack = () => {
  currentStep.value = 0
  selectedFile.value = null
  selectedFileType.value = ''
  fileAnalysis.value = null
  importStatus.value = null
  importId.value = ''
  jobId.value = ''
  
  if (statusPollingInterval.value) {
    clearInterval(statusPollingInterval.value)
    statusPollingInterval.value = null
  }
}

const loadAccounts = async () => {
  try {
    const api = useApi()
    accounts.value = await api.getAccounts()
  } catch (error) {
    console.error('Error loading accounts:', error)
  }
}

const loadImportedTransactions = async () => {
  if (!importId.value) return
  
  try {
    const api = useApi()
    importedTransactions.value = await api.getTransactionsByImportId(importId.value)
  } catch (error) {
    console.error('Error loading imported transactions:', error)
  }
}

const loadCategories = async () => {
  try {
    const api = useApi()
    categories.value = await api.getCategories()
  } catch (error) {
    console.error('Error loading categories:', error)
  }
}

const startEdit = (transaction: any) => {
  editingTransaction.value = transaction.id
  editForm.description = transaction.description || transaction.originalMerchant
  editForm.categoryId = transaction.categoryId || ''
  editForm.notes = transaction.notes || ''
  editForm.tags = transaction.tags || ''
}

const cancelEdit = () => {
  editingTransaction.value = null
  Object.assign(editForm, {
    description: '',
    categoryId: '',
    notes: '',
    tags: ''
  })
}

const saveTransactionEdit = async (transactionId: string) => {
  if (!transactionId) return
  
  try {
    const api = useApi()
    await api.updateTransaction(transactionId, {
      description: editForm.description || undefined,
      categoryId: editForm.categoryId || undefined,
      notes: editForm.notes || undefined,
      tags: editForm.tags || undefined
    })
    
    // Reload transactions to show updated data
    await loadImportedTransactions()
    cancelEdit()
  } catch (error) {
    console.error('Error updating transaction:', error)
  }
}

const deleteTransaction = async (transactionId: string) => {
  if (!confirm('Are you sure you want to delete this transaction?')) return
  
  try {
    const api = useApi()
    await api.deleteTransaction(transactionId)
    
    // Remove from local array and update import status
    importedTransactions.value = importedTransactions.value.filter(t => t.id !== transactionId)
    if (importStatus.value) {
      importStatus.value.importedTransactions = Math.max(0, importStatus.value.importedTransactions - 1)
    }
  } catch (error) {
    console.error('Error deleting transaction:', error)
  }
}

const toggleSelectAll = () => {
  if (allSelected.value) {
    selectedTransactions.value = []
  } else {
    selectedTransactions.value = importedTransactions.value.map(t => t.id)
  }
}

const bulkDeleteSelected = async () => {
  if (selectedTransactions.value.length === 0) return
  
  const count = selectedTransactions.value.length
  if (!confirm(`Are you sure you want to delete ${count} selected transactions?`)) return
  
  try {
    const api = useApi()
    
    // Delete all selected transactions
    await Promise.all(
      selectedTransactions.value.map(id => api.deleteTransaction(id))
    )
    
    // Remove from local array and update import status
    importedTransactions.value = importedTransactions.value.filter(
      t => !selectedTransactions.value.includes(t.id)
    )
    
    if (importStatus.value) {
      importStatus.value.importedTransactions = Math.max(0, importStatus.value.importedTransactions - count)
    }
    
    selectedTransactions.value = []
  } catch (error) {
    console.error('Error deleting transactions:', error)
  }
}

const formatDate = (dateString: string) => {
  const date = new Date(dateString)
  return date.toLocaleDateString('en-US', { 
    year: 'numeric', 
    month: 'short', 
    day: 'numeric' 
  })
}

const getCategoryColor = (categoryName: string | undefined) => {
  if (!categoryName) return 'bg-gray-100 text-gray-800'
  
  const colors: Record<string, string> = {
    'Food & Dining': 'bg-orange-100 text-orange-800',
    'Transportation': 'bg-blue-100 text-blue-800',
    'Entertainment': 'bg-purple-100 text-purple-800',
    'Shopping': 'bg-pink-100 text-pink-800',
    'Groceries': 'bg-green-100 text-green-800',
    'Utilities': 'bg-yellow-100 text-yellow-800',
    'Healthcare': 'bg-red-100 text-red-800',
    'Banking': 'bg-indigo-100 text-indigo-800',
    'Phone & Internet': 'bg-cyan-100 text-cyan-800',
    'Uncategorized': 'bg-gray-100 text-gray-800'
  }
  
  return colors[categoryName] || 'bg-gray-100 text-gray-800'
}

// Computed for bulk edit
const allSelected = computed(() => {
  return importedTransactions.value.length > 0 && 
         selectedTransactions.value.length === importedTransactions.value.length
})

// Lifecycle
onMounted(() => {
  loadAccounts()
  loadCategories()
})

onUnmounted(() => {
  if (statusPollingInterval.value) {
    clearInterval(statusPollingInterval.value)
  }
})
</script>