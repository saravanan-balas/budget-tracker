<template>
  <div class="min-h-screen bg-gray-50">
    <div class="max-w-4xl mx-auto py-8 px-4 sm:px-6 lg:px-8">

      <!-- Header -->
      <div class="mb-8">
        <div class="flex items-center gap-3 mb-2">
          <div class="w-10 h-10 bg-blue-100 rounded-xl flex items-center justify-center flex-shrink-0">
            <svg class="w-5 h-5 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/>
            </svg>
          </div>
          <h1 class="text-3xl font-bold text-gray-900">Import Bank Statement</h1>
        </div>
        <p class="text-gray-500 mt-1">Upload a CSV export from your bank. Our AI will automatically read, parse, and categorize your transactions.</p>
      </div>

      <!-- Progress Steps -->
      <div class="mb-8">
        <div class="flex items-center">
          <div v-for="(step, index) in steps" :key="index" class="flex items-center" :class="index < steps.length - 1 ? 'flex-1' : ''">
            <div class="flex items-center gap-2">
              <div :class="[
                'flex items-center justify-center w-8 h-8 rounded-full text-sm font-semibold transition-colors',
                currentStep > index ? 'bg-green-500 text-white' :
                currentStep === index ? 'bg-blue-600 text-white' :
                'bg-gray-200 text-gray-400'
              ]">
                <svg v-if="currentStep > index" class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M5 13l4 4L19 7"/>
                </svg>
                <span v-else>{{ index + 1 }}</span>
              </div>
              <span :class="['text-sm font-medium', currentStep >= index ? 'text-gray-900' : 'text-gray-400']">{{ step }}</span>
            </div>
            <div v-if="index < steps.length - 1" class="flex-1 h-px mx-4" :class="currentStep > index ? 'bg-green-300' : 'bg-gray-200'"></div>
          </div>
        </div>
      </div>

      <!-- Step 0: Upload File -->
      <div v-if="currentStep === 0">

        <!-- Bank Download Guide -->
        <div class="bg-blue-50 border border-blue-200 rounded-xl mb-6 overflow-hidden">
          <button
            @click="showBankGuide = !showBankGuide"
            class="w-full flex items-center justify-between px-5 py-4 text-left hover:bg-blue-100 transition-colors"
          >
            <div class="flex items-center gap-2.5">
              <span class="text-lg">🏦</span>
              <div>
                <p class="text-sm font-semibold text-blue-900">How do I download my bank statement as CSV?</p>
                <p class="text-xs text-blue-600">Works with any bank. Quick steps inside.</p>
              </div>
            </div>
            <svg class="w-5 h-5 text-blue-500 transition-transform flex-shrink-0" :class="showBankGuide ? 'rotate-180' : ''" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
            </svg>
          </button>

          <div v-show="showBankGuide" class="border-t border-blue-200 px-5 pb-5 pt-4">
            <ol class="space-y-3">
              <li class="flex gap-3 text-sm text-blue-900">
                <span class="flex-shrink-0 w-5 h-5 bg-blue-600 text-white rounded-full flex items-center justify-center text-xs font-bold mt-0.5">1</span>
                <span>Log in to your bank's website or mobile app and open the account you want to export.</span>
              </li>
              <li class="flex gap-3 text-sm text-blue-900">
                <span class="flex-shrink-0 w-5 h-5 bg-blue-600 text-white rounded-full flex items-center justify-center text-xs font-bold mt-0.5">2</span>
                <span>Look for a <strong>Download, Export,</strong> or <strong>Statement</strong> option. It is usually near your transaction list or account activity section.</span>
              </li>
              <li class="flex gap-3 text-sm text-blue-900">
                <span class="flex-shrink-0 w-5 h-5 bg-blue-600 text-white rounded-full flex items-center justify-center text-xs font-bold mt-0.5">3</span>
                <span>Select <strong>CSV</strong> as the file format. If multiple formats are shown, avoid PDF. CSV, Excel, or Spreadsheet formats all work.</span>
              </li>
              <li class="flex gap-3 text-sm text-blue-900">
                <span class="flex-shrink-0 w-5 h-5 bg-blue-600 text-white rounded-full flex items-center justify-center text-xs font-bold mt-0.5">4</span>
                <span>Choose your date range and download. Then upload that file here and we handle the rest automatically.</span>
              </li>
            </ol>
            <div class="mt-4 bg-white rounded-lg px-3 py-2.5 border border-blue-200 space-y-1">
              <p class="text-xs text-blue-700">💡 <strong>Tip:</strong> You can also download from your bank's mobile app. Look under Account Details or Statements in the app menu.</p>
              <p class="text-xs text-blue-600">🔄 Already imported before? Re-uploading the same file will not create duplicates.</p>
            </div>
          </div>
        </div>

        <!-- Upload Card -->
        <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6 mb-6">
          <h2 class="text-lg font-semibold text-gray-900 mb-1">Upload your CSV file</h2>
          <p class="text-sm text-gray-500 mb-5">Supports any bank's CSV export. Commas, semicolons, and tabs all work.</p>

          <!-- Drop Zone -->
          <label
            for="file-upload"
            class="group relative flex flex-col items-center justify-center w-full border-2 border-dashed rounded-xl cursor-pointer transition-colors"
            :class="selectedFile ? 'border-green-400 bg-green-50 py-5' : 'border-gray-300 bg-gray-50 hover:border-blue-400 hover:bg-blue-50 py-10'"
          >
            <!-- File selected state -->
            <div v-if="selectedFile" class="flex items-center gap-4 w-full px-6">
              <div class="w-12 h-12 bg-green-100 rounded-xl flex items-center justify-center flex-shrink-0">
                <svg class="w-6 h-6 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0"/>
                </svg>
              </div>
              <div class="flex-1 min-w-0">
                <p class="text-sm font-semibold text-gray-900 truncate">{{ selectedFile.name }}</p>
                <p class="text-xs text-gray-500 mt-0.5">{{ formatFileSize(selectedFile.size) }} · Ready to upload</p>
              </div>
              <span class="text-xs text-blue-600 underline flex-shrink-0">Change file</span>
            </div>

            <!-- Empty state -->
            <div v-else class="text-center px-6">
              <svg class="w-12 h-12 mx-auto mb-3 text-gray-300 group-hover:text-blue-400 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/>
              </svg>
              <p class="text-sm font-medium text-gray-700 group-hover:text-blue-700">
                <span class="text-blue-600 font-semibold">Click to select</span> or drag and drop
              </p>
              <p class="text-xs text-gray-400 mt-1">CSV or TXT files up to 10 MB</p>
            </div>

            <input
              id="file-upload"
              name="file-upload"
              type="file"
              class="sr-only"
              accept=".csv,.txt"
              @change="handleFileSelect"
            >
          </label>

          <!-- Account Selection -->
          <div v-if="selectedFile" class="mt-5">
            <label class="block text-sm font-semibold text-gray-700 mb-1.5">Which account is this statement for?</label>
            <p class="text-xs text-gray-500 mb-2">Transactions will be linked to this account.</p>
            <select
              v-model="selectedAccountId"
              class="w-full px-3 py-2.5 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white"
            >
              <option value="">Select an account…</option>
              <option v-for="account in accountOptions" :key="account.id" :value="account.id">
                {{ account.name }} ({{ account.type || 'N/A' }})
              </option>
            </select>
          </div>

          <!-- Analyze Button -->
          <div v-if="selectedFile && selectedAccountId" class="mt-5">
            <button
              @click="analyzeFile"
              :disabled="isAnalyzing"
              class="w-full flex items-center justify-center gap-2 bg-blue-600 text-white py-3 px-4 rounded-lg font-semibold hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <svg v-if="isAnalyzing" class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
              </svg>
              <span>{{ isAnalyzing ? 'Analyzing file…' : 'Continue →' }}</span>
            </button>
          </div>
        </div>

        <!-- What happens next -->
        <div class="bg-white rounded-xl border border-gray-200 p-5">
          <h3 class="text-sm font-semibold text-gray-700 mb-3">What happens after you upload?</h3>
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div class="flex gap-3 items-start">
              <div class="w-8 h-8 bg-blue-100 rounded-lg flex items-center justify-center flex-shrink-0">
                <span class="text-sm">🔍</span>
              </div>
              <div>
                <p class="text-sm font-medium text-gray-800">Auto-detect format</p>
                <p class="text-xs text-gray-500">We recognize any bank's CSV layout automatically.</p>
              </div>
            </div>
            <div class="flex gap-3 items-start">
              <div class="w-8 h-8 bg-purple-100 rounded-lg flex items-center justify-center flex-shrink-0">
                <span class="text-sm">✨</span>
              </div>
              <div>
                <p class="text-sm font-medium text-gray-800">AI categorization</p>
                <p class="text-xs text-gray-500">Each transaction is categorized intelligently by AI.</p>
              </div>
            </div>
            <div class="flex gap-3 items-start">
              <div class="w-8 h-8 bg-green-100 rounded-lg flex items-center justify-center flex-shrink-0">
                <span class="text-sm">🔄</span>
              </div>
              <div>
                <p class="text-sm font-medium text-gray-800">Duplicate safe</p>
                <p class="text-xs text-gray-500">Re-importing the same file won't create duplicates.</p>
              </div>
            </div>
          </div>
        </div>

      </div>

      <!-- File Analysis Results -->
      <div v-if="currentStep === 1 && fileAnalysis" class="bg-white rounded-xl shadow-sm border border-gray-200 p-6 mb-6">
        <div class="flex items-center gap-3 mb-5">
          <div class="w-9 h-9 bg-green-100 rounded-lg flex items-center justify-center">
            <svg class="w-5 h-5 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0"/>
            </svg>
          </div>
          <div>
            <h2 class="text-lg font-semibold text-gray-900">File looks good!</h2>
            <p class="text-sm text-gray-500">Review the details below and start the import when ready.</p>
          </div>
        </div>

        <div class="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-6">
          <div class="bg-gray-50 rounded-lg px-4 py-3">
            <p class="text-xs text-gray-500 mb-0.5">Format</p>
            <p class="text-sm font-semibold text-gray-900">{{ fileAnalysis.fileFormat }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg px-4 py-3">
            <p class="text-xs text-gray-500 mb-0.5">File size</p>
            <p class="text-sm font-semibold text-gray-900">{{ formatFileSize(fileAnalysis.fileSize) }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg px-4 py-3">
            <p class="text-xs text-gray-500 mb-0.5">Est. transactions</p>
            <p class="text-sm font-semibold text-gray-900">{{ fileAnalysis.estimatedRowCount ?? 'N/A' }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg px-4 py-3">
            <p class="text-xs text-gray-500 mb-0.5">Processing</p>
            <p class="text-sm font-semibold" :class="fileAnalysis.canProcessSynchronously ? 'text-green-600' : 'text-orange-500'">
              {{ fileAnalysis.canProcessSynchronously ? 'Instant' : 'Background' }}
            </p>
          </div>
        </div>

        <div v-if="!fileAnalysis.canProcessSynchronously" class="bg-orange-50 border border-orange-200 rounded-lg px-4 py-3 mb-5 text-sm text-orange-800">
          <strong>Heads up:</strong> This file is large and will be processed in the background. We will import your transactions automatically. You can leave this page and check back later.
        </div>

        <div class="flex gap-3">
          <button
            @click="startImport"
            :disabled="isImporting"
            class="flex items-center gap-2 bg-blue-600 text-white py-2.5 px-5 rounded-lg font-semibold hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            <svg v-if="isImporting" class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
            </svg>
            <span>{{ isImporting ? 'Starting import…' : 'Start Import' }}</span>
          </button>
          <button
            @click="goBack"
            class="py-2.5 px-5 rounded-lg font-medium text-gray-600 border border-gray-300 hover:bg-gray-50 transition-colors"
          >
            ← Choose different file
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

// Bank guide state
const showBankGuide = ref(false)

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
  
  try {
    const formData = new FormData()
    formData.append('file', selectedFile.value)
    formData.append('accountId', selectedAccountId.value)
    
    const api = useApi()
    const response = await api.smartImport(formData)
    
    if (response.jobId) {
      // Async processing
      jobId.value = response.jobId
      importId.value = response.importId
      currentStep.value = 2
      startStatusPolling()
    } else {
      // Sync processing completed
      // Ensure we track the importId so we can load transactions by import later
      importId.value = response.importId || ''

      importStatus.value = {
        importId: response.importId || '',
        status: 'Completed',
        totalRows: response.transactions?.length || 0,
        processedRows: response.transactions?.length || 0,
        importedTransactions: response.transactions?.length || 0,
        duplicateTransactions: 0,
        failedRows: 0,
        isProcessedSynchronously: true
      }
      currentStep.value = 2

      // For synchronous imports, immediately load the imported transactions
      // so the "Review & Edit Imported Transactions" panel shows real data
      if (importId.value) {
        await loadImportedTransactions()
        showImportedTransactions.value = true
      }
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

    // Keep the status counters in sync with the actual imported transactions
    if (importStatus.value) {
      const count = importedTransactions.value.length
      importStatus.value.importedTransactions = count
      // If backend didn’t populate these correctly, align them as well
      if (!importStatus.value.totalRows) {
        importStatus.value.totalRows = count
      }
      if (!importStatus.value.processedRows) {
        importStatus.value.processedRows = count
      }
    }
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