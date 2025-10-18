<template>
  <div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
    <div class="bg-white rounded-lg shadow-xl max-w-md w-full mx-4">
      <!-- Header -->
      <div class="flex items-center justify-between p-6 border-b border-gray-200">
        <h2 class="text-lg font-semibold text-gray-900">
          {{ editingCategory ? 'Edit Category' : 'Add New Category' }}
        </h2>
        <button @click="$emit('close')" class="text-gray-400 hover:text-gray-600">
          <XMarkIcon class="w-6 h-6" />
        </button>
      </div>

      <!-- Form -->
      <form @submit.prevent="handleSubmit" class="p-6 space-y-4">
        <!-- Category Name -->
        <div>
          <label class="form-label">Category Name *</label>
          <input 
            v-model="form.name"
            type="text" 
            class="form-input" 
            placeholder="e.g., Groceries, Salary"
            required
            :disabled="loading"
          >
        </div>

        <!-- Category Type -->
        <div>
          <label class="form-label">Category Type *</label>
          <select v-model="form.type" class="form-input" required :disabled="loading">
            <option value="">Select type</option>
            <option value="Income">Income</option>
            <option value="Expense">Expense</option>
            <option value="Transfer">Transfer</option>
            <option value="Savings">Savings</option>
          </select>
        </div>

        <!-- Icon -->
        <div>
          <label class="form-label">Icon</label>
          <div class="grid grid-cols-8 gap-2">
            <button
              v-for="icon in popularIcons"
              :key="icon"
              type="button"
              @click="form.icon = icon"
              class="w-10 h-10 rounded-lg border-2 flex items-center justify-center text-lg hover:bg-gray-50 transition-colors"
              :class="{ 'border-blue-500 bg-blue-50': form.icon === icon, 'border-gray-200': form.icon !== icon }"
            >
              {{ icon }}
            </button>
          </div>
          <input 
            v-model="form.icon"
            type="text" 
            class="form-input mt-2" 
            placeholder="Or type emoji"
            maxlength="2"
          >
        </div>

        <!-- Color -->
        <div>
          <label class="form-label">Color</label>
          <div class="grid grid-cols-6 gap-2">
            <button
              v-for="color in colorOptions"
              :key="color"
              type="button"
              @click="form.color = color"
              class="w-8 h-8 rounded-full border-2 hover:scale-110 transition-transform"
              :style="{ backgroundColor: color }"
              :class="{ 'border-gray-800': form.color === color, 'border-gray-300': form.color !== color }"
            ></button>
          </div>
        </div>

        <!-- Budget Amount (Optional) -->
        <div>
          <label class="form-label">Monthly Budget (Optional)</label>
          <input 
            v-model.number="form.budgetAmount"
            type="number" 
            step="0.01"
            min="0"
            class="form-input" 
            placeholder="0.00"
            :disabled="loading"
          >
        </div>

        <!-- Error Message -->
        <div v-if="error" class="bg-red-50 border border-red-200 rounded-md p-3">
          <p class="text-sm text-red-600">{{ error }}</p>
        </div>

        <!-- Actions -->
        <div class="flex space-x-3 pt-4">
          <button 
            type="submit" 
            class="btn-primary flex-1" 
            :disabled="loading"
          >
            <div v-if="loading" class="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2 inline-block"></div>
            {{ loading ? 'Saving...' : (editingCategory ? 'Update Category' : 'Add Category') }}
          </button>
          <button 
            type="button" 
            @click="$emit('close')" 
            class="btn-secondary"
            :disabled="loading"
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { XMarkIcon } from '@heroicons/vue/24/outline'
import type { Category } from '~/types'

interface Props {
  category?: Category | null
}

const props = defineProps<Props>()

const emit = defineEmits<{
  close: []
  success: []
}>()

// State
const loading = ref(false)
const error = ref('')

// Form data
const form = reactive({
  name: '',
  type: '',
  icon: '',
  color: '#3b82f6', // Default blue
  budgetAmount: null as number | null
})

// Popular icons for quick selection
const popularIcons = [
  '💰', '💵', '🏦', '📈', // Income
  '🍔', '🛒', '⛽', '🛍️', // Expense
  '🎬', '🏥', '📱', '🏠', // More expense
  '✈️', '📚', '💅', '🛡️', // More expense
  '↔️', '💸', '🎯', '⭐'  // Transfer/Savings/Other
]

// Color options
const colorOptions = [
  '#ef4444', '#f97316', '#eab308', '#22c55e', '#10b981', '#06b6d4', // Red to Cyan
  '#3b82f6', '#6366f1', '#8b5cf6', '#a855f7', '#ec4899', '#f43f5e', // Blue to Pink
  '#6b7280', '#374151', '#1f2937', '#111827' // Grays
]

// API
const api = useApi()

// Initialize form when editing
watch(() => props.category, (category) => {
  if (category) {
    form.name = category.name
    form.type = category.type
    form.icon = category.icon || ''
    form.color = category.color || '#3b82f6'
    form.budgetAmount = category.budgetAmount || null
  } else {
    // Reset form for new category
    form.name = ''
    form.type = ''
    form.icon = ''
    form.color = '#3b82f6'
    form.budgetAmount = null
  }
  error.value = ''
}, { immediate: true })

// Handle form submission
const handleSubmit = async () => {
  if (!form.name.trim() || !form.type) {
    error.value = 'Please fill in all required fields'
    return
  }

  loading.value = true
  error.value = ''

  try {
    if (props.category) {
      // Update existing category
      await api.updateCategory(props.category.id, {
        name: form.name.trim(),
        type: form.type,
        icon: form.icon || undefined,
        color: form.color,
        budgetAmount: form.budgetAmount || undefined
      })
      emit('success')
    } else {
      // Create new category
      await api.createCategory({
        name: form.name.trim(),
        type: form.type,
        icon: form.icon || undefined,
        color: form.color,
        budgetAmount: form.budgetAmount || undefined
      })
      emit('success')
    }
  } catch (err: any) {
    console.error('Error saving category:', err)
    error.value = err.message || 'Failed to save category. Please try again.'
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
  @apply bg-blue-600 text-white font-semibold px-4 py-2 rounded-lg hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center;
}

.btn-secondary {
  @apply bg-gray-600 text-white font-semibold px-4 py-2 rounded-lg hover:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-gray-500 focus:ring-offset-2 transition-colors disabled:opacity-50 disabled:cursor-not-allowed;
}
</style>
