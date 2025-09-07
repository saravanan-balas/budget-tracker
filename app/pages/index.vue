<template>
  <div>
    <!-- Hero Section -->
    <div class="bg-gradient-to-br from-blue-50 to-indigo-100 -mx-4 -mt-6 px-4 pt-12 pb-16 mb-12 rounded-b-3xl">
      <div class="text-center max-w-4xl mx-auto">
        <h1 class="text-5xl font-bold text-gray-900 mb-6">
          Smart Financial Management with
          <span class="text-blue-600">AI Insights</span>
        </h1>
        <p class="text-xl text-gray-600 mb-8 max-w-2xl mx-auto">
          Import bank statements, get intelligent categorization, and chat with your financial data. 
          Understand your spending patterns like never before.
        </p>
        <div class="flex gap-4 justify-center">
          <template v-if="authStore.isAuthenticated">
            <NuxtLink to="/dashboard" class="bg-blue-600 text-white px-8 py-3 rounded-lg font-semibold text-lg hover:bg-blue-700 transition-colors">
              Go to Dashboard
            </NuxtLink>
          </template>
          <template v-else>
            <NuxtLink to="/auth/register" class="bg-blue-600 text-white px-8 py-3 rounded-lg font-semibold text-lg hover:bg-blue-700 transition-colors">
              Get Started Free
            </NuxtLink>
            <NuxtLink to="/auth/login" class="bg-white text-blue-600 px-8 py-3 rounded-lg font-semibold text-lg hover:bg-gray-100 transition-colors border border-blue-600">
              Sign In
            </NuxtLink>
          </template>
        </div>
      </div>
    </div>

    <!-- Features Grid -->
    <div class="grid md:grid-cols-3 gap-8 mb-16">
      <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 text-center">
        <div class="w-12 h-12 bg-blue-100 rounded-lg flex items-center justify-center mx-auto mb-4">
          <svg class="w-6 h-6 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"></path>
          </svg>
        </div>
        <h3 class="text-xl font-semibold mb-2">Smart Import</h3>
        <p class="text-gray-600">Upload CSV/PDF bank statements. AI automatically categorizes and deduplicates transactions.</p>
      </div>
      
      <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 text-center">
        <div class="w-12 h-12 bg-green-100 rounded-lg flex items-center justify-center mx-auto mb-4">
          <svg class="w-6 h-6 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z"></path>
          </svg>
        </div>
        <h3 class="text-xl font-semibold mb-2">AI Chat</h3>
        <p class="text-gray-600">Ask natural questions: "How much did I spend on dining last month?" Get instant insights.</p>
      </div>
      
      <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 text-center">
        <div class="w-12 h-12 bg-purple-100 rounded-lg flex items-center justify-center mx-auto mb-4">
          <svg class="w-6 h-6 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"></path>
          </svg>
        </div>
        <h3 class="text-xl font-semibold mb-2">Smart Analytics</h3>
        <p class="text-gray-600">Detect recurring subscriptions, unusual spending, and get personalized financial coaching.</p>
      </div>
    </div>

    <!-- How It Works -->
    <div class="text-center mb-16">
      <h2 class="text-3xl font-bold text-gray-900 mb-12">How It Works</h2>
      <div class="grid md:grid-cols-4 gap-8">
        <div class="relative">
          <div class="w-16 h-16 bg-blue-600 text-white rounded-full flex items-center justify-center mx-auto mb-4 text-xl font-bold">1</div>
          <h3 class="font-semibold mb-2">Create Account</h3>
          <p class="text-gray-600 text-sm">Sign up in 30 seconds with your email</p>
        </div>
        
        <div class="relative">
          <div class="w-16 h-16 bg-blue-600 text-white rounded-full flex items-center justify-center mx-auto mb-4 text-xl font-bold">2</div>
          <h3 class="font-semibold mb-2">Import Statements</h3>
          <p class="text-gray-600 text-sm">Upload bank CSV/PDF files from any bank</p>
        </div>
        
        <div class="relative">
          <div class="w-16 h-16 bg-blue-600 text-white rounded-full flex items-center justify-center mx-auto mb-4 text-xl font-bold">3</div>
          <h3 class="font-semibold mb-2">AI Processing</h3>
          <p class="text-gray-600 text-sm">Smart categorization and duplicate detection</p>
        </div>
        
        <div class="relative">
          <div class="w-16 h-16 bg-blue-600 text-white rounded-full flex items-center justify-center mx-auto mb-4 text-xl font-bold">4</div>
          <h3 class="font-semibold mb-2">Get Insights</h3>
          <p class="text-gray-600 text-sm">Chat with your data for instant answers</p>
        </div>
      </div>
    </div>

    <!-- CTA Section -->
    <div v-if="!authStore.isAuthenticated" class="bg-blue-600 text-white rounded-2xl p-12 text-center">
      <h2 class="text-3xl font-bold mb-4">Ready to Take Control of Your Finances?</h2>
      <p class="text-xl text-blue-100 mb-8">Join thousands of users who've transformed their financial understanding with AI.</p>
      <NuxtLink to="/auth/register" class="bg-white text-blue-600 px-8 py-3 rounded-lg font-semibold text-lg hover:bg-gray-100 transition-colors">
        Start Free Today
      </NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
const authStore = useAuthStore()

// Initialize auth when component mounts
onMounted(() => {
  authStore.initializeAuth()
})

// Set page meta
useHead({
  title: 'AI-Powered Budget Tracking - Budget Tracker'
})
</script>