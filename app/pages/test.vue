<template>
  <div class="min-h-screen bg-green-100 flex items-center justify-center">
    <div class="text-center">
      <h1 class="text-4xl font-bold text-green-800 mb-4">🎉 Test Page Works!</h1>
      <p class="text-green-600">If you can see this, Nuxt routing is working correctly.</p>
      <div class="mt-6">
        <NuxtLink to="/auth/login" class="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
          Go to Login
        </NuxtLink>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({
  layout: false
})
</script>