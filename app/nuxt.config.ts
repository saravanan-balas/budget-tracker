// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  css: ['~/assets/css/main.css'],
  modules: [
    '@nuxtjs/tailwindcss',
    '@pinia/nuxt',
    '@vueuse/nuxt'
  ],
  runtimeConfig: {
    // Public keys (exposed to client-side)
    public: {
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL || 'http://localhost:5157',
    }
  },
  app: {
    head: {
      title: 'Budget Tracker',
      meta: [
        { name: 'description', content: 'AI-powered personal finance management' }
      ]
    }
  },
  ssr: false // SPA mode for easier API integration
})
