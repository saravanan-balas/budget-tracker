export default defineNuxtRouteMiddleware((to, from) => {
  const authStore = useAuthStore()
  
  // Initialize auth if not already done
  if (!authStore.isAuthenticated) {
    authStore.initializeAuth()
  }
  
  // If not authenticated, redirect to login
  if (!authStore.isAuthenticated) {
    return navigateTo('/auth/login')
  }
})