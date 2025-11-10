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
    // These are embedded at build time, so NUXT_PUBLIC_* env vars must be available during 'nuxt generate'
    public: {
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL || 'http://localhost:5157',
      googleClientId:
        process.env.NUXT_PUBLIC_GOOGLE_CLIENT_ID ||
        '715368478743-4vugo0hso9hmgouvepovj9jm56tkoutp.apps.googleusercontent.com'
    }
  },
  app: {
    head: {
      title: 'AI Budget Tracker',
      meta: [
        { name: 'description', content: 'AI-powered personal finance management' }
      ]
    }
  },
  ssr: false, // SPA mode for easier API integration
  nitro: {
    preset: 'static'
  }
})
