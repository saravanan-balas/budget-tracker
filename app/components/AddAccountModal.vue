<template>
  <div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50" @click="$emit('close')">
    <div class="bg-white rounded-lg p-6 w-full max-w-md mx-4" @click.stop>
      <div class="flex justify-between items-center mb-4">
        <h3 class="text-lg font-semibold">Add Account</h3>
        <button @click="$emit('close')" class="text-gray-400 hover:text-gray-600">
          <XMarkIcon class="w-6 h-6" />
        </button>
      </div>
      
      <form @submit.prevent="handleSubmit" class="space-y-4">
        <div>
          <label class="form-label">Account Name *</label>
          <input 
            v-model="form.name" 
            type="text" 
            class="form-input" 
            placeholder="e.g., Main Checking" 
            required
          >
        </div>
        
        <div>
          <label class="form-label">Account Type *</label>
          <select v-model="form.type" class="form-input" required>
            <option value="">Select type</option>
            <option value="Checking">Checking</option>
            <option value="Savings">Savings</option>
            <option value="CreditCard">Credit Card</option>
            <option value="Investment">Investment</option>
            <option value="Loan">Loan</option>
            <option value="Cash">Cash</option>
          </select>
        </div>
        
        <div>
          <label class="form-label">Initial Balance</label>
          <input 
            v-model.number="form.initialBalance" 
            type="number" 
            step="0.01" 
            class="form-input" 
            placeholder="0.00"
          >
        </div>
        
        <div>
          <label class="form-label">Institution</label>
          <input 
            v-model="form.institution" 
            type="text" 
            class="form-input" 
            placeholder="e.g., Bank of America"
          >
        </div>
        
        <div>
          <label class="form-label">Account Number (Last 4 digits)</label>
          <input 
            v-model="form.accountNumber" 
            type="text" 
            class="form-input" 
            placeholder="****1234" 
            maxlength="8"
          >
        </div>
        
        <div>
          <label class="form-label">Currency</label>
          <select v-model="form.currency" class="form-input">
            <option value="USD">USD - US Dollar</option>
            <option value="EUR">EUR - Euro</option>
            <option value="GBP">GBP - British Pound</option>
            <option value="CAD">CAD - Canadian Dollar</option>
            <option value="AUD">AUD - Australian Dollar</option>
          </select>
        </div>
        
        <div class="flex space-x-3 pt-4">
          <button type="submit" class="btn-primary flex-1" :disabled="loading">
            <div v-if="loading" class="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2 inline-block"></div>
            {{ loading ? 'Adding...' : 'Add Account' }}
          </button>
          <button type="button" @click="$emit('close')" class="btn-secondary">Cancel</button>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { XMarkIcon } from '@heroicons/vue/24/outline'
import type { CreateAccount } from '~/types'

const emit = defineEmits<{
  close: []
  success: []
}>()

const api = useApi()
const loading = ref(false)

const form = reactive<CreateAccount>({
  name: '',
  type: '',
  initialBalance: 0,
  currency: 'USD',
  institution: '',
  accountNumber: ''
})

const handleSubmit = async () => {
  loading.value = true
  try {
    const accountData: CreateAccount = {
      name: form.name,
      type: form.type,
      initialBalance: form.initialBalance || 0,
      currency: form.currency
    }
    
    // Only include optional fields if they have values
    if (form.institution?.trim()) {
      accountData.institution = form.institution.trim()
    }
    
    if (form.accountNumber?.trim()) {
      accountData.accountNumber = form.accountNumber.trim()
    }
    
    await api.createAccount(accountData)
    emit('success')
  } catch (error) {
    console.error('Error creating account:', error)
    alert('Failed to create account. Please try again.')
  } finally {
    loading.value = false
  }
}
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