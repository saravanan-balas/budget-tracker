<template>
  <div class="relative">
    <!-- Search Input -->
    <div class="relative">
      <input
        v-model="searchQuery"
        @focus="showDropdown = true"
        @blur="handleBlur"
        @keydown="handleKeydown"
        type="text"
        class="form-input pr-10"
        :placeholder="placeholder"
        :disabled="disabled"
      />
      <div class="absolute inset-y-0 right-0 flex items-center pr-3">
        <ChevronDownIcon class="w-4 h-4 text-gray-400" />
      </div>
    </div>

    <!-- Dropdown -->
    <div
      v-if="showDropdown"
      class="absolute z-50 w-full mt-1 bg-white border border-gray-300 rounded-md shadow-lg max-h-60 overflow-auto"
    >
      <!-- Quick Create Option -->
      <div
        v-if="searchQuery && !exactMatch"
        @click="handleCreateNew"
        class="px-3 py-2 text-sm text-blue-600 hover:bg-blue-50 cursor-pointer border-b border-gray-100"
      >
        <PlusIcon class="w-4 h-4 inline mr-2" />
        Create "{{ searchQuery }}"
      </div>

      <!-- Category Options -->
      <div
        v-for="(category, index) in filteredCategories"
        :key="category.id"
        @click="selectCategory(category)"
        class="px-3 py-2 text-sm hover:bg-gray-50 cursor-pointer flex items-center"
        :class="{ 'bg-blue-50': index === highlightedIndex }"
      >
        <span class="text-lg mr-3">{{ category.icon || '📝' }}</span>
        <span class="flex-1">{{ category.name }}</span>
        <span class="text-xs text-gray-500 ml-2">{{ category.type }}</span>
      </div>

      <!-- No Results -->
      <div
        v-if="filteredCategories.length === 0 && !searchQuery"
        class="px-3 py-2 text-sm text-gray-500"
      >
        No categories available
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ChevronDownIcon, PlusIcon } from '@heroicons/vue/24/outline'
import type { Category } from '~/types'

interface Props {
  modelValue?: string
  categories: Category[]
  placeholder?: string
  disabled?: boolean
  allowCreate?: boolean
}

interface Emits {
  'update:modelValue': [value: string]
  'create-category': [name: string]
}

const props = withDefaults(defineProps<Props>(), {
  placeholder: 'Select or search category...',
  disabled: false,
  allowCreate: true
})

const emit = defineEmits<Emits>()

// State
const showDropdown = ref(false)
const searchQuery = ref('')
const highlightedIndex = ref(0)

// Computed
const selectedCategory = computed(() => {
  return props.categories.find(c => c.id === props.modelValue)
})

const exactMatch = computed(() => {
  if (!searchQuery.value) return false
  return props.categories.some(c => 
    c.name.toLowerCase() === searchQuery.value.toLowerCase()
  )
})

const filteredCategories = computed(() => {
  if (!searchQuery.value) return props.categories
  
  const query = searchQuery.value.toLowerCase()
  return props.categories.filter(category =>
    category.name.toLowerCase().includes(query) ||
    category.type.toLowerCase().includes(query)
  )
})

// Methods
const selectCategory = (category: Category) => {
  emit('update:modelValue', category.id)
  searchQuery.value = category.name
  showDropdown.value = false
  highlightedIndex.value = 0
}

const handleCreateNew = () => {
  if (props.allowCreate) {
    emit('create-category', searchQuery.value)
    showDropdown.value = false
  }
}

const handleBlur = () => {
  // Delay to allow click events to fire
  setTimeout(() => {
    showDropdown.value = false
    highlightedIndex.value = 0
  }, 150)
}

const handleKeydown = (event: KeyboardEvent) => {
  if (!showDropdown.value) return

  switch (event.key) {
    case 'ArrowDown':
      event.preventDefault()
      highlightedIndex.value = Math.min(
        highlightedIndex.value + 1,
        filteredCategories.value.length - 1
      )
      break
    case 'ArrowUp':
      event.preventDefault()
      highlightedIndex.value = Math.max(highlightedIndex.value - 1, 0)
      break
    case 'Enter':
      event.preventDefault()
      if (highlightedIndex.value >= 0 && highlightedIndex.value < filteredCategories.value.length) {
        selectCategory(filteredCategories.value[highlightedIndex.value])
      } else if (searchQuery.value && !exactMatch.value && props.allowCreate) {
        handleCreateNew()
      }
      break
    case 'Escape':
      showDropdown.value = false
      highlightedIndex.value = 0
      break
  }
}

// Watch for model value changes
watch(() => props.modelValue, (newValue) => {
  if (newValue && selectedCategory.value) {
    searchQuery.value = selectedCategory.value.name
  } else if (!newValue) {
    searchQuery.value = ''
  }
}, { immediate: true })

// Reset highlighted index when search changes
watch(searchQuery, () => {
  highlightedIndex.value = 0
})
</script>

<style scoped>
.form-input {
  @apply w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm placeholder-gray-400 focus:outline-none focus:ring-blue-500 focus:border-blue-500;
}
</style>
