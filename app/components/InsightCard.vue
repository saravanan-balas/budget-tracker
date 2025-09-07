<template>
  <div class="p-4 rounded-lg border-l-4" :class="containerClasses">
    <div class="flex items-center mb-2">
      <component :is="iconComponent" class="w-5 h-5 mr-2" :class="iconClasses" />
      <h4 class="font-semibold" :class="titleClasses">{{ title }}</h4>
    </div>
    <p :class="messageClasses">{{ message }}</p>
  </div>
</template>

<script setup lang="ts">
import { 
  LightBulbIcon,
  CheckCircleIcon,
  ExclamationTriangleIcon
} from '@heroicons/vue/24/outline'

interface Props {
  type: 'pattern' | 'success' | 'warning'
  title: string
  message: string
}

const props = defineProps<Props>()

const iconComponents = {
  pattern: LightBulbIcon,
  success: CheckCircleIcon,
  warning: ExclamationTriangleIcon,
}

const iconComponent = computed(() => iconComponents[props.type])

const typeClasses = {
  pattern: {
    container: 'bg-blue-50 border-blue-400',
    icon: 'text-blue-600',
    title: 'text-blue-900',
    message: 'text-blue-800'
  },
  success: {
    container: 'bg-green-50 border-green-400',
    icon: 'text-green-600',
    title: 'text-green-900',
    message: 'text-green-800'
  },
  warning: {
    container: 'bg-yellow-50 border-yellow-400',
    icon: 'text-yellow-600',
    title: 'text-yellow-900',
    message: 'text-yellow-800'
  }
}

const containerClasses = computed(() => typeClasses[props.type].container)
const iconClasses = computed(() => typeClasses[props.type].icon)
const titleClasses = computed(() => typeClasses[props.type].title)
const messageClasses = computed(() => typeClasses[props.type].message)
</script>