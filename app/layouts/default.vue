<template>
  <div class="min-h-screen bg-gray-50">
    <!-- Navigation -->
    <nav class="bg-white shadow-sm border-b relative z-40">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between h-16">
          <!-- Logo -->
          <div class="flex items-center">
            <NuxtLink to="/" class="flex items-center">
              <img src="/logo.svg" alt="BudgetVu" class="h-12" />
            </NuxtLink>
          </div>

          <!-- Desktop nav -->
          <div class="hidden md:flex items-center space-x-6">
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
              <NuxtLink
                v-if="authStore.user?.isAdmin"
                to="/categories"
                class="nav-link"
                :class="{ 'nav-link-active': $route.path === '/categories' }"
              >
                Categories
              </NuxtLink>
              <NuxtLink to="/import" class="nav-link" :class="{ 'nav-link-active': $route.path === '/import' }">
                Import
              </NuxtLink>
              <NuxtLink to="/chat" class="nav-link" :class="{ 'nav-link-active': $route.path === '/chat' }">
                Ask AI
              </NuxtLink>
              <NuxtLink
                v-if="authStore.user?.isAdmin"
                to="/admin/logs"
                class="nav-link"
                :class="{ 'nav-link-active': $route.path === '/admin/logs' }"
              >
                Logs
              </NuxtLink>

              <!-- User Dropdown (desktop) -->
              <div class="relative" ref="dropdownRef">
                <button @click="showDropdown = !showDropdown" class="flex items-center space-x-2 text-gray-700 hover:text-gray-900">
                  <span>{{ getUserName() }}</span>
                  <ChevronDownIcon class="w-4 h-4" />
                </button>
                <div v-show="showDropdown" class="absolute right-0 mt-2 w-48 bg-white rounded-md shadow-lg z-50 border">
                  <div class="py-1">
                    <NuxtLink to="/settings/profile" class="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100">Profile</NuxtLink>
                    <NuxtLink to="/settings" class="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100">Settings</NuxtLink>
                    <hr class="my-1">
                    <button @click="handleLogout" class="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100">Sign out</button>
                  </div>
                </div>
              </div>
            </template>
            <template v-else>
              <NuxtLink to="/auth/login" class="nav-link">Sign In</NuxtLink>
              <NuxtLink to="/auth/register" class="bg-blue-600 text-white px-4 py-2 rounded-md text-sm font-medium hover:bg-blue-700">
                Get Started
              </NuxtLink>
            </template>
          </div>

          <!-- Mobile right side -->
          <div class="flex md:hidden items-center gap-3">
            <template v-if="!authStore.isAuthenticated">
              <NuxtLink to="/auth/login" class="text-sm font-medium text-blue-600">Sign In</NuxtLink>
            </template>
            <button
              @click="showMobileMenu = !showMobileMenu"
              class="p-2 rounded-md text-gray-600 hover:text-gray-900 hover:bg-gray-100 transition-colors"
              aria-label="Toggle menu"
            >
              <svg v-if="!showMobileMenu" class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16"/>
              </svg>
              <svg v-else class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>
        </div>
      </div>

      <!-- Mobile menu drawer -->
      <div v-show="showMobileMenu" class="md:hidden border-t bg-white shadow-lg">
        <template v-if="authStore.isAuthenticated">
          <div class="px-4 py-3 space-y-1">
            <NuxtLink to="/dashboard" @click="showMobileMenu = false"
              class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors"
              :class="$route.path === '/dashboard' ? 'bg-blue-50 text-blue-700' : 'text-gray-700 hover:bg-gray-50'">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"/>
              </svg>
              Dashboard
            </NuxtLink>

            <NuxtLink to="/transactions" @click="showMobileMenu = false"
              class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors"
              :class="$route.path === '/transactions' ? 'bg-blue-50 text-blue-700' : 'text-gray-700 hover:bg-gray-50'">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"/>
              </svg>
              Transactions
            </NuxtLink>

            <NuxtLink to="/accounts" @click="showMobileMenu = false"
              class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors"
              :class="$route.path === '/accounts' ? 'bg-blue-50 text-blue-700' : 'text-gray-700 hover:bg-gray-50'">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z"/>
              </svg>
              Accounts
            </NuxtLink>

            <NuxtLink
              v-if="authStore.user?.isAdmin"
              to="/categories" @click="showMobileMenu = false"
              class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z"/>
              </svg>
              Categories
            </NuxtLink>
          </div>

          <!-- Import & Ask AI -->
          <div class="px-4 pb-3 pt-1 space-y-1 border-t border-gray-100 mt-1">
            <NuxtLink to="/import" @click="showMobileMenu = false"
              class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors"
              :class="$route.path === '/import' ? 'bg-blue-50 text-blue-700' : 'text-gray-700 hover:bg-gray-50'">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/>
              </svg>
              Import
            </NuxtLink>
            <NuxtLink to="/chat" @click="showMobileMenu = false"
              class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors"
              :class="$route.path === '/chat' ? 'bg-blue-50 text-blue-700' : 'text-gray-700 hover:bg-gray-50'">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z"/>
              </svg>
              Ask AI
            </NuxtLink>
          </div>

          <!-- User section -->
          <div class="px-4 py-3 border-t border-gray-100 space-y-1">
            <p class="text-xs text-gray-400 px-3 mb-1">Signed in as {{ getUserName() }}</p>
            <NuxtLink to="/settings/profile" @click="showMobileMenu = false"
              class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-gray-700 hover:bg-gray-50 transition-colors">
              Profile & Settings
            </NuxtLink>
            <button @click="handleLogout"
              class="w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-red-600 hover:bg-red-50 transition-colors">
              Sign out
            </button>
          </div>
        </template>
      </div>
    </nav>

    <!-- Main Content -->
    <main class="max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8">
      <slot />
    </main>

    <!-- Footer -->
    <footer class="bg-white border-t mt-12">
      <div class="max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8">
        <div class="flex flex-col sm:flex-row items-center justify-between gap-2 text-sm text-gray-400">
          <span>&copy; 2025-2026 BudgetVu. All rights reserved.</span>
          <div class="flex items-center gap-4">
            <NuxtLink to="/terms" class="hover:text-gray-600 transition-colors">Terms of Service</NuxtLink>
            <NuxtLink to="/privacy" class="hover:text-gray-600 transition-colors">Privacy Policy</NuxtLink>
          </div>
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
const showMobileMenu = ref(false)
const loading = ref(false)
const dropdownRef = ref(null)

onClickOutside(dropdownRef, () => {
  showDropdown.value = false
})

onMounted(() => {
  authStore.initializeAuth()
})

const handleLogout = async () => {
  loading.value = true
  showMobileMenu.value = false
  try {
    await authStore.logout()
  } finally {
    loading.value = false
    showDropdown.value = false
  }
}

watch(() => useRoute().path, () => {
  showDropdown.value = false
  showMobileMenu.value = false
})

const getUserName = () => {
  if (authStore.user) {
    return authStore.user.firstName ? `${authStore.user.firstName} ${authStore.user.lastName}` : authStore.user.email
  }
  return 'User'
}
</script>
