<template>
  <div class="bg-white rounded-lg border border-gray-200 p-4 hover:shadow-md transition-shadow">
    <div class="flex items-center justify-between">
      <div class="flex items-center space-x-3">
        <div 
          class="w-10 h-10 rounded-lg flex items-center justify-center text-lg"
          :style="{ backgroundColor: category.color + '20', color: category.color }"
        >
          {{ category.icon || '📝' }}
        </div>
        <div>
          <h3 class="font-medium text-gray-900">{{ category.name }}</h3>
          <p class="text-sm text-gray-500">{{ category.type }}</p>
          <p v-if="transactionCount > 0" class="text-xs text-gray-400">
            {{ transactionCount }} transaction{{ transactionCount !== 1 ? 's' : '' }}
          </p>
        </div>
      </div>
      
      <div class="flex items-center space-x-1">
        <button 
          @click="$emit('edit', category)"
          class="p-1 text-gray-400 hover:text-blue-600 transition-colors"
          title="Edit category"
        >
          <PencilIcon class="w-4 h-4" />
        </button>
        <button 
          @click="$emit('delete', category)"
          class="p-1 text-gray-400 hover:text-red-600 transition-colors"
          title="Delete category"
        >
          <TrashIcon class="w-4 h-4" />
        </button>
      </div>
    </div>
    
    <!-- Budget info if available -->
    <div v-if="category.budgetAmount" class="mt-3 pt-3 border-t border-gray-100">
      <div class="flex justify-between text-sm">
        <span class="text-gray-500">Budget:</span>
        <span class="font-medium">{{ formatCurrency(category.budgetAmount) }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { PencilIcon, TrashIcon } from '@heroicons/vue/24/outline'
import type { Category } from '~/types'

interface Props {
  category: Category
  transactionCount?: number
}

const props = withDefaults(defineProps<Props>(), {
  transactionCount: 0
})

defineEmits<{
  edit: [category: Category]
  delete: [category: Category]
}>()

// Format currency
const formatCurrency = (amount: number) => {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD'
  }).format(amount)
}
</script>
