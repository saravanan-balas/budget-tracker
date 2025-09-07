<template>
  <div class="min-h-screen bg-gray-50">
    <!-- Navigation -->
    <nav class="bg-white shadow-sm border-b">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between h-16">
          <div class="flex items-center">
            <NuxtLink to="/" class="flex items-center space-x-2">
              <div class="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center">
                <span class="text-white font-bold">$</span>
              </div>
              <span class="text-xl font-semibold text-gray-900">Budget Tracker</span>
            </NuxtLink>
          </div>
          
          <div class="flex items-center space-x-6">
            <template v-if="authStore.isAuthenticated">
              <NuxtLink to="/dashboard" class="nav-link" :class="{ 'nav-link-active': $route.path === '/dashboard' }">
                Dashboard
              </NuxtLink>
              <NuxtLink to="/transactions" class="nav-link" :class="{ 'nav-link-active': $route.path === '/transactions' }">
                Transactions
              </NuxtLink>
              <NuxtLink to="/accounts" class="nav-link" :class="{ 'nav-link-active': $route.path === '/accounts' }">
                Accounts
              </NuxtLink>
              <NuxtLink to="/import" class="nav-link" :class="{ 'nav-link-active': $route.path === '/import' }">
                Import
              </NuxtLink>
              
              <!-- User Dropdown -->
              <div class="relative" ref="dropdownRef">
                <button @click="showDropdown = !showDropdown" class="flex items-center space-x-2 text-gray-700 hover:text-gray-900">
                  <span>{{ getUserName() }}</span>
                  <ChevronDownIcon class="w-4 h-4" />
                </button>
                <div v-show="showDropdown" class="absolute right-0 mt-2 w-48 bg-white rounded-md shadow-lg z-50 border">
                  <div class="py-1">
                    <NuxtLink to="/profile" class="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100">
                      Profile
                    </NuxtLink>
                    <NuxtLink to="/settings" class="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100">
                      Settings
                    </NuxtLink>
                    <hr class="my-1">
                    <button @click="handleLogout" class="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100">
                      Sign out
                    </button>
                  </div>
                </div>
              </div>
            </template>
            <template v-else>
              <NuxtLink to="/auth/login" class="nav-link">
                Sign In
              </NuxtLink>
              <NuxtLink to="/auth/register" class="bg-blue-600 text-white px-4 py-2 rounded-md text-sm font-medium hover:bg-blue-700">
                Get Started
              </NuxtLink>
            </template>
          </div>
        </div>
      </div>
    </nav>

    <!-- Main Content -->
    <main class="max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8">
      <slot />
    </main>

    <!-- Footer -->
    <footer class="bg-white border-t mt-12">
      <div class="max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8">
        <div class="text-center text-gray-500 text-sm">
          &copy; 2025 Budget Tracker. Made with ❤️ for better financial health.
        </div>
      </div>
    </footer>

    <!-- Loading Overlay -->
    <div v-if="loading" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div class="bg-white p-6 rounded-lg">
        <div class="flex items-center space-x-3">
          <div class="animate-spin rounded-full h-6 w-6 border-b-2 border-blue-600"></div>
          <span>Loading...</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ChevronDownIcon } from '@heroicons/vue/24/outline'

const authStore = useAuthStore()
const showDropdown = ref(false)
const loading = ref(false)
const dropdownRef = ref(null)

// Close dropdown when clicking outside
onClickOutside(dropdownRef, () => {
  showDropdown.value = false
})

// Initialize auth on mount
onMounted(() => {
  authStore.initializeAuth()
})

const handleLogout = async () => {
  loading.value = true
  try {
    await authStore.logout()
  } finally {
    loading.value = false
    showDropdown.value = false
  }
}

// Watch for route changes to close dropdown
watch(() => useRoute().path, () => {
  showDropdown.value = false
})

// Helper function to get user display name
const getUserName = () => {
  if (authStore.user) {
    return authStore.user.firstName ? `${authStore.user.firstName} ${authStore.user.lastName}` : authStore.user.email
  }
  return 'User'
}
</script>