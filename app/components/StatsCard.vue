<template>
  <div class="card">
    <div class="flex items-center justify-between">
      <div>
        <p class="text-sm font-medium text-gray-500">{{ title }}</p>
        <p class="text-2xl font-bold" :class="textColorClass">{{ value }}</p>
      </div>
      <div class="w-10 h-10 rounded-lg flex items-center justify-center" :class="bgColorClass">
        <component :is="iconComponent" class="w-5 h-5" :class="iconColorClass" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { 
  TrendingUpIcon, 
  TrendingDownIcon, 
  CurrencyDollarIcon,
  DocumentTextIcon 
} from '@heroicons/vue/24/outline'

interface Props {
  title: string
  value: string
  icon: string
  color: string
}

const props = defineProps<Props>()

const iconComponents = {
  'trending-up': TrendingUpIcon,
  'trending-down': TrendingDownIcon,
  'currency-dollar': CurrencyDollarIcon,
  'document-text': DocumentTextIcon,
}

const iconComponent = computed(() => iconComponents[props.icon as keyof typeof iconComponents])

const colorClasses = {
  red: {
    bg: 'bg-red-100',
    icon: 'text-red-600',
    text: 'text-gray-900'
  },
  green: {
    bg: 'bg-green-100',
    icon: 'text-green-600',
    text: 'text-green-600'
  },
  blue: {
    bg: 'bg-blue-100',
    icon: 'text-blue-600',
    text: 'text-blue-600'
  },
  purple: {
    bg: 'bg-purple-100',
    icon: 'text-purple-600',
    text: 'text-gray-900'
  }
}

const bgColorClass = computed(() => colorClasses[props.color as keyof typeof colorClasses]?.bg || 'bg-gray-100')
const iconColorClass = computed(() => colorClasses[props.color as keyof typeof colorClasses]?.icon || 'text-gray-600')
const textColorClass = computed(() => colorClasses[props.color as keyof typeof colorClasses]?.text || 'text-gray-900')
</script>