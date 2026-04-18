<template>
  <div class="min-h-screen bg-gray-50 flex items-center justify-center px-4">
    <div class="text-center max-w-md">
      <div class="w-20 h-20 bg-blue-100 rounded-2xl flex items-center justify-center mx-auto mb-6">
        <svg class="w-10 h-10 text-blue-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9.172 16.172a4 4 0 015.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
        </svg>
      </div>

      <h1 class="text-5xl font-bold text-gray-900 mb-2">{{ error?.statusCode ?? 404 }}</h1>
      <h2 class="text-xl font-semibold text-gray-700 mb-3">
        {{ error?.statusCode === 404 ? 'Page not found' : 'Something went wrong' }}
      </h2>
      <p class="text-gray-500 mb-8">
        {{ error?.statusCode === 404
          ? "The page you are looking for does not exist or may have been moved."
          : "An unexpected error occurred. Please try again." }}
      </p>

      <div class="flex gap-3 justify-center">
        <NuxtLink
          to="/"
          class="bg-blue-600 text-white px-6 py-2.5 rounded-lg font-semibold hover:bg-blue-700 transition-colors"
        >
          Go to Home
        </NuxtLink>
        <button
          @click="handleError"
          class="border border-gray-300 text-gray-700 px-6 py-2.5 rounded-lg font-semibold hover:bg-gray-50 transition-colors"
        >
          Try again
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{ error: { statusCode: number; message?: string } | null }>()

const handleError = () => clearError({ redirect: '/' })

useHead({
  title: props.error?.statusCode === 404 ? 'Page Not Found' : 'Error'
})
</script>
