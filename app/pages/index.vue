<template>
  <div class="flex flex-col gap-5">

    <!-- Hero -->
    <div class="bg-gradient-to-br from-blue-600 via-blue-700 to-violet-700 -mx-4 -mt-6 px-4 py-9 text-center text-white rounded-b-3xl">

      <!-- Free badge -->
      <div class="inline-flex items-center gap-2 bg-green-500 border border-green-400 rounded-full px-3 py-1.5 mb-4 shadow-sm">
        <span class="w-2 h-2 bg-white rounded-full animate-pulse flex-shrink-0"></span>
        <span class="text-xs sm:text-sm font-bold tracking-wide text-white">BudgetVu is completely free</span>
      </div>

      <h1 class="text-2xl sm:text-3xl md:text-5xl font-bold mb-3 leading-tight px-2">
        Know exactly where your money goes
      </h1>

      <p class="text-blue-100 text-sm mb-5 max-w-sm sm:max-w-lg mx-auto px-2">
        Import your bank statements, let AI sort your transactions, and finally understand your spending.
      </p>

      <div class="flex gap-3 justify-center flex-wrap">
        <template v-if="authStore.isAuthenticated">
          <NuxtLink to="/dashboard" class="bg-white text-blue-700 px-7 py-2.5 rounded-lg font-bold text-base hover:bg-blue-50 transition-colors shadow-sm">
            Go to Dashboard
          </NuxtLink>
        </template>
        <template v-else>
          <NuxtLink to="/auth/register" class="bg-white text-blue-700 px-7 py-2.5 rounded-lg font-bold text-base hover:bg-blue-50 transition-colors shadow-sm">
            Get Started
          </NuxtLink>
          <NuxtLink to="/auth/login" class="bg-white/10 border border-white/30 text-white px-7 py-2.5 rounded-lg font-semibold text-base hover:bg-white/20 transition-colors">
            Sign In
          </NuxtLink>
        </template>
      </div>

    </div>

    <!-- Features: compact horizontal cards -->
    <div class="grid md:grid-cols-3 gap-3">
      <div class="bg-white rounded-xl border border-gray-200 p-4 flex items-start gap-3 shadow-sm">
        <div class="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center flex-shrink-0">
          <svg class="w-5 h-5 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/>
          </svg>
        </div>
        <div>
          <h3 class="font-semibold text-gray-900 mb-0.5">Smart Import</h3>
          <p class="text-sm text-gray-500">Download your bank statement and upload it here. AI reads and sorts everything automatically.</p>
        </div>
      </div>

      <div class="bg-white rounded-xl border border-gray-200 p-4 flex items-start gap-3 shadow-sm">
        <div class="w-10 h-10 bg-violet-100 rounded-lg flex items-center justify-center flex-shrink-0">
          <svg class="w-5 h-5 text-violet-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z"/>
          </svg>
        </div>
        <div>
          <h3 class="font-semibold text-gray-900 mb-0.5">Ask AI</h3>
          <p class="text-sm text-gray-500">Ask questions like "How much did I spend on dining last month?" and get instant answers.</p>
        </div>
      </div>

      <div class="bg-white rounded-xl border border-gray-200 p-4 flex items-start gap-3 shadow-sm">
        <div class="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center flex-shrink-0">
          <svg class="w-5 h-5 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/>
          </svg>
        </div>
        <div>
          <h3 class="font-semibold text-gray-900 mb-0.5">Clear Analytics</h3>
          <p class="text-sm text-gray-500">Spending charts, category breakdowns, and monthly summaries at a glance.</p>
        </div>
      </div>
    </div>

    <!-- How It Works: compact row -->
    <div class="bg-white rounded-xl border border-gray-200 px-4 py-4 shadow-sm">
      <p class="text-xs font-semibold text-gray-400 uppercase tracking-wider text-center mb-4">How it works</p>
      <div class="grid grid-cols-2 md:grid-cols-4 gap-3 text-center">
        <div v-for="(step, i) in howItWorks" :key="i" class="flex flex-col items-center gap-1.5">
          <div class="w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold flex-shrink-0"
               :class="step.color">
            {{ i + 1 }}
          </div>
          <p class="text-xs font-semibold text-gray-700 leading-tight">{{ step.title }}</p>
          <p class="text-xs text-gray-400 leading-tight">{{ step.desc }}</p>
        </div>
      </div>
    </div>

  </div>
</template>

<script setup lang="ts">
const authStore = useAuthStore()

const howItWorks = [
  { title: 'Create account', desc: 'Sign up in 30 sec', color: 'bg-blue-600 text-white' },
  { title: 'Connect your bank', desc: 'Download and upload statements', color: 'bg-blue-500 text-white' },
  { title: 'AI categorizes', desc: 'Automatic and smart', color: 'bg-violet-600 text-white' },
  { title: 'Get insights', desc: 'Ask anything', color: 'bg-green-600 text-white' }
]

onMounted(() => {
  authStore.initializeAuth()
})

useHead({
  title: 'BudgetVu | Free AI Budget Tracker'
})
</script>
