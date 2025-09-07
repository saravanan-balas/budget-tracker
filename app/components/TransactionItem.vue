<template>
  <div class="flex items-center justify-between py-3 border-b border-gray-100 hover:bg-gray-50 transition-colors duration-200">
    <div class="flex items-center space-x-3 flex-1">
      <div class="w-8 h-8 rounded-full flex items-center justify-center" :class="iconBgClass">
        <span class="text-xs font-bold" :class="iconTextClass">{{ categoryIcon }}</span>
      </div>
      <div class="flex-1 min-w-0">
        <p class="font-medium text-gray-900 truncate">{{ displayMerchant }}</p>
        <p class="text-sm text-gray-500 truncate">{{ transaction.categoryName || 'Uncategorized' }}</p>
      </div>
    </div>
    <div class="flex items-center space-x-3">
      <div class="text-right">
        <p class="font-medium" :class="amountColorClass">{{ formattedAmount }}</p>
        <p class="text-sm text-gray-500">{{ formattedDate }}</p>
      </div>
      <button 
        @click="$emit('edit', transaction)"
        class="p-1 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-full transition-colors duration-200"
        title="Edit transaction"
      >
        <PencilIcon class="w-4 h-4" />
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { PencilIcon } from '@heroicons/vue/24/outline'
import type { Transaction } from '~/types'

interface Props {
  transaction: Transaction
}

defineProps<Props>()
defineEmits<{
  edit: [transaction: Transaction]
}>()

const props = defineProps<Props>()

// Computed properties
const displayMerchant = computed(() => 
  props.transaction.normalizedMerchant || props.transaction.merchant || 'Unknown Merchant'
)

const formattedAmount = computed(() => {
  const amount = props.transaction.amount
  const formatted = Math.abs(amount).toLocaleString('en-US', {
    style: 'currency',
    currency: 'USD'
  })
  return amount > 0 ? `+${formatted}` : `-${formatted}`
})

const formattedDate = computed(() => {
  const date = new Date(props.transaction.transactionDate)
  const now = new Date()
  const diff = now.getTime() - date.getTime()
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))

  if (days === 0) return 'Today'
  if (days === 1) return 'Yesterday'
  if (days < 7) return `${days} days ago`
  if (days < 30) return `${Math.floor(days / 7)} weeks ago`
  
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
})

const categoryIcon = computed(() => {
  const category = props.transaction.categoryName?.toLowerCase()
  const iconMap: { [key: string]: string } = {
    'food & dining': '🍔',
    'dining': '🍔',
    'restaurants': '🍔',
    'groceries': '🛒',
    'transportation': '⛽',
    'gas': '⛽',
    'fuel': '⛽',
    'shopping': '🛍️',
    'entertainment': '🎬',
    'bills & utilities': '📱',
    'utilities': '📱',
    'healthcare': '🏥',
    'medical': '🏥',
    'income': '💰',
    'salary': '💰',
    'deposit': '💰',
    'transfer': '↔️'
  }
  return iconMap[category || ''] || '📝'
})

const amountColorClass = computed(() => 
  props.transaction.amount > 0 ? 'text-green-600' : 'text-red-600'
)

const iconBgClass = computed(() => 
  props.transaction.amount > 0 ? 'bg-green-100' : 'bg-red-100'
)

const iconTextClass = computed(() => 
  props.transaction.amount > 0 ? 'text-green-600' : 'text-red-600'
)
</script>