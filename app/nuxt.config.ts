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
      // API base URL - defaults to port 5157 (matches backend launchSettings.json)
      // Override with NUXT_PUBLIC_API_BASE_URL environment variable if needed
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL || 'http://localhost:5157',
      googleClientId:
        process.env.NUXT_PUBLIC_GOOGLE_CLIENT_ID ||
        '715368478743-4vugo0hso9hmgouvepovj9jm56tkoutp.apps.googleusercontent.com'
    }
  },
  app: {
    head: {
      title: 'BudgetVu - Smart Budget Tracking',
      meta: [
        { name: 'description', content: 'AI-powered personal finance management. Track spending, gain insights, and take control of your budget with BudgetVu.' },
        { property: 'og:title', content: 'BudgetVu - Smart Budget Tracking' },
        { property: 'og:description', content: 'AI-powered personal finance management. Track spending, gain insights, and take control of your budget.' },
        { property: 'og:type', content: 'website' },
        { property: 'og:url', content: 'https://budgetvu.com' },
        { name: 'twitter:card', content: 'summary_large_image' },
        { name: 'twitter:title', content: 'BudgetVu - Smart Budget Tracking' },
        { name: 'twitter:description', content: 'AI-powered personal finance management' }
      ],
      link: [
        { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' },
        { rel: 'icon', type: 'image/x-icon', href: '/favicon.ico' }
      ],
      script: [
        {
          src: 'https://www.googletagmanager.com/gtag/js?id=G-S7D1TXEMG3',
          async: true
        },
        {
          innerHTML: `
            window.dataLayer = window.dataLayer || [];
            function gtag(){dataLayer.push(arguments);}
            gtag('js', new Date());
            gtag('config', 'G-S7D1TXEMG3');
          `,
          type: 'text/javascript'
        }
      ]
    }
  },
  ssr: false, // SPA mode for easier API integration
  nitro: {
    preset: 'static'
  },
  devServer: {
    port: 3000, // Explicitly set frontend dev server to port 3000
    host: 'localhost'
  }
})
