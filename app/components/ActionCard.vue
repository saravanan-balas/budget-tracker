<template>
  <div 
    @click="$emit('click')"
    class="card hover:shadow-md transition-shadow cursor-pointer text-center"
  >
    <div class="w-12 h-12 rounded-lg flex items-center justify-center mx-auto mb-3" :class="bgColorClass">
      <component :is="iconComponent" class="w-6 h-6" :class="iconColorClass" />
    </div>
    <h3 class="font-medium mb-1">{{ title }}</h3>
    <p class="text-sm text-gray-600">{{ description }}</p>
  </div>
</template>

<script setup lang="ts">
import { 
  CloudArrowUpIcon,
  ChatBubbleLeftRightIcon,
  PlusIcon
} from '@heroicons/vue/24/outline'

interface Props {
  title: string
  description: string
  icon: string
  color: string
}

defineProps<Props>()
defineEmits<{
  click: []
}>()

const props = defineProps<Props>()

const iconComponents = {
  'cloud-arrow-up': CloudArrowUpIcon,
  'chat-bubble-left-right': ChatBubbleLeftRightIcon,
  'plus': PlusIcon,
}

const iconComponent = computed(() => iconComponents[props.icon as keyof typeof iconComponents])

const colorClasses = {
  blue: {
    bg: 'bg-blue-100',
    icon: 'text-blue-600',
  },
  green: {
    bg: 'bg-green-100',
    icon: 'text-green-600',
  },
  purple: {
    bg: 'bg-purple-100',
    icon: 'text-purple-600',
  }
}

const bgColorClass = computed(() => colorClasses[props.color as keyof typeof colorClasses]?.bg || 'bg-gray-100')
const iconColorClass = computed(() => colorClasses[props.color as keyof typeof colorClasses]?.icon || 'text-gray-600')
</script>