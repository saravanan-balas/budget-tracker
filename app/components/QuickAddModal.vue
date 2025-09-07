<template>
  <div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50" @click="$emit('close')">
    <div class="bg-white rounded-lg p-6 w-full max-w-md mx-4" @click.stop>
      <div class="flex justify-between items-center mb-4">
        <h3 class="text-lg font-semibold">Quick Add Transaction</h3>
        <button @click="$emit('close')" class="text-gray-400 hover:text-gray-600">
          <XMarkIcon class="w-6 h-6" />
        </button>
      </div>
      
      <form @submit.prevent="handleSubmit" class="space-y-4">
        <div>
          <label class="form-label">Account *</label>
          <select v-model="form.accountId" class="form-input" required>
            <option value="">Select account</option>
            <option 
              v-for="account in accounts" 
              :key="account.id" 
              :value="account.id"
            >
              {{ account.name }} ({{ account.type }})
            </option>
          </select>
        </div>
        
        <div>
          <label class="form-label">Amount *</label>
          <input 
            v-model.number="form.amount" 
            type="number" 
            step="0.01" 
            class="form-input" 
            placeholder="0.00" 
            required
          >
        </div>
        
        <div>
          <label class="form-label">Description *</label>
          <input 
            v-model="form.description" 
            type="text" 
            class="form-input" 
            placeholder="Transaction description" 
            required
          >
        </div>
        
        <div>
          <label class="form-label">Category</label>
          <select v-model="form.categoryId" class="form-input">
            <option value="">Select category</option>
            <option 
              v-for="category in categories" 
              :key="category.id" 
              :value="category.id"
            >
              {{ category.name }}
            </option>
          </select>
        </div>
        
        <div>
          <label class="form-label">Type *</label>
          <select v-model="form.transactionType" class="form-input" required>
            <option value="Expense">Expense</option>
            <option value="Income">Income</option>
          </select>
        </div>
        
        <div class="flex space-x-3 pt-4">
          <button type="submit" class="btn-primary flex-1" :disabled="loading">
            <div v-if="loading" class="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2 inline-block"></div>
            {{ loading ? 'Adding...' : 'Add Transaction' }}
          </button>
          <button type="button" @click="$emit('close')" class="btn-secondary">Cancel</button>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { XMarkIcon } from '@heroicons/vue/24/outline'
import type { Account, Category } from '~/types'

const emit = defineEmits<{
  close: []
  success: []
}>()

const api = useApi()
const loading = ref(false)
const accounts = ref<Account[]>([])
const categories = ref<Category[]>([])

const form = reactive({
  accountId: '',
  amount: 0,
  description: '',
  categoryId: '',
  transactionType: 'Expense'
})

// Load accounts and categories on mount
const loadData = async () => {
  try {
    const [accountsData, categoriesData] = await Promise.all([
      api.getAccounts(),
      api.getCategories()
    ])
    accounts.value = accountsData
    categories.value = categoriesData
  } catch (error) {
    console.error('Error loading data:', error)
  }
}

const handleSubmit = async () => {
  loading.value = true
  try {
    // Create the transaction with the correct amount sign
    const amount = form.transactionType === 'Income' ? form.amount : -form.amount
    
    await api.createTransaction({
      accountId: form.accountId,
      amount: amount,
      merchant: form.description,
      transactionDate: new Date().toISOString(),
      categoryId: form.categoryId || undefined,
      description: form.description,
      notes: 'Added via Quick Add'
    })
    
    emit('success')
  } catch (error) {
    console.error('Error creating transaction:', error)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadData()
})
</script>

<style scoped>
.form-label {
  @apply block text-sm font-medium text-gray-700 mb-1;
}

.form-input {
  @apply w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm placeholder-gray-400 focus:outline-none focus:ring-blue-500 focus:border-blue-500;
}

.btn-primary {
  @apply bg-blue-600 text-white font-semibold px-4 py-2 rounded-lg hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 transition-colors disabled:opacity-50 disabled:cursor-not-allowed;
}

.btn-secondary {
  @apply bg-gray-600 text-white font-semibold px-4 py-2 rounded-lg hover:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-gray-500 focus:ring-offset-2 transition-colors;
}
</style>