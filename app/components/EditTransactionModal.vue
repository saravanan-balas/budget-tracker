<template>
  <div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50" @click="$emit('close')">
    <div class="bg-white rounded-lg p-6 w-full max-w-md mx-4" @click.stop>
      <div class="flex justify-between items-center mb-4">
        <h3 class="text-lg font-semibold">Edit Transaction</h3>
        <button @click="$emit('close')" class="text-gray-400 hover:text-gray-600">
          <XMarkIcon class="w-6 h-6" />
        </button>
      </div>
      
      <!-- Read-only transaction details -->
      <div class="bg-gray-50 p-4 rounded-lg mb-4">
        <h4 class="font-medium text-gray-900 mb-2">Transaction Details</h4>
        <div class="space-y-1 text-sm">
          <div>
            <span class="text-gray-600">Amount:</span> 
            <span class="font-medium">{{ formatCurrency(transaction.amount) }}</span>
          </div>
          <div>
            <span class="text-gray-600">Date:</span> 
            <span class="font-medium">{{ formatDate(transaction.transactionDate) }}</span>
          </div>
          <div>
            <span class="text-gray-600">Account:</span> 
            <span class="font-medium">{{ transaction.accountName || 'Unknown Account' }}</span>
          </div>
        </div>
      </div>

      <form @submit.prevent="handleSubmit" class="space-y-4">
        <div>
          <label class="form-label">Merchant/Description</label>
          <input 
            v-model="form.merchant" 
            type="text" 
            class="form-input" 
            placeholder="Update merchant name"
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
          <label class="form-label">Notes</label>
          <textarea 
            v-model="form.notes" 
            class="form-input" 
            rows="2" 
            placeholder="Add notes about this transaction"
          ></textarea>
        </div>
        
        <div>
          <label class="form-label">Tags</label>
          <input 
            v-model="form.tags" 
            type="text" 
            class="form-input" 
            placeholder="comma, separated, tags"
          >
        </div>
        
        <div class="flex space-x-3 pt-4">
          <button type="submit" class="btn-primary flex-1" :disabled="loading">
            {{ loading ? 'Updating...' : 'Update Transaction' }}
          </button>
          <button 
            type="button" 
            @click="handleDelete" 
            :disabled="loading"
            class="btn-secondary bg-red-600 text-white hover:bg-red-700"
          >
            Delete
          </button>
          <button type="button" @click="$emit('close')" class="btn-secondary">Cancel</button>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { XMarkIcon } from '@heroicons/vue/24/outline'
import type { Transaction, Category } from '~/types'

interface Props {
  transaction: Transaction
}

const props = defineProps<Props>()

const emit = defineEmits<{
  close: []
  success: []
}>()

const api = useApi()
const loading = ref(false)
const categories = ref<Category[]>([])

const form = reactive({
  merchant: props.transaction.merchant || '',
  categoryId: props.transaction.categoryId || '',
  notes: props.transaction.notes || '',
  tags: props.transaction.tags || ''
})

// Load categories on mount
const loadCategories = async () => {
  try {
    categories.value = await api.getCategories()
  } catch (error) {
    console.error('Error loading categories:', error)
  }
}

onMounted(() => {
  loadCategories()
})

const handleSubmit = async () => {
  loading.value = true
  try {
    await api.updateTransaction(props.transaction.id, {
      merchant: form.merchant || undefined,
      categoryId: form.categoryId || undefined,
      notes: form.notes || undefined,
      tags: form.tags || undefined
    })
    
    emit('success')
  } catch (error) {
    console.error('Error updating transaction:', error)
  } finally {
    loading.value = false
  }
}

const handleDelete = async () => {
  if (!confirm('Are you sure you want to delete this transaction?')) return
  
  loading.value = true
  try {
    await api.deleteTransaction(props.transaction.id)
    emit('success')
  } catch (error) {
    console.error('Error deleting transaction:', error)
  } finally {
    loading.value = false
  }
}

const formatCurrency = (amount: number) => {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD'
  }).format(amount)
}

const formatDate = (dateString: string) => {
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  })
}
</script>