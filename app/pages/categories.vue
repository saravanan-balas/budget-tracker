<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900">Categories</h1>
        <p class="text-gray-600">Manage your income and expense categories</p>
      </div>
      <div class="flex space-x-3">
        <button 
          v-if="categories.length === 0"
          @click="seedDefaultCategories"
          :disabled="loading"
          class="btn-secondary"
        >
          <span v-if="loading" class="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2 inline-block"></span>
          Load Default Categories
        </button>
        <button 
          @click="openAddModal"
          class="btn-primary"
        >
          <PlusIcon class="w-4 h-4 mr-2" />
          Add Category
        </button>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
      <span class="ml-3 text-gray-600">Loading categories...</span>
    </div>

    <!-- Categories Content -->
    <div v-else class="space-y-8">
      <!-- Income Categories -->
      <div>
        <div class="flex items-center mb-4">
          <div class="w-3 h-3 bg-green-500 rounded-full mr-3"></div>
          <h2 class="text-lg font-semibold text-gray-900">Income Categories</h2>
          <span class="ml-2 text-sm text-gray-500">({{ incomeCategories.length }})</span>
        </div>
        <div v-if="incomeCategories.length === 0" class="text-center py-8 text-gray-500">
          <p>No income categories yet. <button @click="openAddModal" class="text-blue-600 hover:underline">Add one</button></p>
        </div>
        <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <CategoryCard 
            v-for="category in incomeCategories" 
            :key="category.id"
            :category="category"
            @edit="openEditModal"
            @delete="handleDelete"
          />
        </div>
      </div>

      <!-- Expense Categories -->
      <div>
        <div class="flex items-center mb-4">
          <div class="w-3 h-3 bg-red-500 rounded-full mr-3"></div>
          <h2 class="text-lg font-semibold text-gray-900">Expense Categories</h2>
          <span class="ml-2 text-sm text-gray-500">({{ expenseCategories.length }})</span>
        </div>
        <div v-if="expenseCategories.length === 0" class="text-center py-8 text-gray-500">
          <p>No expense categories yet. <button @click="openAddModal" class="text-blue-600 hover:underline">Add one</button></p>
        </div>
        <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <CategoryCard 
            v-for="category in expenseCategories" 
            :key="category.id"
            :category="category"
            @edit="openEditModal"
            @delete="handleDelete"
          />
        </div>
      </div>

      <!-- Other Categories (Transfer, Savings) -->
      <div v-if="otherCategories.length > 0">
        <div class="flex items-center mb-4">
          <div class="w-3 h-3 bg-gray-500 rounded-full mr-3"></div>
          <h2 class="text-lg font-semibold text-gray-900">Other Categories</h2>
          <span class="ml-2 text-sm text-gray-500">({{ otherCategories.length }})</span>
        </div>
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <CategoryCard 
            v-for="category in otherCategories" 
            :key="category.id"
            :category="category"
            @edit="openEditModal"
            @delete="handleDelete"
          />
        </div>
      </div>
    </div>

    <!-- Add/Edit Category Modal -->
    <AddCategoryModal 
      v-if="showModal"
      :category="editingCategory"
      @close="closeModal"
      @success="handleModalSuccess"
    />
  </div>
</template>

<script setup lang="ts">
import { PlusIcon } from '@heroicons/vue/24/outline'
import type { Category } from '~/types'

definePageMeta({
  layout: 'default',
  middleware: 'admin'
})

// State
const loading = ref(true)
const categories = ref<Category[]>([])
const showModal = ref(false)
const editingCategory = ref<Category | null>(null)

// API
const api = useApi()

// Computed properties
const incomeCategories = computed(() => 
  categories.value.filter(c => c.type === 'Income').sort((a, b) => a.name.localeCompare(b.name))
)

const expenseCategories = computed(() => 
  categories.value.filter(c => c.type === 'Expense').sort((a, b) => a.name.localeCompare(b.name))
)

const otherCategories = computed(() => 
  categories.value.filter(c => !['Income', 'Expense'].includes(c.type)).sort((a, b) => a.name.localeCompare(b.name))
)

// Load categories
const loadCategories = async () => {
  try {
    loading.value = true
    const categoriesData = await api.getCategories()
    categories.value = categoriesData
  } catch (error) {
    console.error('Error loading categories:', error)
  } finally {
    loading.value = false
  }
}

// Modal functions
const openAddModal = () => {
  editingCategory.value = null
  showModal.value = true
}

const openEditModal = (category: Category) => {
  editingCategory.value = category
  showModal.value = true
}

const closeModal = () => {
  showModal.value = false
  editingCategory.value = null
}

const handleModalSuccess = () => {
  closeModal()
  loadCategories()
}

// Delete category
const handleDelete = async (category: Category) => {
  if (!confirm(`Are you sure you want to delete "${category.name}"? This will remove it from all transactions.`)) {
    return
  }

  try {
    await api.deleteCategory(category.id)
    loadCategories()
  } catch (error) {
    console.error('Error deleting category:', error)
    alert('Failed to delete category. Please try again.')
  }
}

// Seed default categories
const seedDefaultCategories = async () => {
  try {
    loading.value = true
    await api.seedDefaultCategories()
    loadCategories()
  } catch (error: any) {
    console.error('Error seeding default categories:', error)
    if (error.message?.includes('already has categories')) {
      // User already has categories, just reload
      loadCategories()
    } else {
      alert('Failed to load default categories. Please try again.')
    }
  } finally {
    loading.value = false
  }
}

// Load categories on mount
onMounted(() => {
  loadCategories()
})
</script>

<style scoped>
.btn-primary {
  @apply bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 transition-colors flex items-center;
}
</style>
